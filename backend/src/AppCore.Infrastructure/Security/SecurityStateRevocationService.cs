using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class SecurityStateRevocationService(
    ApplicationDbContext context,
    TimeProvider timeProvider) : ISecurityStateRevocationService
{
    public async Task RevokeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] preSessionIds = await context.MfaLoginChallenges
            .Where(value =>
                value.UserId == userId
                && value.ConsumedAtUtc == null
                && value.InvalidatedAtUtc == null)
            .Select(value => value.AnonymousPreSessionId)
            .ToArrayAsync(cancellationToken);

        await context.ServerSessions
            .Where(value => value.UserId == userId && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await context.RestrictedRecoverySessions
            .Where(value => value.UserId == userId && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await context.MfaLoginChallenges
            .Where(value =>
                value.UserId == userId
                && value.ConsumedAtUtc == null
                && value.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.InvalidatedAtUtc, now),
                cancellationToken);
        await context.SecurityChallenges
            .Where(value =>
                value.UserId == userId
                && value.ConsumedAtUtc == null
                && value.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.InvalidatedAtUtc, now),
                cancellationToken);
        if (preSessionIds.Length > 0)
        {
            await context.AnonymousPreSessions
                .Where(value =>
                    preSessionIds.Contains(value.Id)
                    && value.ConsumedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(value => value.ConsumedAtUtc, now),
                    cancellationToken);
        }
    }
}
