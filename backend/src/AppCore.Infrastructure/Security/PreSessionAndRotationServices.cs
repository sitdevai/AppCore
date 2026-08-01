using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppCore.Infrastructure.Security;

public sealed class AnonymousPreSessionStore(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : IAnonymousPreSessionStore
{
    public async Task<Guid> CreateAsync(
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await context.MfaLoginChallenges
            .Where(value => value.ExpiresAtUtc <= now
                || value.ConsumedAtUtc != null
                || value.InvalidatedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
        await context.AnonymousPreSessions
            .Where(value => value.ExpiresAtUtc <= now
                && !context.MfaLoginChallenges.Any(challenge =>
                    challenge.AnonymousPreSessionId == value.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var preSession = new AnonymousPreSession
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
        };
        context.AnonymousPreSessions.Add(preSession);
        await context.SaveChangesAsync(cancellationToken);
        return preSession.Id;
    }

    public async Task<bool> ConsumeAsync(
        Guid preSessionId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int affected = await context.AnonymousPreSessions
            .Where(value =>
                value.Id == preSessionId
                && value.ConsumedAtUtc == null
                && value.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.ConsumedAtUtc,
                    now),
                cancellationToken);
        return affected == 1;
    }
}

public sealed class SessionRotationService(
    ApplicationDbContext context,
    ISecurityAuditWriter auditWriter)
    : ISessionRotationService
{
    public async Task<Guid> RotateAsync(
        Guid userId,
        Guid? priorSessionId,
        long authorizationVersion,
        DateTimeOffset? mfaVerifiedAtUtc,
        string authenticationMethods,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction =
            context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0));",
            cancellationToken);
        DateTimeOffset now = await context.Database
            .SqlQuery<DateTimeOffset>(
                $"SELECT statement_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (priorSessionId.HasValue)
        {
            await context.ServerSessions
                .Where(session =>
                    session.Id == priorSessionId.Value
                    && session.UserId == userId
                    && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        session => session.RevokedAtUtc,
                        now),
                    cancellationToken);
        }

        DateTimeOffset idleBoundary = now.AddMinutes(-30);
        Guid[] activeSessionIds = await context.ServerSessions
            .Where(session =>
                session.UserId == userId
                && session.RevokedAtUtc == null
                && session.AbsoluteExpiresAtUtc > now
                && session.LastActivityAtUtc > idleBoundary)
            .OrderBy(session => session.CreatedAtUtc)
            .Select(session => session.Id)
            .ToArrayAsync(cancellationToken);

        int sessionsToRevoke = Math.Max(0, activeSessionIds.Length - 2);
        foreach (Guid oldestId in activeSessionIds.Take(sessionsToRevoke))
        {
            await context.ServerSessions
                .Where(session => session.Id == oldestId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        session => session.RevokedAtUtc,
                        now),
                    cancellationToken);
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(
                    SecurityAuditCodes.ConcurrentSessionRevoked,
                    "success",
                    userId,
                    userId,
                    Details: new Dictionary<string, string?>
                    {
                        ["reason"] = "concurrent_limit",
                        ["revokedCount"] = "1",
                    }),
                cancellationToken);
        }

        var newSession = new ServerSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AuthorizationVersion = authorizationVersion,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            AbsoluteExpiresAtUtc = now.AddHours(8),
            MfaVerifiedAtUtc = mfaVerifiedAtUtc,
            AuthenticationMethods = authenticationMethods,
        };
        context.ServerSessions.Add(newSession);
        await context.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.SessionRotated,
                "success",
                userId,
                userId),
            cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return newSession.Id;
    }
}
