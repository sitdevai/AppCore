using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppCore.Infrastructure.Security;

public sealed class AtomicSecurityStateStore(
    ApplicationDbContext context,
    TimeProvider timeProvider)
{
    public async Task<bool> AdvanceTotpStepAsync(
        Guid authenticatorId,
        long acceptedTimeStep,
        CancellationToken cancellationToken = default)
    {
        int affected = await context.MfaAuthenticators
            .Where(authenticator =>
                authenticator.Id == authenticatorId
                && authenticator.RevokedAtUtc == null
                && (authenticator.LastAcceptedTimeStep == null
                    || authenticator.LastAcceptedTimeStep < acceptedTimeStep))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    authenticator => authenticator.LastAcceptedTimeStep,
                    acceptedTimeStep),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> ConsumeRecoveryCodeAsync(
        Guid recoveryCodeId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.MfaRecoveryCodes
            .Where(code => code.Id == recoveryCodeId && code.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(code => code.ConsumedAtUtc, now),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> ConsumeLoginChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.MfaLoginChallenges
            .Where(challenge =>
                challenge.Id == challengeId
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null
                && challenge.AttemptCount < 5
                && challenge.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.ConsumedAtUtc,
                    now),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> IncrementLoginChallengeAttemptAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.MfaLoginChallenges
            .Where(challenge =>
                challenge.Id == challengeId
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null
                && challenge.AttemptCount < 5
                && challenge.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.AttemptCount,
                    challenge => challenge.AttemptCount + 1),
                cancellationToken);
        return affected == 1;
    }

    public async Task ReplaceMfaLoginChallengeAsync(
        MfaLoginChallenge replacement,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IDbContextTransaction? transaction =
            context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({replacement.UserId.ToString()}, 0));",
            cancellationToken);
        await context.MfaLoginChallenges
            .Where(challenge =>
                challenge.UserId == replacement.UserId
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.InvalidatedAtUtc,
                    now),
                cancellationToken);
        context.MfaLoginChallenges.Add(replacement);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task ReplaceSecurityChallengeAsync(
        SecurityChallenge replacement,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IDbContextTransaction? transaction =
            context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
        string lockKey = $"{replacement.UserId:N}:{(int)replacement.Purpose}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));",
            cancellationToken);
        await context.SecurityChallenges
            .Where(challenge =>
                challenge.UserId == replacement.UserId
                && challenge.Purpose == replacement.Purpose
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.InvalidatedAtUtc,
                    now),
                cancellationToken);
        context.SecurityChallenges.Add(replacement);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<bool> IncrementSecurityChallengeAttemptAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.SecurityChallenges
            .Where(challenge =>
                challenge.Id == challengeId
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null
                && challenge.AttemptCount < 5
                && challenge.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.AttemptCount,
                    challenge => challenge.AttemptCount + 1),
                cancellationToken);
        return affected == 1;
    }

    public async Task<bool> ConsumeSecurityChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.SecurityChallenges
            .Where(challenge =>
                challenge.Id == challengeId
                && challenge.ConsumedAtUtc == null
                && challenge.InvalidatedAtUtc == null
                && challenge.AttemptCount < 5
                && challenge.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.ConsumedAtUtc,
                    now),
                cancellationToken);
        return affected == 1;
    }
}
