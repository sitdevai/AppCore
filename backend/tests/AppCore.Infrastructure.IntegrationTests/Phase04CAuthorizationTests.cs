using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class Phase04CAuthorizationTests(PostgreSqlContainerFixture database)
{
    [Fact]
    public void CatalogIsCompleteUniqueAndClassified()
    {
        Assert.Equal(29, SystemPermissions.Catalog.Count);
        Assert.Equal(
            SystemPermissions.Catalog.Count,
            SystemPermissions.Catalog.Select(value => value.Id).Distinct().Count());
        Assert.All(SystemPermissions.Catalog, permission =>
            Assert.True(Enum.IsDefined(permission.Assurance)));
        Assert.Equal(
            PermissionAssurance.Emergency,
            SystemPermissions.Find(SystemPermissions.SessionsRevokeGlobal)?.Assurance);
        Assert.Null(SystemPermissions.Find("Unknown.Permission"));
    }

    [Fact]
    public async Task PermissionEvaluationDeniesUnknownAndEnforcesMfaAndFreshness()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        context.Users.Add(user);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = user.Id,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        await context.SaveChangesAsync();
        var service = new PermissionAuthorizationService(context);
        var passwordSession = Session(user, now, null, "password");
        var staleMfaSession = Session(user, now, now.AddMinutes(-16), "password,totp");
        var freshMfaSession = Session(user, now, now.AddMinutes(-2), "password,totp");
        var futureMfaSession = Session(user, now, now.AddMinutes(2), "password,totp");

        Assert.False(await service.HasPermissionAsync(passwordSession, "Unknown.Permission"));
        Assert.True(await service.HasPermissionAsync(passwordSession, SystemPermissions.UsersView));
        Assert.False(await service.HasPermissionAsync(passwordSession, SystemPermissions.UsersCreate));
        Assert.True(await service.HasPermissionAsync(freshMfaSession, SystemPermissions.UsersCreate));
        Assert.False(await service.HasPermissionAsync(staleMfaSession, SystemPermissions.SessionsRevokeGlobal));
        Assert.False(await service.HasPermissionAsync(futureMfaSession, SystemPermissions.SessionsRevokeGlobal));
        Assert.True(await service.HasPermissionAsync(freshMfaSession, SystemPermissions.SessionsRevokeGlobal));

        await context.UserRoles.Where(value => value.UserId == user.Id).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == user.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task DevelopmentProtectedOwnerCanExerciseHighRiskPermissionsWithoutMfa()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser owner = CreateUser(now);
        owner.IsProtectedOwner = true;
        context.Users.Add(owner);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = owner.Id,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        await context.SaveChangesAsync();
        ValidatedSession passwordSession = Session(owner, now, null, "password");

        var development = new PermissionAuthorizationService(
            context, new TestHostEnvironment(Environments.Development));
        var production = new PermissionAuthorizationService(
            context, new TestHostEnvironment(Environments.Production));

        Assert.True(await development.HasPermissionAsync(
            passwordSession, SystemPermissions.SettingsVisualIdentityUpdate));
        Assert.False(await production.HasPermissionAsync(
            passwordSession, SystemPermissions.SettingsVisualIdentityUpdate));

        await context.UserRoles.Where(value => value.UserId == owner.Id).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == owner.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task DataScopeAllowsProtectedOwnerReadsButDeniesMutations()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = CreateUser(now);
        ApplicationUser protectedOwner = CreateUser(now);
        protectedOwner.IsProtectedOwner = true;
        context.Users.AddRange(actor, protectedOwner);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = actor.Id,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        await context.SaveChangesAsync();
        var service = new PermissionAuthorizationService(context);
        ValidatedSession session = Session(actor, now, now, "password,totp");

        Assert.False(await service.AuthorizeAsync(session, SystemPermissions.UsersView));
        Assert.True(await service.AuthorizeAsync(session, SystemPermissions.UsersView, protectedOwner.Id));
        Assert.True(await service.AuthorizeAsync(
            session, SystemPermissions.SessionsViewForUser, protectedOwner.Id));
        Assert.False(await service.AuthorizeAsync(
            session, SystemPermissions.UsersUpdate, protectedOwner.Id));
        Assert.True(await service.AuthorizeAsync(session, SystemPermissions.SessionsViewOwn, actor.Id));
        Assert.False(await service.AuthorizeAsync(session, SystemPermissions.SessionsViewOwn, protectedOwner.Id));

        await context.UserRoles.Where(value => value.UserId == actor.Id).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == actor.Id || value.Id == protectedOwner.Id)
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task RoleAssignmentRejectsToxicCombinationAndRevokesSessions()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = CreateUser(now);
        ApplicationUser target = CreateUser(now);
        target.MfaState = MfaState.Active;
        context.Users.AddRange(actor, target);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = actor.Id,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        context.MfaAuthenticators.Add(new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = target.Id,
            ProtectedSecret = [1],
            CreatedAtUtc = now,
            VerifiedAtUtc = now,
        });
        var activeSession = new ServerSession
        {
            Id = Guid.NewGuid(),
            UserId = target.Id,
            AuthorizationVersion = target.AuthorizationVersion,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            AbsoluteExpiresAtUtc = now.AddHours(1),
        };
        context.ServerSessions.Add(activeSession);
        await context.SaveChangesAsync();
        var service = new RoleAuthorizationService(
            context,
            new SecurityStateRevocationService(context, new FixedTimeProvider(now)),
            new SecurityAuditWriter(context, new FixedTimeProvider(now)));
        string userAdminStamp = (await context.Roles.FindAsync(SystemRoleIds.UserAdministrator))!.ConcurrencyStamp!;
        string securityAdminStamp = (await context.Roles.FindAsync(SystemRoleIds.SecurityAdministrator))!.ConcurrencyStamp!;

        Assert.True(await service.AssignRoleAsync(
            actor.Id,
            target.Id,
            SystemRoleIds.UserAdministrator,
            userAdminStamp));
        Assert.False(await service.AssignRoleAsync(
            actor.Id,
            target.Id,
            SystemRoleIds.SecurityAdministrator,
            securityAdminStamp));
        Assert.NotNull((await context.ServerSessions.AsNoTracking()
            .SingleAsync(value => value.Id == activeSession.Id)).RevokedAtUtc);

        await context.UserRoles
            .Where(value => value.UserId == actor.Id || value.UserId == target.Id)
            .ExecuteDeleteAsync();
        await context.ServerSessions.Where(value => value.UserId == target.Id).ExecuteDeleteAsync();
        await context.MfaAuthenticators.Where(value => value.UserId == target.Id).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == actor.Id || value.Id == target.Id)
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CustomRoleLifecycleUsesConcurrencyAndRejectsArchivalWhileAssigned()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = CreateUser(now);
        ApplicationUser target = CreateUser(now);
        context.Users.AddRange(actor, target);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = actor.Id,
            RoleId = SystemRoleIds.SystemAdministrator,
        });
        await context.SaveChangesAsync();
        var service = new RoleAuthorizationService(
            context,
            new SecurityStateRevocationService(context, new FixedTimeProvider(now)),
            new SecurityAuditWriter(context, new FixedTimeProvider(now)));

        Guid roleId = Assert.IsType<Guid>(await service.CreateRoleAsync(
            actor.Id,
            $"Custom {Guid.NewGuid():N}"));
        ApplicationRole role = await context.Roles.SingleAsync(value => value.Id == roleId);
        string initialStamp = role.ConcurrencyStamp!;
        Assert.True(await service.ReplaceRolePermissionsAsync(
            actor.Id,
            roleId,
            [SystemPermissions.SessionsViewOwn],
            initialStamp));
        Assert.False(await service.ReplaceRolePermissionsAsync(
            actor.Id,
            roleId,
            [SystemPermissions.SessionsViewOwn],
            initialStamp));
        await context.Entry(role).ReloadAsync();
        Assert.True(await service.AssignRoleAsync(
            actor.Id,
            target.Id,
            roleId,
            role.ConcurrencyStamp!));
        Assert.False(await service.ArchiveRoleAsync(
            actor.Id,
            roleId,
            role.ConcurrencyStamp!));
        Assert.True(await service.RemoveRoleAsync(actor.Id, target.Id, roleId));
        Assert.True(await service.ArchiveRoleAsync(
            actor.Id,
            roleId,
            role.ConcurrencyStamp!));

        await context.RolePermissions.Where(value => value.RoleId == roleId).ExecuteDeleteAsync();
        await context.Roles.Where(value => value.Id == roleId).ExecuteDeleteAsync();
        await context.UserRoles.Where(value => value.UserId == actor.Id).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == actor.Id || value.Id == target.Id)
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task DelegatedRoleEditorCannotRemoveAPermissionTheyDoNotPossess()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser actor = CreateUser(now);
        Guid editorRoleId = Guid.NewGuid();
        Guid targetRoleId = Guid.NewGuid();
        var editorRole = new ApplicationRole
        {
            Id = editorRoleId,
            Name = $"Editor-{editorRoleId:N}",
            NormalizedName = $"EDITOR-{editorRoleId:N}",
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        };
        var targetRole = new ApplicationRole
        {
            Id = targetRoleId,
            Name = $"Target-{targetRoleId:N}",
            NormalizedName = $"TARGET-{targetRoleId:N}",
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        };
        context.Users.Add(actor);
        context.Roles.AddRange(editorRole, targetRole);
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = actor.Id,
            RoleId = editorRoleId,
        });
        context.RolePermissions.AddRange(
            new RolePermissionAssignment
            {
                RoleId = editorRoleId,
                PermissionId = SystemPermissions.PermissionsAssignToRoles,
            },
            new RolePermissionAssignment
            {
                RoleId = editorRoleId,
                PermissionId = SystemPermissions.RolesView,
            },
            new RolePermissionAssignment
            {
                RoleId = targetRoleId,
                PermissionId = SystemPermissions.RolesView,
            },
            new RolePermissionAssignment
            {
                RoleId = targetRoleId,
                PermissionId = SystemPermissions.SessionsRevokeGlobal,
            });
        await context.SaveChangesAsync();
        var service = new RoleAuthorizationService(
            context,
            new SecurityStateRevocationService(context, new FixedTimeProvider(now)),
            new SecurityAuditWriter(context, new FixedTimeProvider(now)));

        Assert.False(await service.ReplaceRolePermissionsAsync(
            actor.Id, targetRoleId, [SystemPermissions.RolesView],
            targetRole.ConcurrencyStamp!));
        Assert.True(await context.RolePermissions.AnyAsync(value =>
            value.RoleId == targetRoleId
            && value.PermissionId == SystemPermissions.SessionsRevokeGlobal));

        await context.UserRoles.Where(value => value.UserId == actor.Id).ExecuteDeleteAsync();
        await context.RolePermissions.Where(value =>
            value.RoleId == editorRoleId || value.RoleId == targetRoleId).ExecuteDeleteAsync();
        await context.Roles.Where(value =>
            value.Id == editorRoleId || value.Id == targetRoleId).ExecuteDeleteAsync();
        await context.Users.Where(value => value.Id == actor.Id).ExecuteDeleteAsync();
    }

    private async Task<ApplicationDbContext> CreateMigratedContextAsync()
    {
        ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        return context;
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseNpgsql(
            database.ConnectionString,
            npgsql => npgsql
                .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Infrastructure));
        return new ApplicationDbContext(options.Options);
    }

    private static ApplicationUser CreateUser(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"phase04c-{Guid.NewGuid():N}",
        NormalizedUserName = $"PHASE04C-{Guid.NewGuid():N}",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = now,
        AccountStatus = AccountStatus.Enabled,
        CredentialStatus = CredentialStatus.Active,
    };

    private static ValidatedSession Session(
        ApplicationUser user,
        DateTimeOffset now,
        DateTimeOffset? mfaVerifiedAt,
        string methods) =>
        new(Guid.NewGuid(), user.Id, user.AuthorizationVersion, now.AddHours(1), now, mfaVerifiedAt, methods);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
