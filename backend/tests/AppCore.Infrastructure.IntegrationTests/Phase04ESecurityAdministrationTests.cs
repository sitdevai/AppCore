using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class Phase04ESecurityAdministrationTests(PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task SessionsCanBeListedAndRevokedAndAuditSearchIsAudited()
    {
        await using ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var actor = User(now);
        var target = User(now);
        var current = Session(actor, now);
        var targetSession = Session(target, now);
        context.Users.AddRange(actor, target);
        context.ServerSessions.AddRange(current, targetSession);
        await context.SaveChangesAsync();
        var auditWriter = new SecurityAuditWriter(context, new FixedTimeProvider(now));
        var service = new SecurityAdministrationService(context, auditWriter);

        IReadOnlyList<SessionAdministrationResult> own =
            await service.ListSessionsAsync(actor.Id, current.Id, null);
        Assert.Single(own);
        Assert.True(own[0].IsCurrent);
        Assert.True(await service.RevokeSessionAsync(
            actor.Id, current.Id, target.Id, targetSession.Id));
        Assert.NotNull((await context.ServerSessions.AsNoTracking()
            .SingleAsync(value => value.Id == targetSession.Id)).RevokedAtUtc);

        SecurityAuditPage page = await service.SearchAuditAsync(
            actor.Id,
            new SecurityAuditQuery(SecurityAuditCodes.SessionRevoked, null, target.Id,
                null, null, 1, 25));
        Assert.Single(page.Items);
        Assert.Equal(target.Id, page.Items[0].TargetUserId);
        Assert.True(await context.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(value => value.EventCode == SecurityAuditCodes.AuditViewed));

        SecurityAuditPage ascending = await service.SearchAuditAsync(
            actor.Id,
            new SecurityAuditQuery(null, null, null, null, null, 1, 100,
                "eventCode", "asc"));
        Assert.Equal(
            ascending.Items.Select(value => value.EventCode)
                .OrderBy(value => value, StringComparer.Ordinal),
            ascending.Items.Select(value => value.EventCode));

        SecurityAuditPage descending = await service.SearchAuditAsync(
            actor.Id,
            new SecurityAuditQuery(null, null, null, null, null, 1, 100,
                "eventCode", "desc"));
        Assert.Equal(
            descending.Items.Select(value => value.EventCode)
                .OrderByDescending(value => value, StringComparer.Ordinal),
            descending.Items.Select(value => value.EventCode));

        var authenticator = new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            ProtectedSecret = [1],
            CreatedAtUtc = now,
            VerifiedAtUtc = now,
        };
        var expiredPreSession = new AnonymousPreSession
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddMinutes(-30),
        };
        context.MfaAuthenticators.Add(authenticator);
        context.AnonymousPreSessions.Add(expiredPreSession);
        context.MfaLoginChallenges.Add(new MfaLoginChallenge
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            AnonymousPreSessionId = expiredPreSession.Id,
            AuthorizationVersionAtIssue = actor.AuthorizationVersion,
            AuthenticatorId = authenticator.Id,
            ExpiresAtUtc = now.AddMinutes(-20),
        });
        await context.SaveChangesAsync();
        var preSessions = new AnonymousPreSessionStore(
            context, new FixedTimeProvider(now));
        Guid freshPreSession = await preSessions.CreateAsync(TimeSpan.FromMinutes(15));
        Assert.False(await context.AnonymousPreSessions.AsNoTracking()
            .AnyAsync(value => value.Id == expiredPreSession.Id));

        await context.ServerSessions.Where(value =>
            value.UserId == actor.Id || value.UserId == target.Id).ExecuteDeleteAsync();
        await context.AnonymousPreSessions.Where(value => value.Id == freshPreSession)
            .ExecuteDeleteAsync();
        await context.MfaAuthenticators.Where(value => value.Id == authenticator.Id)
            .ExecuteDeleteAsync();
        await context.Users.Where(value =>
            value.Id == actor.Id || value.Id == target.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task OwnOtherSessionOperationsNeverRevokeTheCurrentSession()
    {
        await using ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = User(now);
        ServerSession current = Session(actor, now);
        ServerSession other = Session(actor, now);
        context.Users.Add(actor);
        context.ServerSessions.AddRange(current, other);
        await context.SaveChangesAsync();
        var service = new SecurityAdministrationService(
            context, new SecurityAuditWriter(context, new FixedTimeProvider(now)));

        Assert.False(await service.RevokeSessionAsync(
            actor.Id, current.Id, actor.Id, current.Id));
        Assert.Equal(1, await service.RevokeUserSessionsAsync(
            actor.Id, current.Id, actor.Id));
        await context.Entry(current).ReloadAsync();
        await context.Entry(other).ReloadAsync();
        Assert.Null(current.RevokedAtUtc);
        Assert.NotNull(other.RevokedAtUtc);

        await context.ServerSessions.Where(value => value.UserId == actor.Id)
            .ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == actor.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ProtectedOwnerSessionsAreVisibleButCannotBeRevokedRoutinely()
    {
        await using ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = User(now);
        ApplicationUser owner = User(now);
        owner.IsProtectedOwner = true;
        ServerSession current = Session(actor, now);
        ServerSession ownerSession = Session(owner, now);
        context.Users.AddRange(actor, owner);
        context.ServerSessions.AddRange(current, ownerSession);
        await context.SaveChangesAsync();
        var service = new SecurityAdministrationService(
            context, new SecurityAuditWriter(context, new FixedTimeProvider(now)));

        IReadOnlyList<SessionAdministrationResult> visible =
            await service.ListSessionsAsync(actor.Id, current.Id, owner.Id);
        Assert.Single(visible);
        Assert.Equal(ownerSession.Id, visible[0].SessionId);
        Assert.False(await service.RevokeSessionAsync(
            actor.Id, current.Id, owner.Id, ownerSession.Id));
        await context.Entry(ownerSession).ReloadAsync();
        Assert.Null(ownerSession.RevokedAtUtc);

        await context.ServerSessions.Where(value =>
            value.UserId == actor.Id || value.UserId == owner.Id).ExecuteDeleteAsync();
        await context.Users.Where(value =>
            value.Id == actor.Id || value.Id == owner.Id).ExecuteDeleteAsync();
    }

    [Theory]
    [InlineData("single")]
    [InlineData("user")]
    [InlineData("global")]
    public async Task FailedAuditRollsBackSessionRevocation(string operation)
    {
        await using ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = User(now);
        ApplicationUser target = User(now);
        ServerSession current = Session(actor, now);
        ServerSession targetSession = Session(target, now);
        context.Users.AddRange(actor, target);
        context.ServerSessions.AddRange(current, targetSession);
        await context.SaveChangesAsync();
        var service = new SecurityAdministrationService(context, new ThrowingAuditWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() => operation switch
        {
            "single" => AsTask(service.RevokeSessionAsync(
                actor.Id, current.Id, target.Id, targetSession.Id)),
            "user" => AsTask(service.RevokeUserSessionsAsync(
                actor.Id, current.Id, target.Id)),
            _ => AsTask(service.RevokeGlobalSessionsAsync(actor.Id)),
        });
        await context.Entry(current).ReloadAsync();
        await context.Entry(targetSession).ReloadAsync();
        Assert.Null(current.RevokedAtUtc);
        Assert.Null(targetSession.RevokedAtUtc);

        await context.ServerSessions.Where(value =>
            value.UserId == actor.Id || value.UserId == target.Id).ExecuteDeleteAsync();
        await context.Users.Where(value =>
            value.Id == actor.Id || value.Id == target.Id).ExecuteDeleteAsync();
    }

    private static async Task AsTask<T>(Task<T> task) => _ = await task;

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseNpgsql(database.ConnectionString, npgsql => npgsql
            .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
            .MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Infrastructure));
        return new ApplicationDbContext(options.Options);
    }

    private static ApplicationUser User(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"phase04e-{Guid.NewGuid():N}",
        NormalizedUserName = $"PHASE04E-{Guid.NewGuid():N}",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = now,
        AccountStatus = AccountStatus.Enabled,
        CredentialStatus = CredentialStatus.Active,
    };

    private static ServerSession Session(ApplicationUser user, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        AuthorizationVersion = user.AuthorizationVersion,
        CreatedAtUtc = now,
        LastActivityAtUtc = now,
        AbsoluteExpiresAtUtc = now.AddHours(8),
        AuthenticationMethods = "password,totp",
        MfaVerifiedAtUtc = now,
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ThrowingAuditWriter : ISecurityAuditWriter
    {
        public Task WriteAsync(SecurityAuditEntry entry,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit failure.");
    }
}
