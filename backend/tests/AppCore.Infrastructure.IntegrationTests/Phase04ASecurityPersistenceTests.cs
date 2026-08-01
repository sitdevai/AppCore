using System.Globalization;
using System.Security.Cryptography;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using AppCore.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class Phase04ASecurityPersistenceTests(
    PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task SessionValidationTouchesActiveSessionAndRejectsExpiredSession()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        user.AuthorizationVersion = 7;
        var active = new ServerSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AuthorizationVersion = 7,
            CreatedAtUtc = now.AddMinutes(-5),
            LastActivityAtUtc = now.AddMinutes(-1),
            AbsoluteExpiresAtUtc = now.AddHours(1),
            AuthenticationMethods = "password,totp",
            MfaVerifiedAtUtc = now.AddMinutes(-2),
        };
        var expired = new ServerSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AuthorizationVersion = 7,
            CreatedAtUtc = now.AddHours(-2),
            LastActivityAtUtc = now.AddMinutes(-31),
            AbsoluteExpiresAtUtc = now.AddHours(1),
            AuthenticationMethods = "password",
        };
        context.Users.Add(user);
        context.ServerSessions.AddRange(active, expired);
        await context.SaveChangesAsync();

        var validator = new ServerSessionValidator(context);
        ValidatedSession? validated =
            await validator.ValidateAsync(active.Id, 7);
        ValidatedSession? rejected =
            await validator.ValidateAsync(expired.Id, 7);

        Assert.NotNull(validated);
        Assert.Equal(user.Id, validated.UserId);
        Assert.Null(rejected);
        await context.Entry(expired).ReloadAsync();
        Assert.Equal(
            now.AddMinutes(-31).ToUnixTimeMilliseconds(),
            expired.LastActivityAtUtc.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ConcurrentTotpStepAdvancementSucceedsExactlyOnce()
    {
        Guid authenticatorId;
        await using (ApplicationDbContext setup = await CreateMigratedContextAsync())
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ApplicationUser user = CreateUser(now);
            authenticatorId = Guid.NewGuid();
            setup.Users.Add(user);
            setup.MfaAuthenticators.Add(
                new MfaAuthenticator
                {
                    Id = authenticatorId,
                    UserId = user.Id,
                    ProtectedSecret = [1, 2, 3],
                    CreatedAtUtc = now,
                });
            await setup.SaveChangesAsync();
        }

        Task<bool>[] attempts = Enumerable.Range(0, 8)
            .Select(async _ =>
            {
                await using ApplicationDbContext context = CreateContext();
                var store = new AtomicSecurityStateStore(context, TimeProvider.System);
                return await store.AdvanceTotpStepAsync(authenticatorId, 123456);
            })
            .ToArray();

        bool[] results = await Task.WhenAll(attempts);

        Assert.Single(results, value => value);
    }

    [Fact]
    public async Task AuditWriterRejectsUnapprovedDetailsAndAuditIsImmutable()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        var writer = new SecurityAuditWriter(context, TimeProvider.System);

        foreach (string prohibitedKey in new[]
                 {
                     "activationCode",
                     "otp",
                     "sessionId",
                     "recoveryShare",
                 })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.SessionCreated,
                    "success",
                    Details: new Dictionary<string, string?>
                    {
                        [prohibitedKey] = "must-not-persist",
                    })));
        }

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.SessionCreated,
                "success",
                Details: new Dictionary<string, string?>
                {
                    ["reason"] = "raw-activation-material",
                })));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.SessionCreated,
                "success",
                Details: new Dictionary<string, string?>
                {
                    ["revokedCount"] = "-1",
                })));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.SessionCreated,
                new string('a', 65))));

        await writer.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.SessionCreated,
                "success",
                CorrelationId: "phase04a-test",
                SourceIp: new string('1', 100),
                UserAgent: new string('u', 700),
                Details: new Dictionary<string, string?>
                {
                    ["reason"] = "concurrent_limit",
                    ["revokedCount"] = "1",
                }));

        SecurityAuditEvent auditEvent =
            await context.SecurityAuditEvents.SingleAsync(
                value => value.CorrelationId == "phase04a-test");
        Assert.Contains("concurrent_limit", auditEvent.DetailsJson);
        SecurityAuditContext auditContext =
            await context.SecurityAuditContexts.SingleAsync(
                value => value.SecurityAuditEventId == auditEvent.Id);
        Assert.Equal(64, auditContext.SourceIp?.Length);
        Assert.Equal(512, auditContext.UserAgent?.Length);
        Assert.InRange(
            auditContext.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddDays(89),
            DateTimeOffset.UtcNow.AddDays(91));

        auditContext.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();
        var retention = new SecurityAuditContextRetentionService(context);
        Assert.Equal(1, await retention.DeleteExpiredAsync());
        Assert.True(await context.SecurityAuditEvents.AnyAsync(
            value => value.Id == auditEvent.Id));

        context.Entry(auditEvent).Property("ResultCode").CurrentValue = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
        await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE security.security_audit_events SET \"ResultCode\" = 'changed' WHERE \"Id\" = {auditEvent.Id}"));
    }

    [Fact]
    public async Task AnonymousPreSessionConsumptionIsExactlyOnce()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        var store = new AnonymousPreSessionStore(context, TimeProvider.System);
        Guid id = await store.CreateAsync(TimeSpan.FromMinutes(5));

        bool first = await store.ConsumeAsync(id);
        bool replay = await store.ConsumeAsync(id);

        Assert.True(first);
        Assert.False(replay);
    }

    [Fact]
    public async Task ConcurrentSessionRotationNeverExceedsThreeActiveSessions()
    {
        Guid userId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationDbContext setup = await CreateMigratedContextAsync())
        {
            ApplicationUser user = CreateUser(now);
            userId = user.Id;
            setup.Users.Add(user);
            setup.ServerSessions.AddRange(
                CreateSession(userId, now.AddMinutes(-4), now),
                CreateSession(userId, now.AddMinutes(-3), now),
                CreateSession(userId, now.AddMinutes(-2), now),
                CreateSession(
                    userId,
                    now.AddHours(-2),
                    now.AddMinutes(-31)));
            await setup.SaveChangesAsync();
        }

        Task<Guid>[] rotations = Enumerable.Range(0, 2)
            .Select(async _ =>
            {
                await using ApplicationDbContext context = CreateContext();
                var writer = new SecurityAuditWriter(context, TimeProvider.System);
                var service = new SessionRotationService(context, writer);
                return await service.RotateAsync(
                    userId,
                    null,
                    0,
                    null,
                    "password");
            })
            .ToArray();

        await Task.WhenAll(rotations);

        await using ApplicationDbContext verification = CreateContext();
        DateTimeOffset idleBoundary = DateTimeOffset.UtcNow.AddMinutes(-30);
        ServerSession[] active = await verification.ServerSessions
            .Where(session =>
                session.UserId == userId
                && session.RevokedAtUtc == null
                && session.AbsoluteExpiresAtUtc > DateTimeOffset.UtcNow
                && session.LastActivityAtUtc > idleBoundary)
            .ToArrayAsync();
        ServerSession idle = await verification.ServerSessions
            .SingleAsync(session =>
                session.UserId == userId
                && session.CreatedAtUtc == now.AddHours(-2));
        int concurrentRevocations = await verification.SecurityAuditEvents
            .CountAsync(audit =>
                audit.EventCode
                == SecurityAuditCodes.ConcurrentSessionRevoked
                && audit.TargetUserId == userId);

        Assert.Equal(3, active.Length);
        Assert.Null(idle.RevokedAtUtc);
        Assert.Equal(2, concurrentRevocations);
    }

    [Fact]
    public async Task SecurityChallengeReplacementAndAttemptLimitsAreAtomic()
    {
        Guid userId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationDbContext setup = await CreateMigratedContextAsync())
        {
            ApplicationUser user = CreateUser(now);
            userId = user.Id;
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
        }

        Task[] replacements = Enumerable.Range(0, 2)
            .Select(async _ =>
            {
                await using ApplicationDbContext context = CreateContext();
                var store = new AtomicSecurityStateStore(
                    context,
                    TimeProvider.System);
                await store.ReplaceSecurityChallengeAsync(
                    CreateChallenge(userId, now));
            })
            .ToArray();
        await Task.WhenAll(replacements);

        Guid activeId;
        await using (ApplicationDbContext verification = CreateContext())
        {
            SecurityChallenge[] challenges = await verification.SecurityChallenges
                .Where(challenge => challenge.UserId == userId)
                .ToArrayAsync();
            SecurityChallenge active = Assert.Single(
                challenges,
                challenge =>
                    challenge.ConsumedAtUtc == null
                    && challenge.InvalidatedAtUtc == null);
            activeId = active.Id;
        }

        Task<bool>[] attempts = Enumerable.Range(0, 8)
            .Select(async _ =>
            {
                await using ApplicationDbContext context = CreateContext();
                var store = new AtomicSecurityStateStore(
                    context,
                    TimeProvider.System);
                return await store.IncrementSecurityChallengeAttemptAsync(activeId);
            })
            .ToArray();
        bool[] attemptResults = await Task.WhenAll(attempts);

        Assert.Equal(5, attemptResults.Count(result => result));
        await using ApplicationDbContext finalContext = CreateContext();
        SecurityChallenge persisted =
            await finalContext.SecurityChallenges.SingleAsync(
                challenge => challenge.Id == activeId);
        Assert.Equal(5, persisted.AttemptCount);
        var consumptionStore = new AtomicSecurityStateStore(
            finalContext,
            TimeProvider.System);
        Assert.False(await consumptionStore.ConsumeSecurityChallengeAsync(activeId));

        SecurityChallenge consumable = CreateChallenge(
            userId,
            DateTimeOffset.UtcNow);
        await consumptionStore.ReplaceSecurityChallengeAsync(consumable);
        Task<bool>[] consumptions = Enumerable.Range(0, 2)
            .Select(async _ =>
            {
                await using ApplicationDbContext consumeContext = CreateContext();
                var store = new AtomicSecurityStateStore(
                    consumeContext,
                    TimeProvider.System);
                return await store.ConsumeSecurityChallengeAsync(consumable.Id);
            })
            .ToArray();
        bool[] consumptionResults = await Task.WhenAll(consumptions);
        Assert.Single(consumptionResults, result => result);
    }

    [Fact]
    public async Task MfaLoginChallengeReplacementIsAtomicAndInvalidatesPrevious()
    {
        Guid userId;
        Guid preSessionId;
        Guid authenticatorId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationDbContext setup = await CreateMigratedContextAsync())
        {
            ApplicationUser user = CreateUser(now);
            var preSession = new AnonymousPreSession
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(10),
            };
            userId = user.Id;
            preSessionId = preSession.Id;
            var authenticator = new MfaAuthenticator
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProtectedSecret = new byte[20],
                CreatedAtUtc = now,
                VerifiedAtUtc = now,
            };
            authenticatorId = authenticator.Id;
            setup.AddRange(user, preSession, authenticator);
            await setup.SaveChangesAsync();
        }

        var previous = CreateMfaLoginChallenge(
            userId,
            preSessionId,
            authenticatorId,
            now);
        await using (ApplicationDbContext initial = CreateContext())
        {
            var store = new AtomicSecurityStateStore(initial, TimeProvider.System);
            await store.ReplaceMfaLoginChallengeAsync(previous);
        }

        Task[] replacements = Enumerable.Range(0, 2)
            .Select(async _ =>
            {
                await using ApplicationDbContext context = CreateContext();
                var store = new AtomicSecurityStateStore(context, TimeProvider.System);
                await store.ReplaceMfaLoginChallengeAsync(
                    CreateMfaLoginChallenge(
                        userId,
                        preSessionId,
                        authenticatorId,
                        now));
            })
            .ToArray();
        await Task.WhenAll(replacements);

        await using ApplicationDbContext verification = CreateContext();
        MfaLoginChallenge[] challenges = await verification.MfaLoginChallenges
            .Where(challenge => challenge.UserId == userId)
            .ToArrayAsync();
        Assert.Single(
            challenges,
            challenge =>
                challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null);
        Assert.False(
            await new AtomicSecurityStateStore(verification, TimeProvider.System)
                .ConsumeLoginChallengeAsync(previous.Id));
    }

    [Fact]
    public async Task OptionalEmailHasDatabaseLevelConditionalUniqueness()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser firstWithoutEmail = CreateUser(now);
        ApplicationUser secondWithoutEmail = CreateUser(now);
        context.Users.AddRange(firstWithoutEmail, secondWithoutEmail);
        await context.SaveChangesAsync();

        ApplicationUser firstWithEmail = CreateUser(now);
        firstWithEmail.Email = "person@example.edu";
        firstWithEmail.NormalizedEmail = "PERSON@EXAMPLE.EDU";
        context.Users.Add(firstWithEmail);
        await context.SaveChangesAsync();

        ApplicationUser duplicate = CreateUser(now);
        duplicate.Email = "PERSON@example.edu";
        duplicate.NormalizedEmail = "PERSON@EXAMPLE.EDU";
        context.Users.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SensitiveRecheckRequiresTransactionAndLocksValidRows()
    {
        await using ApplicationDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        var session = CreateSession(user.Id, now, now);
        context.Users.Add(user);
        context.ServerSessions.Add(session);
        await context.SaveChangesAsync();
        var validator = new ServerSessionValidator(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.RecheckAsync(session.Id, 0));

        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.True(await validator.RecheckAsync(session.Id, 0));
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ActivationEnableAndLoginWorkflowCompletesWithoutPrematureSession()
    {
        await using (ApplicationDbContext migration = await CreateMigratedContextAsync())
        {
        }

        await using ServiceProvider services = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IAccountLifecycleService lifecycle =
            scope.ServiceProvider.GetRequiredService<IAccountLifecycleService>();
        IAnonymousPreSessionStore preSessions =
            scope.ServiceProvider.GetRequiredService<IAnonymousPreSessionStore>();
        IAuthenticationWorkflowService authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationWorkflowService>();

        AccountCreationResult account = await lifecycle.CreateAsync(
            $"workflow-{Guid.NewGuid():N}",
            null,
            protectedOwner: false);
        OneTimeChallengeResult challenge = await lifecycle.IssueChallengeAsync(
            account.UserId,
            "activation");
        Guid activationPreSession = await preSessions.CreateAsync(
            TimeSpan.FromMinutes(5));

        Assert.False(await lifecycle.CompleteChallengeAsync(
            account.Username,
            "activation",
            challenge.Code,
            "123456789012345",
            activationPreSession));
        Assert.True(await lifecycle.CompleteChallengeAsync(
            account.Username,
            "activation",
            challenge.Code,
            "A sufficiently long password 2026!",
            activationPreSession));
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await context.ServerSessions
            .Where(value => value.UserId == account.UserId)
            .ToArrayAsync());
        Assert.True(await lifecycle.TransitionAsync(account.UserId, "enable"));

        Guid loginPreSession = await preSessions.CreateAsync(
            TimeSpan.FromMinutes(5));
        LoginWorkflowResult login = await authentication.LoginAsync(
            account.Username,
            "A sufficiently long password 2026!",
            loginPreSession);

        Assert.Equal(LoginWorkflowStatus.Authenticated, login.Status);
        Assert.NotNull(login.SessionId);
        Assert.True(await context.ServerSessions.AnyAsync(
            value =>
                value.Id == login.SessionId
                && value.UserId == account.UserId
                && value.RevokedAtUtc == null));
    }

    [Fact]
    public async Task PasswordSecurityChangeInvalidatesIssuedMfaLoginChallenge()
    {
        await using ServiceProvider provider = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAnonymousPreSessionStore preSessions =
            scope.ServiceProvider.GetRequiredService<IAnonymousPreSessionStore>();
        IAuthenticationWorkflowService workflow =
            scope.ServiceProvider.GetRequiredService<IAuthenticationWorkflowService>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        user.MfaState = MfaState.Active;
        const string password = "A sufficiently long password 2026!";
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        byte[] secret = RandomNumberGenerator.GetBytes(20);
        var authenticator = new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProtectedSecret = secret,
            CreatedAtUtc = now,
            VerifiedAtUtc = now,
        };
        context.MfaAuthenticators.Add(authenticator);
        await context.SaveChangesAsync();
        Guid preSessionId = await preSessions.CreateAsync(TimeSpan.FromMinutes(5));

        LoginWorkflowResult pending = await workflow.LoginAsync(
            user.UserName!,
            password,
            preSessionId);
        Assert.Equal(LoginWorkflowStatus.MfaRequired, pending.Status);

        user.AuthorizationVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync();
        string code = TotpCode(secret, DateTimeOffset.UtcNow);
        LoginWorkflowResult completed = await workflow.CompleteMfaLoginAsync(
            pending.MfaChallengeId!.Value,
            preSessionId,
            code);

        Assert.Equal(LoginWorkflowStatus.Invalid, completed.Status);
        Assert.False(await context.ServerSessions.AnyAsync(
            value => value.UserId == user.Id && value.RevokedAtUtc == null));
    }

    [Fact]
    public async Task MfaEnrollmentRotatesPasswordOnlySession()
    {
        await using ServiceProvider provider = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ISessionRotationService sessions =
            scope.ServiceProvider.GetRequiredService<ISessionRotationService>();
        IMfaEnrollmentService enrollment =
            scope.ServiceProvider.GetRequiredService<IMfaEnrollmentService>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        const string password = "A sufficiently long enrollment password 2026!";
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Guid oldSessionId = await sessions.RotateAsync(
            user.Id,
            null,
            user.AuthorizationVersion,
            null,
            "password");

        MfaEnrollmentResult started = Assert.IsType<MfaEnrollmentResult>(
            await enrollment.BeginEnrollmentAsync(
                user.Id,
                oldSessionId,
                user.AuthorizationVersion,
                password,
                restrictedRecovery: false));
        byte[] secret = (await context.MfaAuthenticators.SingleAsync(
            value => value.Id == started.AuthenticatorId)).ProtectedSecret;
        MfaVerificationResult verified = Assert.IsType<MfaVerificationResult>(
            await enrollment.VerifyEnrollmentAsync(
                user.Id,
                oldSessionId,
                user.AuthorizationVersion,
                restrictedRecovery: false,
                started.AuthenticatorId,
                TotpCode(secret, DateTimeOffset.UtcNow)));

        Assert.NotNull(verified.SessionId);
        Assert.NotEqual(oldSessionId, verified.SessionId);
        Assert.NotNull((await context.ServerSessions
            .AsNoTracking()
            .SingleAsync(value => value.Id == oldSessionId)).RevokedAtUtc);
        ServerSession elevated = await context.ServerSessions.SingleAsync(
            value => value.Id == verified.SessionId);
        Assert.Equal("password,totp", elevated.AuthenticationMethods);
        Assert.NotNull(elevated.MfaVerifiedAtUtc);
    }

    [Fact]
    public async Task MfaEnrollmentRollsBackWhenMandatoryAuditFails()
    {
        await using ServiceProvider services =
            CreateSecurityServiceProvider(throwOnAudit: true);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IMfaEnrollmentService enrollment =
            scope.ServiceProvider.GetRequiredService<IMfaEnrollmentService>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationUser user = CreateUser(now);
        const string password = "Rollback Audit Password 2026!";
        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        var oldSession = CreateSession(user.Id, now, now);
        context.Users.Add(user);
        context.ServerSessions.Add(oldSession);
        await context.SaveChangesAsync();

        MfaEnrollmentResult started = Assert.IsType<MfaEnrollmentResult>(
            await enrollment.BeginEnrollmentAsync(
                user.Id,
                oldSession.Id,
                user.AuthorizationVersion,
                password,
                restrictedRecovery: false));
        byte[] secret = (await context.MfaAuthenticators.SingleAsync(
            value => value.Id == started.AuthenticatorId)).ProtectedSecret;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enrollment.VerifyEnrollmentAsync(
                user.Id,
                oldSession.Id,
                user.AuthorizationVersion,
                restrictedRecovery: false,
                started.AuthenticatorId,
                TotpCode(secret, DateTimeOffset.UtcNow)));

        context.ChangeTracker.Clear();
        Assert.Equal(
            MfaState.NotEnrolled,
            (await context.Users.SingleAsync(value => value.Id == user.Id)).MfaState);
        Assert.Null((await context.ServerSessions.SingleAsync(
            value => value.Id == oldSession.Id)).RevokedAtUtc);
        Assert.Empty(await context.MfaRecoveryCodes
            .Where(value => value.UserId == user.Id)
            .ToArrayAsync());
    }

    [Fact]
    public async Task OrdinaryRecoveryAtomicallyInvalidatesPreviousMfaAndSessions()
    {
        await using (ApplicationDbContext migration = await CreateMigratedContextAsync())
        {
        }

        await using ServiceProvider services = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IAccountLifecycleService lifecycle =
            scope.ServiceProvider.GetRequiredService<IAccountLifecycleService>();
        IAnonymousPreSessionStore preSessions =
            scope.ServiceProvider.GetRequiredService<IAnonymousPreSessionStore>();
        IAuthenticationWorkflowService authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationWorkflowService>();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        AccountCreationResult account = await lifecycle.CreateAsync(
            $"ordinary-recovery-{Guid.NewGuid():N}",
            null,
            protectedOwner: false);
        OneTimeChallengeResult activation = await lifecycle.IssueChallengeAsync(
            account.UserId,
            "activation");
        Guid activationPreSession =
            await preSessions.CreateAsync(TimeSpan.FromMinutes(5));
        const string password = "A sufficiently long ordinary recovery password!";
        Assert.True(await lifecycle.CompleteChallengeAsync(
            account.Username,
            "activation",
            activation.Code,
            password,
            activationPreSession));
        Assert.True(await lifecycle.TransitionAsync(account.UserId, "enable"));
        Guid loginPreSession =
            await preSessions.CreateAsync(TimeSpan.FromMinutes(5));
        LoginWorkflowResult login = await authentication.LoginAsync(
            account.Username,
            password,
            loginPreSession);

        ApplicationUser user = await context.Users.SingleAsync(
            value => value.Id == account.UserId);
        long previousVersion = user.AuthorizationVersion;
        user.MfaState = MfaState.Active;
        var authenticator = new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProtectedSecret = [1, 2, 3],
            CreatedAtUtc = DateTimeOffset.UtcNow,
            VerifiedAtUtc = DateTimeOffset.UtcNow,
        };
        byte[] rawCode = Enumerable.Repeat((byte)7, 16).ToArray();
        string recoveryCode = Convert.ToBase64String(rawCode)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        context.MfaAuthenticators.Add(authenticator);
        context.MfaRecoveryCodes.AddRange(
            new MfaRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                KeyVersion = 1,
                KeyedHash = HMACSHA256.HashData(new byte[32], rawCode),
                CreatedAtUtc = DateTimeOffset.UtcNow,
            },
            new MfaRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                KeyVersion = 1,
                KeyedHash = new byte[32],
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        await context.SaveChangesAsync();

        Guid recoveryPreSession =
            await preSessions.CreateAsync(TimeSpan.FromMinutes(5));
        RecoveryWorkflowResult? recovery = await authentication.BeginRecoveryAsync(
            account.Username,
            password,
            recoveryCode,
            recoveryPreSession);

        Assert.NotNull(recovery);
        await context.Entry(user).ReloadAsync();
        Assert.Equal(MfaState.RecoveryPending, user.MfaState);
        Assert.Equal(previousVersion + 1, user.AuthorizationVersion);
        Assert.False(await context.ServerSessions.AnyAsync(
            value => value.Id == login.SessionId && value.RevokedAtUtc == null));
        Assert.False(await context.MfaAuthenticators.AnyAsync(
            value => value.Id == authenticator.Id && value.RevokedAtUtc == null));
        Assert.False(await context.MfaRecoveryCodes.AnyAsync(
            value => value.UserId == user.Id && value.ConsumedAtUtc == null));
        Assert.Single(await context.RestrictedRecoverySessions
            .Where(value => value.UserId == user.Id && value.RevokedAtUtc == null)
            .ToArrayAsync());
    }

    [Fact]
    public async Task AdministrativeMfaRecoveryRevokesExistingSecurityStateAndCreatesRestrictedSession()
    {
        await using (ApplicationDbContext migration = await CreateMigratedContextAsync())
        {
        }

        await using ServiceProvider services = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IAccountLifecycleService lifecycle =
            scope.ServiceProvider.GetRequiredService<IAccountLifecycleService>();
        IAnonymousPreSessionStore preSessions =
            scope.ServiceProvider.GetRequiredService<IAnonymousPreSessionStore>();
        IAuthenticationWorkflowService authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationWorkflowService>();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        AccountCreationResult account = await lifecycle.CreateAsync(
            $"recovery-{Guid.NewGuid():N}",
            null,
            protectedOwner: false);
        OneTimeChallengeResult activation = await lifecycle.IssueChallengeAsync(
            account.UserId,
            "activation");
        Guid activationPreSession = await preSessions.CreateAsync(
            TimeSpan.FromMinutes(5));
        Assert.True(await lifecycle.CompleteChallengeAsync(
            account.Username,
            "activation",
            activation.Code,
            "A sufficiently long recovery password 2026!",
            activationPreSession));
        Assert.True(await lifecycle.TransitionAsync(account.UserId, "enable"));

        ApplicationUser user = await context.Users.SingleAsync(
            value => value.Id == account.UserId);
        user.MfaState = MfaState.Active;
        var authenticator = new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProtectedSecret = [1, 2, 3],
            CreatedAtUtc = DateTimeOffset.UtcNow,
            VerifiedAtUtc = DateTimeOffset.UtcNow,
        };
        context.MfaAuthenticators.Add(authenticator);
        await context.SaveChangesAsync();

        OneTimeChallengeResult? recovery =
            await lifecycle.StartMfaRecoveryAsync(user.Id);
        Assert.NotNull(recovery);
        Assert.NotNull((await context.MfaAuthenticators
            .AsNoTracking()
            .SingleAsync(value => value.Id == authenticator.Id)).RevokedAtUtc);

        Guid recoveryPreSession = await preSessions.CreateAsync(
            TimeSpan.FromMinutes(5));
        RecoveryWorkflowResult? result = await authentication.BeginRecoveryAsync(
            account.Username,
            "A sufficiently long recovery password 2026!",
            recovery!.Code,
            recoveryPreSession);

        Assert.NotNull(result);
        Assert.True(await context.RestrictedRecoverySessions.AnyAsync(
            value =>
                value.Id == result!.RecoverySessionId
                && value.UserId == user.Id
                && value.RevokedAtUtc == null));
        Assert.Empty(await context.ServerSessions
            .Where(value => value.UserId == user.Id && value.RevokedAtUtc == null)
            .ToArrayAsync());
    }

    [Fact]
    public async Task BootstrapOwnerCanBeEnabledOnlyThroughTrustedPreparationFlow()
    {
        await using (ApplicationDbContext migration = await CreateMigratedContextAsync())
        {
        }

        await using ServiceProvider services = CreateSecurityServiceProvider();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        BootstrapIdentityPreparationService bootstrap =
            scope.ServiceProvider
                .GetRequiredService<BootstrapIdentityPreparationService>();
        IAccountLifecycleService lifecycle =
            scope.ServiceProvider.GetRequiredService<IAccountLifecycleService>();
        IAnonymousPreSessionStore preSessions =
            scope.ServiceProvider.GetRequiredService<IAnonymousPreSessionStore>();
        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        OneTimeChallengeResult activation = await bootstrap.CreateOwnerAsync(
            $"owner-{Guid.NewGuid():N}",
            null);
        ApplicationUser owner = await context.Users.SingleAsync(
            value => value.Id == activation.UserId);
        Guid preSession = await preSessions.CreateAsync(TimeSpan.FromMinutes(5));
        Assert.True(await lifecycle.CompleteChallengeAsync(
            owner.UserName!,
            "activation",
            activation.Code,
            "A sufficiently long owner password 2026!",
            preSession));
        Assert.False(await lifecycle.TransitionAsync(owner.Id, "enable"));
        Assert.True(await bootstrap.EnablePreparedOwnerAsync(owner.Id));
        Assert.False(await bootstrap.MarkReadyForPrivilegeGrantAsync(owner.Id));

        owner.MfaState = MfaState.Active;
        context.MfaAuthenticators.Add(
            new MfaAuthenticator
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                ProtectedSecret = new byte[20],
                CreatedAtUtc = DateTimeOffset.UtcNow,
                VerifiedAtUtc = DateTimeOffset.UtcNow,
            });
        await context.SaveChangesAsync();
        Assert.True(await bootstrap.MarkReadyForPrivilegeGrantAsync(owner.Id));
        Assert.Equal(
            BootstrapState.ReadyForPrivilegeGrant,
            (await context.BootstrapProgress.AsNoTracking().SingleAsync()).State);
        long versionBeforeGrant = owner.AuthorizationVersion;
        Assert.True(await bootstrap.CompletePrivilegeGrantAsync(owner.Id));
        Assert.False(await bootstrap.CompletePrivilegeGrantAsync(owner.Id));
        await context.Entry(owner).ReloadAsync();
        Assert.Equal(versionBeforeGrant + 1, owner.AuthorizationVersion);
        Assert.True(await context.UserRoles.AnyAsync(value =>
            value.UserId == owner.Id
            && value.RoleId == SystemRoleIds.SystemAdministrator));
        Assert.Equal(
            BootstrapState.Completed,
            (await context.BootstrapProgress.AsNoTracking().SingleAsync()).State);
        await context.UserRoles
            .Where(value => value.UserId == owner.Id)
            .ExecuteDeleteAsync();
        await context.BootstrapProgress.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(
                    value => value.State,
                    BootstrapState.NotStarted)
                .SetProperty(
                    value => value.ProtectedOwnerUserId,
                    (Guid?)null));
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
                .MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    DatabaseSchemas.Infrastructure));
        return new ApplicationDbContext(options.Options);
    }

    private ServiceProvider CreateSecurityServiceProvider(
        bool throwOnAudit = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                database.ConnectionString,
                npgsql => npgsql
                    .MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName)
                    .MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        DatabaseSchemas.Infrastructure)));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 15;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddUserValidator<OptionalUniqueEmailUserValidator>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<ISecurityKeyProvider, TestSecurityKeyProvider>();
        services.AddSingleton<IMfaSecretProtector, TestMfaSecretProtector>();
        if (throwOnAudit)
        {
            services.AddScoped<ISecurityAuditWriter, ThrowingSecurityAuditWriter>();
        }
        else
        {
            services.AddScoped<ISecurityAuditWriter, SecurityAuditWriter>();
        }
        services.AddScoped<IAnonymousPreSessionStore, AnonymousPreSessionStore>();
        services.AddScoped<ISessionRotationService, SessionRotationService>();
        services.AddScoped<ISessionValidator, ServerSessionValidator>();
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
        services.AddScoped<
            ISecurityStateRevocationService,
            SecurityStateRevocationService>();
        services.AddScoped<AtomicSecurityStateStore>();
        services.AddScoped<BootstrapStateStore>();
        services.AddScoped<BootstrapIdentityPreparationService>();
        services.AddScoped<IAccountLifecycleService, AccountLifecycleService>();
        services.AddScoped<IMfaEnrollmentService, MfaEnrollmentService>();
        services.AddScoped<
            IAuthenticationWorkflowService,
            AuthenticationWorkflowService>();
        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserName = $"phase04a-{Guid.NewGuid():N}",
            NormalizedUserName = $"PHASE04A-{Guid.NewGuid():N}",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now,
            AccountStatus = AccountStatus.Enabled,
            CredentialStatus = CredentialStatus.Active,
        };

    private static ServerSession CreateSession(
        Guid userId,
        DateTimeOffset createdAt,
        DateTimeOffset lastActivityAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AuthorizationVersion = 0,
            CreatedAtUtc = createdAt,
            LastActivityAtUtc = lastActivityAt,
            AbsoluteExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            AuthenticationMethods = "password",
        };

    private static MfaLoginChallenge CreateMfaLoginChallenge(
        Guid userId,
        Guid preSessionId,
        Guid authenticatorId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AnonymousPreSessionId = preSessionId,
            AuthorizationVersionAtIssue = 0,
            AuthenticatorId = authenticatorId,
            ExpiresAtUtc = now.AddMinutes(5),
        };

    private static string TotpCode(byte[] secret, DateTimeOffset now)
    {
        long step = now.ToUnixTimeSeconds() / 30;
        byte[] counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

#pragma warning disable CA5350 // TOTP interoperability requires HMAC-SHA1 (RFC 6238).
        byte[] hash = HMACSHA1.HashData(secret, counter);
#pragma warning restore CA5350
        int offset = hash[^1] & 0x0f;
        int binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString(
            "D6",
            CultureInfo.InvariantCulture);
    }

    private sealed class TestSecurityKeyProvider : ISecurityKeyProvider
    {
        private static readonly VersionedSecurityKey Key =
            new(1, new byte[32]);

        public ValueTask<VersionedSecurityKey> GetCurrentKeyAsync(
            string purpose,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Key);

        public ValueTask<VersionedSecurityKey?> GetKeyAsync(
            string purpose,
            int version,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<VersionedSecurityKey?>(
                version == Key.Version ? Key : null);
    }

    private sealed class ThrowingSecurityAuditWriter : ISecurityAuditWriter
    {
        public Task WriteAsync(
            SecurityAuditEntry entry,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated mandatory audit failure.");
    }

    private sealed class TestMfaSecretProtector : IMfaSecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> secret) => secret.ToArray();

        public byte[] Unprotect(ReadOnlySpan<byte> protectedSecret) =>
            protectedSecret.ToArray();
    }

    private static SecurityChallenge CreateChallenge(
        Guid userId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = SecurityChallengePurpose.Activation,
            KeyedHash = [1, 2, 3],
            KeyVersion = 1,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(30),
        };
}
