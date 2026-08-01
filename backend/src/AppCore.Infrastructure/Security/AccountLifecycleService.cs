using System.Security.Cryptography;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppCore.Infrastructure.Security;

public sealed class AccountLifecycleService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ISecurityKeyProvider securityKeys,
    AtomicSecurityStateStore atomicState,
    IAnonymousPreSessionStore preSessions,
    ISecurityAuditWriter auditWriter,
    IPasswordPolicyService passwordPolicy,
    ISecurityStateRevocationService securityState,
    TimeProvider timeProvider)
    : IAccountLifecycleService
{
    public async Task<AccountCreationResult> CreateAsync(
        string username,
        string? email,
        bool protectedOwner,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction =
            context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            AccountStatus = AccountStatus.Disabled,
            CredentialStatus = CredentialStatus.ActivationPending,
            MfaState = MfaState.NotEnrolled,
            CreatedAtUtc = now,
            IsProtectedOwner = protectedOwner,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        IdentityResult result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(",", result.Errors.Select(value => value.Code)));
        }

        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.AccountCreated,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return new AccountCreationResult(user.Id, user.UserName);
    }

    public async Task<OneTimeChallengeResult> IssueChallengeAsync(
        Guid userId,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction =
            context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
        SecurityChallengePurpose parsedPurpose = ParsePurpose(purpose);
        ApplicationUser user = await context.Users.SingleAsync(
            value => value.Id == userId,
            cancellationToken);
        bool allowed = user.AccountStatus != AccountStatus.Archived
            && ((parsedPurpose == SecurityChallengePurpose.Activation
                    && user.CredentialStatus == CredentialStatus.ActivationPending)
                || (parsedPurpose == SecurityChallengePurpose.PasswordReset
                    && user.CredentialStatus == CredentialStatus.ResetPending)
                || parsedPurpose
                    == SecurityChallengePurpose.AdministrativeMfaRecovery);
        if (!allowed)
        {
            throw new InvalidOperationException("Invalid account lifecycle state.");
        }

        byte[] raw = RandomNumberGenerator.GetBytes(16);
        string code = EncodeBase64Url(raw);
        VersionedSecurityKey key = await securityKeys.GetCurrentKeyAsync(
            "challenge-hmac",
            cancellationToken);
        byte[] hash = HMACSHA256.HashData(key.Key.Span, raw);
        DateTimeOffset now = timeProvider.GetUtcNow();
        var challenge = new SecurityChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = parsedPurpose,
            KeyedHash = hash,
            KeyVersion = key.Version,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(30),
        };
        await atomicState.ReplaceSecurityChallengeAsync(
            challenge,
            cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.ChallengeIssued,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return new OneTimeChallengeResult(user.Id, code, challenge.ExpiresAtUtc);
    }

    public async Task<bool> CompleteChallengeAsync(
        string username,
        string purpose,
        string code,
        string newPassword,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default)
    {
        SecurityChallengePurpose parsedPurpose = ParsePurpose(purpose);
        string normalizedName = userManager.NormalizeName(username.Trim());
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.NormalizedUserName == normalizedName,
            cancellationToken);
        if (user is null
            || user.AccountStatus == AccountStatus.Archived
            || !IsExpectedCredentialState(user, parsedPurpose)
            || !TryDecodeCode(code, out byte[] raw)
            || await passwordPolicy.NormalizeAndValidateAsync(
                user.Id,
                newPassword,
                cancellationToken) is null)
        {
            return false;
        }

        SecurityChallenge? challenge = await context.SecurityChallenges
            .Where(value =>
                value.UserId == user.Id
                && value.Purpose == parsedPurpose
                && value.ConsumedAtUtc == null
                && value.InvalidatedAtUtc == null)
            .SingleOrDefaultAsync(cancellationToken);
        if (challenge is null)
        {
            return false;
        }

        VersionedSecurityKey? key = await securityKeys.GetKeyAsync(
            "challenge-hmac",
            challenge.KeyVersion,
            cancellationToken);
        bool matches = key is not null
            && CryptographicOperations.FixedTimeEquals(
                challenge.KeyedHash,
                HMACSHA256.HashData(key.Key.Span, raw));
        if (!matches)
        {
            await atomicState.IncrementSecurityChallengeAttemptAsync(
                challenge.Id,
                cancellationToken);
            return false;
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 0));",
            cancellationToken);
        if (!await atomicState.ConsumeSecurityChallengeAsync(
                challenge.Id,
                cancellationToken)
            || !await preSessions.ConsumeAsync(
                anonymousPreSessionId,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        string normalizedPassword =
            (await passwordPolicy.NormalizeAndValidateAsync(
                user.Id,
                newPassword,
                cancellationToken))!;
        if (user.PasswordHash is not null)
        {
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

        user.PasswordHash =
            userManager.PasswordHasher.HashPassword(user, normalizedPassword);
        user.CredentialStatus = CredentialStatus.Active;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AuthorizationVersion++;
        await securityState.RevokeAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                parsedPurpose == SecurityChallengePurpose.Activation
                    ? SecurityAuditCodes.ActivationCompleted
                    : SecurityAuditCodes.PasswordResetCompleted,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TransitionAsync(
        Guid userId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        if (user is null || user.IsProtectedOwner)
        {
            return false;
        }

        bool revokeSessions = false;
        bool valid = operation switch
        {
            "enable" when user.AccountStatus == AccountStatus.Disabled
                && user.CredentialStatus == CredentialStatus.Active =>
                SetAccountStatus(user, AccountStatus.Enabled),
            "disable" when user.AccountStatus == AccountStatus.Enabled =>
                SetAccountStatus(user, AccountStatus.Disabled),
            "suspend" when user.AccountStatus == AccountStatus.Enabled =>
                SetAccountStatus(user, AccountStatus.Suspended),
            "restore" when user.AccountStatus is AccountStatus.Suspended
                or AccountStatus.Archived =>
                SetAccountStatus(user, AccountStatus.Disabled),
            "archive" when user.AccountStatus != AccountStatus.Archived =>
                SetAccountStatus(user, AccountStatus.Archived),
            "startReset" when user.AccountStatus != AccountStatus.Archived
                && user.CredentialStatus == CredentialStatus.Active =>
                SetCredentialStatus(user, CredentialStatus.ResetPending),
            _ => false,
        };
        if (!valid)
        {
            return false;
        }

        revokeSessions = operation is
            "disable" or "suspend" or "archive" or "restore" or "startReset";
        user.AuthorizationVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        if (revokeSessions)
        {
            await securityState.RevokeAsync(user.Id, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.AccountStateChanged,
                "success",
                TargetUserId: user.Id),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<OneTimeChallengeResult?> StartMfaRecoveryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        if (user is null
            || user.AccountStatus == AccountStatus.Archived
            || user.MfaState != MfaState.Active)
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        user.MfaState = MfaState.RecoveryPending;
        user.AuthorizationVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.MfaAuthenticators
            .Where(value => value.UserId == userId && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await context.MfaRecoveryCodes
            .Where(value => value.UserId == userId && value.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.ConsumedAtUtc, now),
                cancellationToken);
        await securityState.RevokeAsync(userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        byte[] raw = RandomNumberGenerator.GetBytes(16);
        VersionedSecurityKey key = await securityKeys.GetCurrentKeyAsync(
            "challenge-hmac",
            cancellationToken);
        var entity = new SecurityChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = SecurityChallengePurpose.AdministrativeMfaRecovery,
            KeyedHash = HMACSHA256.HashData(key.Key.Span, raw),
            KeyVersion = key.Version,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(15),
        };
        await atomicState.ReplaceSecurityChallengeAsync(entity, cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.MfaRecoveryStarted,
                "success",
                TargetUserId: userId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OneTimeChallengeResult(
            userId,
            EncodeBase64Url(raw),
            entity.ExpiresAtUtc);
    }

    private static bool SetAccountStatus(
        ApplicationUser user,
        AccountStatus status)
    {
        user.AccountStatus = status;
        return true;
    }

    private static bool SetCredentialStatus(
        ApplicationUser user,
        CredentialStatus status)
    {
        user.CredentialStatus = status;
        return true;
    }

    private static SecurityChallengePurpose ParsePurpose(string purpose) =>
        purpose switch
        {
            "activation" => SecurityChallengePurpose.Activation,
            "password-reset" => SecurityChallengePurpose.PasswordReset,
            "mfa-recovery" =>
                SecurityChallengePurpose.AdministrativeMfaRecovery,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
        };

    private static bool IsExpectedCredentialState(
        ApplicationUser user,
        SecurityChallengePurpose purpose) =>
        purpose switch
        {
            SecurityChallengePurpose.Activation =>
                user.CredentialStatus == CredentialStatus.ActivationPending,
            SecurityChallengePurpose.PasswordReset =>
                user.CredentialStatus == CredentialStatus.ResetPending,
            _ => false,
        };

    private static bool TryDecodeCode(string code, out byte[] bytes)
    {
        try
        {
            string padded = code.Replace('-', '+').Replace('_', '/')
                + new string('=', (4 - code.Length % 4) % 4);
            bytes = Convert.FromBase64String(padded);
            return code.Length == 22 && bytes.Length == 16;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static string EncodeBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

}
