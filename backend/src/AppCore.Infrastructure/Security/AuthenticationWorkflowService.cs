using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class AuthenticationWorkflowService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IAnonymousPreSessionStore preSessions,
    ISessionRotationService sessionRotation,
    IMfaSecretProtector secretProtector,
    ISecurityKeyProvider securityKeys,
    ISecurityAuditWriter auditWriter,
    AtomicSecurityStateStore atomicState,
    ISessionValidator sessionValidator,
    IPasswordPolicyService passwordPolicy,
    ISecurityStateRevocationService securityState,
    TimeProvider timeProvider)
    : IAuthenticationWorkflowService
{
    private static readonly ApplicationUser DummyUser = new()
    {
        Id = Guid.Empty,
        UserName = "authentication-timing-placeholder",
    };

    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(
            DummyUser,
            "not-a-real-password-value");

    public async Task<LoginWorkflowResult> LoginAsync(
        string username,
        string password,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool validPreSession = await context.AnonymousPreSessions.AnyAsync(
            value =>
                value.Id == anonymousPreSessionId
                && value.ConsumedAtUtc == null
                && value.ExpiresAtUtc > now,
            cancellationToken);
        if (!validPreSession)
        {
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        string normalizedName = userManager.NormalizeName(username.Trim());
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.NormalizedUserName == normalizedName,
            cancellationToken);
        string normalizedPassword = NormalizePassword(password);
        bool passwordValid;
        if (user is null)
        {
            _ = userManager.PasswordHasher.VerifyHashedPassword(
                DummyUser,
                DummyPasswordHash,
                normalizedPassword);
            passwordValid = false;
        }
        else
        {
            passwordValid = await userManager.CheckPasswordAsync(
                user,
                normalizedPassword);
        }
        bool accountValid = user is not null
            && user.AccountStatus == AccountStatus.Enabled
            && user.CredentialStatus == CredentialStatus.Active
            && (user.TemporarilyThrottledUntilUtc is null
                || user.TemporarilyThrottledUntilUtc <= now);

        if (!passwordValid || !accountValid)
        {
            TimeSpan minimumDelay = TimeSpan.FromMilliseconds(500);
            if (user is not null)
            {
                minimumDelay = await RecordFailedLoginAsync(
                    user,
                    now,
                    cancellationToken);
            }

            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.LoginFailed,
                    "invalid"),
                cancellationToken);
            await DelayUntilMinimumDurationAsync(
                startedAt,
                minimumDelay,
                cancellationToken);
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        bool throttleEnded = user!.TemporarilyThrottledUntilUtc is not null;
        user.AccessFailedCount = 0;
        user.FailedLoginWindowStartedAtUtc = null;
        user.TemporarilyThrottledUntilUtc = null;
        await context.SaveChangesAsync(cancellationToken);
        if (throttleEnded)
        {
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.LoginThrottleEnded,
                    "success",
                    user.Id,
                    user.Id),
                cancellationToken);
        }

        if (user.MfaState == MfaState.RecoveryPending)
        {
            return new LoginWorkflowResult(
                LoginWorkflowStatus.RecoveryRequired);
        }

        if (user.MfaState == MfaState.Active)
        {
            Guid authenticatorId = await context.MfaAuthenticators
                .Where(value =>
                    value.UserId == user.Id
                    && value.VerifiedAtUtc != null
                    && value.RevokedAtUtc == null)
                .Select(value => value.Id)
                .SingleAsync(cancellationToken);
            var challenge = new MfaLoginChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AnonymousPreSessionId = anonymousPreSessionId,
                AuthorizationVersionAtIssue = user.AuthorizationVersion,
                AuthenticatorId = authenticatorId,
                ExpiresAtUtc = now.AddMinutes(5),
            };
            await atomicState.ReplaceMfaLoginChallengeAsync(
                challenge,
                cancellationToken);
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.MfaChallengeIssued,
                    "success",
                    TargetUserId: user.Id),
                cancellationToken);
            return new LoginWorkflowResult(
                LoginWorkflowStatus.MfaRequired,
                user.Id,
                AuthorizationVersion: user.AuthorizationVersion,
                MfaChallengeId: challenge.Id);
        }

        await using var passwordLoginTransaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        if (!await preSessions.ConsumeAsync(
                anonymousPreSessionId,
                cancellationToken))
        {
            await passwordLoginTransaction.RollbackAsync(cancellationToken);
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        Guid sessionId = await sessionRotation.RotateAsync(
            user.Id,
            null,
            user.AuthorizationVersion,
            null,
            "password",
            cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.LoginSucceeded,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        await passwordLoginTransaction.CommitAsync(cancellationToken);
        return new LoginWorkflowResult(
            LoginWorkflowStatus.Authenticated,
            user.Id,
            sessionId,
            user.AuthorizationVersion);
    }

    public async Task<LoginWorkflowResult> CompleteMfaLoginAsync(
        Guid challengeId,
        Guid anonymousPreSessionId,
        string code,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        MfaLoginChallenge? challenge = await context.MfaLoginChallenges
            .SingleOrDefaultAsync(value => value.Id == challengeId, cancellationToken);
        if (challenge is null
            || challenge.AnonymousPreSessionId != anonymousPreSessionId
            || challenge.ConsumedAtUtc is not null
            || challenge.InvalidatedAtUtc is not null
            || challenge.ExpiresAtUtc <= now
            || challenge.AttemptCount >= 5)
        {
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({challenge.UserId.ToString()}, 0));",
            cancellationToken);
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == challenge.UserId,
            cancellationToken);
        MfaAuthenticator? authenticator = await context.MfaAuthenticators
            .SingleOrDefaultAsync(
                value =>
                    value.Id == challenge.AuthenticatorId
                    && value.UserId == challenge.UserId
                    && value.VerifiedAtUtc != null
                    && value.RevokedAtUtc == null,
                cancellationToken);
        bool securityStateValid = user is not null
            && user.AccountStatus == AccountStatus.Enabled
            && user.CredentialStatus == CredentialStatus.Active
            && user.MfaState == MfaState.Active
            && user.AuthorizationVersion == challenge.AuthorizationVersionAtIssue;
        long? acceptedStep = !securityStateValid || authenticator is null
            ? null
            : FindAcceptedTotpStep(
                secretProtector.Unprotect(authenticator.ProtectedSecret),
                code,
                now);
        if (authenticator is null
            || acceptedStep is null
            || !await atomicState.AdvanceTotpStepAsync(
                authenticator.Id,
                acceptedStep.Value,
                cancellationToken))
        {
            await atomicState.IncrementLoginChallengeAttemptAsync(
                challengeId,
                cancellationToken);
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.MfaChallengeFailed,
                    "invalid",
                    TargetUserId: challenge.UserId),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        if (!await atomicState.ConsumeLoginChallengeAsync(
                challengeId,
                cancellationToken)
            || !await preSessions.ConsumeAsync(
                anonymousPreSessionId,
                cancellationToken))
        {
            return new LoginWorkflowResult(LoginWorkflowStatus.Invalid);
        }

        Guid sessionId = await sessionRotation.RotateAsync(
            user!.Id,
            null,
            user.AuthorizationVersion,
            now,
            "password,totp",
            cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.LoginSucceeded,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginWorkflowResult(
            LoginWorkflowStatus.Authenticated,
            user.Id,
            sessionId,
            user.AuthorizationVersion);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        Guid sessionId,
        long authorizationVersion,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        string? normalized = user is null
            ? null
            : await passwordPolicy.NormalizeAndValidateAsync(
                user.Id,
                newPassword,
                cancellationToken);
        if (user is null
            || !await userManager.CheckPasswordAsync(
                user,
                NormalizePassword(currentPassword))
            || normalized is null)
        {
            return false;
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        if (!await sessionValidator.RecheckAsync(
                sessionId,
                authorizationVersion,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        await SavePasswordHistoryAsync(user, cancellationToken);
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, normalized);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AuthorizationVersion++;
        await securityState.RevokeAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.PasswordChanged,
                "success",
                user.Id,
                user.Id),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task LogoutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.ServerSessions
            .Where(value => value.Id == sessionId && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(SecurityAuditCodes.Logout, "success"),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CurrentUserResult?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        string[] permissions = await (
            from assignment in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join permission in context.RolePermissions.AsNoTracking() on role.Id equals permission.RoleId
            where assignment.UserId == userId && !role.IsArchived
            select permission.PermissionId)
            .Distinct()
            .OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        return new CurrentUserResult(
            user.Id,
            user.UserName!,
            user.Email,
            user.AccountStatus.ToString(),
            user.MfaState.ToString(),
            permissions);
    }

    public async Task<RecoveryWorkflowResult?> BeginRecoveryAsync(
        string username,
        string password,
        string recoveryCode,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default)
    {
        long startedAt = Stopwatch.GetTimestamp();
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool validPreSession = await context.AnonymousPreSessions.AnyAsync(
            value =>
                value.Id == anonymousPreSessionId
                && value.ConsumedAtUtc == null
                && value.ExpiresAtUtc > now,
            cancellationToken);
        string normalizedName = userManager.NormalizeName(username.Trim());
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.NormalizedUserName == normalizedName,
            cancellationToken);
        string normalizedPassword = NormalizePassword(password);
        bool passwordValid = user is null
            ? userManager.PasswordHasher.VerifyHashedPassword(
                DummyUser,
                DummyPasswordHash,
                normalizedPassword) == PasswordVerificationResult.Success
            : await userManager.CheckPasswordAsync(user, normalizedPassword);
        if (!validPreSession
            || user is null
            || user.AccountStatus != AccountStatus.Enabled
            || user.CredentialStatus != CredentialStatus.Active
            || !passwordValid
            || !TryDecodeBase64Url(recoveryCode, out byte[] raw))
        {
            await DelayUntilMinimumDurationAsync(
                startedAt,
                TimeSpan.FromMilliseconds(500),
                cancellationToken);
            return null;
        }

        MfaRecoveryCode[] candidates = await context.MfaRecoveryCodes
            .Where(value =>
                value.UserId == user.Id && value.ConsumedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        MfaRecoveryCode? matched = null;
        foreach (MfaRecoveryCode candidate in candidates)
        {
            VersionedSecurityKey? key = await securityKeys.GetKeyAsync(
                "challenge-hmac",
                candidate.KeyVersion,
                cancellationToken);
            if (key is not null
                && CryptographicOperations.FixedTimeEquals(
                    candidate.KeyedHash,
                    HMACSHA256.HashData(key.Key.Span, raw)))
            {
                matched = candidate;
                break;
            }
        }

        SecurityChallenge? administrativeChallenge = null;
        if (matched is null && user.MfaState == MfaState.RecoveryPending)
        {
            administrativeChallenge = await context.SecurityChallenges
                .SingleOrDefaultAsync(
                    value =>
                        value.UserId == user.Id
                        && value.Purpose
                            == SecurityChallengePurpose.AdministrativeMfaRecovery
                        && value.ConsumedAtUtc == null
                        && value.InvalidatedAtUtc == null
                        && value.AttemptCount < 5
                        && value.ExpiresAtUtc > now,
                    cancellationToken);
            if (administrativeChallenge is not null)
            {
                VersionedSecurityKey? key = await securityKeys.GetKeyAsync(
                    "challenge-hmac",
                    administrativeChallenge.KeyVersion,
                    cancellationToken);
                if (key is null
                    || !CryptographicOperations.FixedTimeEquals(
                        administrativeChallenge.KeyedHash,
                        HMACSHA256.HashData(key.Key.Span, raw)))
                {
                    await atomicState.IncrementSecurityChallengeAttemptAsync(
                        administrativeChallenge.Id,
                        cancellationToken);
                    administrativeChallenge = null;
                }
            }
        }

        if (matched is null && administrativeChallenge is null)
        {
            return null;
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 0));",
            cancellationToken);
        bool credentialConsumed = matched is not null
            ? await atomicState.ConsumeRecoveryCodeAsync(
                matched.Id,
                cancellationToken)
            : await atomicState.ConsumeSecurityChallengeAsync(
                administrativeChallenge!.Id,
                cancellationToken);
        if (!credentialConsumed
            || !await preSessions.ConsumeAsync(
                anonymousPreSessionId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var recoverySession = new RestrictedRecoverySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(15),
        };
        user.MfaState = MfaState.RecoveryPending;
        user.AuthorizationVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.MfaAuthenticators
            .Where(value =>
                value.UserId == user.Id && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await context.MfaRecoveryCodes
            .Where(value =>
                value.UserId == user.Id && value.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.ConsumedAtUtc, now),
                cancellationToken);
        await context.ServerSessions
            .Where(value => value.UserId == user.Id && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await context.MfaLoginChallenges
            .Where(value =>
                value.UserId == user.Id
                && value.ConsumedAtUtc == null
                && value.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.InvalidatedAtUtc,
                    now),
                cancellationToken);
        await context.RestrictedRecoverySessions
            .Where(value =>
                value.UserId == user.Id && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        context.RestrictedRecoverySessions.Add(recoverySession);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.MfaRecoverySessionCreated,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecoveryWorkflowResult(user.Id, recoverySession.Id);
    }

    public async Task LogoutRecoveryAsync(
        Guid recoverySessionId,
        CancellationToken cancellationToken = default)
    {
        await context.RestrictedRecoverySessions
            .Where(value =>
                value.Id == recoverySessionId
                && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.RevokedAtUtc,
                    timeProvider.GetUtcNow()),
                cancellationToken);
    }

    private async Task<TimeSpan> RecordFailedLoginAsync(
        ApplicationUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 0));",
            cancellationToken);
        await context.Entry(user).ReloadAsync(cancellationToken);

        if (user.FailedLoginWindowStartedAtUtc is null
            || user.FailedLoginWindowStartedAtUtc < now.AddMinutes(-15))
        {
            user.FailedLoginWindowStartedAtUtc = now;
            user.AccessFailedCount = 1;
        }
        else
        {
            user.AccessFailedCount++;
        }

        int failureNumber = user.AccessFailedCount;
        if (failureNumber >= 5)
        {
            user.TemporarilyThrottledUntilUtc = now.AddMinutes(15);
            user.AccessFailedCount = 0;
            user.FailedLoginWindowStartedAtUtc = null;
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.LoginThrottleStarted,
                    "success",
                    TargetUserId: user.Id),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TimeSpan.FromMilliseconds(
            Math.Min(1_500, 500 + ((failureNumber - 1) * 250)));
    }

    private async Task SavePasswordHistoryAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (user.PasswordHash is null)
        {
            return;
        }

        context.PasswordHistory.Add(
            new PasswordHistoryEntry
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash,
                CreatedAtUtc = timeProvider.GetUtcNow(),
            });
        PasswordHistoryEntry[] stale = await context.PasswordHistory
            .Where(value => value.UserId == user.Id)
            .OrderByDescending(value => value.CreatedAtUtc)
            .Skip(5)
            .ToArrayAsync(cancellationToken);
        context.PasswordHistory.RemoveRange(stale);
    }

    internal static string NormalizePassword(string value) =>
        value.Normalize(NormalizationForm.FormC);

    private static async Task DelayUntilMinimumDurationAsync(
        long startedAt,
        TimeSpan minimum,
        CancellationToken cancellationToken)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
        if (elapsed < minimum)
        {
            await Task.Delay(minimum - elapsed, cancellationToken);
        }
    }

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        try
        {
            string padded = value.Replace('-', '+').Replace('_', '/')
                + new string('=', (4 - value.Length % 4) % 4);
            bytes = Convert.FromBase64String(padded);
            return value.Length == 22 && bytes.Length == 16;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    internal static long? FindAcceptedTotpStep(
        byte[] secret,
        string code,
        DateTimeOffset now)
    {
        if (code.Length != 6
            || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        long currentStep = now.ToUnixTimeSeconds() / 30;
        foreach (long step in new[] { currentStep, currentStep - 1, currentStep + 1 })
        {
            string expected = GenerateTotp(secret, step);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }

        return null;
    }

    private static string GenerateTotp(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
#pragma warning disable CA5350 // RFC 6238 interoperability requires HMAC-SHA-1.
        byte[] hash = HMACSHA1.HashData(secret, counter);
#pragma warning restore CA5350
        int offset = hash[^1] & 0x0f;
        int binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
