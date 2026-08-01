using System.Security.Cryptography;
using System.Text;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class MfaEnrollmentService(
    ApplicationDbContext context,
    IMfaSecretProtector secretProtector,
    ISecurityKeyProvider securityKeys,
    AtomicSecurityStateStore atomicState,
    ISecurityAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager,
    ISessionValidator sessionValidator,
    ISessionRotationService sessionRotation,
    TimeProvider timeProvider)
    : IMfaEnrollmentService
{
    public async Task<MfaEnrollmentResult?> BeginEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        long? authorizationVersion,
        string? currentPassword,
        bool restrictedRecovery,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        if (user is null
            || user.AccountStatus == AccountStatus.Archived
            || user.MfaState == MfaState.Active
            || (!restrictedRecovery
                && (string.IsNullOrEmpty(currentPassword)
                    || !await userManager.CheckPasswordAsync(
                        user,
                        AuthenticationWorkflowService.NormalizePassword(
                            currentPassword)))))
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        bool sessionValid = restrictedRecovery
            ? await context.RestrictedRecoverySessions.AnyAsync(
                value =>
                    value.Id == sessionId
                    && value.UserId == userId
                    && value.RevokedAtUtc == null
                    && value.ExpiresAtUtc > now,
                cancellationToken)
            : authorizationVersion is not null
                && await sessionValidator.RecheckAsync(
                    sessionId,
                    authorizationVersion.Value,
                    cancellationToken);
        if (!sessionValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        await context.MfaAuthenticators
            .Where(value => value.UserId == userId && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.RevokedAtUtc,
                    now),
                cancellationToken);
        byte[] secret = RandomNumberGenerator.GetBytes(20);
        var authenticator = new MfaAuthenticator
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProtectedSecret = secretProtector.Protect(secret),
            CreatedAtUtc = now,
        };
        context.MfaAuthenticators.Add(authenticator);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        string encoded = Base32Encode(secret);
        string label = Uri.EscapeDataString(
            $"AppCore:{user.UserName}");
        string issuer = Uri.EscapeDataString("AppCore");
        return new MfaEnrollmentResult(
            authenticator.Id,
            encoded,
            $"otpauth://totp/{label}?secret={encoded}&issuer={issuer}&digits=6&period=30");
    }

    public async Task<MfaVerificationResult?> VerifyEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        long? authorizationVersion,
        bool restrictedRecovery,
        Guid authenticatorId,
        string code,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        bool sessionValid = restrictedRecovery
            ? await context.RestrictedRecoverySessions.AnyAsync(
                value =>
                    value.Id == sessionId
                    && value.UserId == userId
                    && value.RevokedAtUtc == null
                    && value.ExpiresAtUtc > now,
                cancellationToken)
            : authorizationVersion is not null
                && await sessionValidator.RecheckAsync(
                    sessionId,
                    authorizationVersion.Value,
                    cancellationToken);
        if (!sessionValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        MfaAuthenticator? authenticator = await context.MfaAuthenticators
            .SingleOrDefaultAsync(
                value =>
                    value.Id == authenticatorId
                    &&
                    value.UserId == userId
                    && value.RevokedAtUtc == null
                    && value.VerifiedAtUtc == null,
                cancellationToken);
        if (authenticator is null)
        {
            return null;
        }

        long? step = AuthenticationWorkflowService.FindAcceptedTotpStep(
            secretProtector.Unprotect(authenticator.ProtectedSecret),
            code,
            now);
        if (step is null
            || !await atomicState.AdvanceTotpStepAsync(
                authenticator.Id,
                step.Value,
                cancellationToken))
        {
            return null;
        }

        VersionedSecurityKey key = await securityKeys.GetCurrentKeyAsync(
            "challenge-hmac",
            cancellationToken);
        var rawCodes = new List<string>(10);
        await context.MfaRecoveryCodes
            .Where(value => value.UserId == userId && value.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.ConsumedAtUtc,
                    now),
                cancellationToken);
        for (int index = 0; index < 10; index++)
        {
            byte[] raw = RandomNumberGenerator.GetBytes(16);
            rawCodes.Add(EncodeBase64Url(raw));
            context.MfaRecoveryCodes.Add(
                new MfaRecoveryCode
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    KeyedHash = HMACSHA256.HashData(key.Key.Span, raw),
                    KeyVersion = key.Version,
                    CreatedAtUtc = now,
                });
        }

        ApplicationUser user = await context.Users.SingleAsync(
            value => value.Id == userId,
            cancellationToken);
        authenticator.VerifiedAtUtc = now;
        user.MfaState = MfaState.Active;
        Guid? rotatedSessionId = null;
        if (!restrictedRecovery)
        {
            rotatedSessionId = await sessionRotation.RotateAsync(
                userId,
                sessionId,
                user.AuthorizationVersion,
                now,
                "password,totp",
                cancellationToken);
        }
        await context.RestrictedRecoverySessions
            .Where(value =>
                value.Id == sessionId
                && value.UserId == userId
                && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.RevokedAtUtc,
                    now),
                cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.MfaEnrollmentCompleted,
                "success",
                userId,
                userId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MfaVerificationResult(
            rawCodes,
            rotatedSessionId,
            user.AuthorizationVersion);
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bits = 0;
        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(alphabet[(buffer << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    private static string EncodeBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
