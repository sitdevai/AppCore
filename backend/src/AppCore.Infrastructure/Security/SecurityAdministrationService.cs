using System.Data;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class SecurityAdministrationService(
    ApplicationDbContext context,
    ISecurityAuditWriter auditWriter) : ISecurityAdministrationService
{
    public async Task<IReadOnlyList<SessionAdministrationResult>> ListSessionsAsync(
        Guid actorUserId, Guid currentSessionId, Guid? targetUserId,
        CancellationToken cancellationToken = default)
    {
        Guid userId = targetUserId ?? actorUserId;
        if (userId != actorUserId && !await TargetExistsAsync(userId, cancellationToken))
        {
            return [];
        }

        DateTimeOffset now = await DatabaseNowAsync(cancellationToken);
        DateTimeOffset idleBoundary = now.AddMinutes(-30);
        SessionAdministrationResult[] sessions = await context.ServerSessions.AsNoTracking()
            .Where(value => value.UserId == userId
                && value.RevokedAtUtc == null
                && value.AbsoluteExpiresAtUtc > now
                && value.LastActivityAtUtc > idleBoundary)
            .OrderByDescending(value => value.LastActivityAtUtc)
            .Select(value => new SessionAdministrationResult(
                value.Id, value.UserId, value.CreatedAtUtc, value.LastActivityAtUtc,
                value.AbsoluteExpiresAtUtc, value.MfaVerifiedAtUtc,
                value.AuthenticationMethods, value.DeviceLabel, value.ClientCategory,
                value.Id == currentSessionId))
            .ToArrayAsync(cancellationToken);
        if (targetUserId.HasValue)
        {
            await auditWriter.WriteAsync(
                new SecurityAuditEntry(SecurityAuditCodes.SessionViewed, "success",
                    actorUserId, userId), cancellationToken);
        }
        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(
        Guid actorUserId, Guid currentSessionId, Guid targetUserId, Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId != actorUserId
            && !await IsEligibleTargetAsync(targetUserId, cancellationToken))
        {
            return false;
        }
        if (targetUserId == actorUserId && sessionId == currentSessionId)
        {
            return false;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        DateTimeOffset now = await DatabaseNowAsync(cancellationToken);
        int affected = await context.ServerSessions
            .Where(value => value.Id == sessionId
                && value.UserId == targetUserId
                && value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        if (affected == 1)
        {
            await WriteAsync(actorUserId, targetUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return affected == 1;
    }

    public async Task<int> RevokeUserSessionsAsync(
        Guid actorUserId, Guid currentSessionId, Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId != actorUserId
            && !await IsEligibleTargetAsync(targetUserId, cancellationToken))
        {
            return 0;
        }
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        int affected = await RevokeWhereAsync(
            targetUserId,
            targetUserId == actorUserId ? currentSessionId : null,
            cancellationToken);
        await WriteAsync(actorUserId, targetUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task<int> RevokeGlobalSessionsAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        DateTimeOffset now = await DatabaseNowAsync(cancellationToken);
        int affected = await context.ServerSessions
            .Where(value => value.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
        await WriteAsync(actorUserId, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task<SecurityAuditPage> SearchAuditAsync(
        Guid actorUserId, SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        IQueryable<SecurityAuditEvent> filtered = Filter(query);
        int total = await filtered.CountAsync(cancellationToken);
        SecurityAuditResult[] items = await SortAndProject(filtered, query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(SecurityAuditCodes.AuditViewed, "success", actorUserId),
            cancellationToken);
        return new SecurityAuditPage(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<SecurityAuditResult>> ExportAuditAsync(
        Guid actorUserId, SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        SecurityAuditResult[] items = await SortAndProject(Filter(query), query)
            .Take(10_000)
            .ToArrayAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(SecurityAuditCodes.AuditExported, "success", actorUserId),
            cancellationToken);
        return items;
    }

    private IQueryable<SecurityAuditEvent> Filter(SecurityAuditQuery query)
    {
        IQueryable<SecurityAuditEvent> values = context.SecurityAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EventCode))
            values = values.Where(value => value.EventCode == query.EventCode.Trim());
        if (query.ActorUserId.HasValue)
            values = values.Where(value => value.ActorUserId == query.ActorUserId);
        if (query.TargetUserId.HasValue)
            values = values.Where(value => value.TargetUserId == query.TargetUserId);
        if (query.FromUtc.HasValue)
            values = values.Where(value => value.OccurredAtUtc >= query.FromUtc);
        if (query.ToUtc.HasValue)
            values = values.Where(value => value.OccurredAtUtc <= query.ToUtc);
        return values;
    }

    private IQueryable<SecurityAuditResult> Project(IQueryable<SecurityAuditEvent> values) =>
        from value in values
        join auditContext in context.SecurityAuditContexts.AsNoTracking()
            on value.Id equals auditContext.SecurityAuditEventId into contexts
        from auditContext in contexts.DefaultIfEmpty()
        select new SecurityAuditResult(
            value.Id, value.EventCode, value.ResultCode, value.OccurredAtUtc,
            value.ActorUserId, value.TargetUserId, value.CorrelationId,
            value.DetailsJson, auditContext.SourceIp, auditContext.UserAgent);

    private IQueryable<SecurityAuditResult> SortAndProject(
        IQueryable<SecurityAuditEvent> values, SecurityAuditQuery query)
    {
        string sortBy = query.SortBy.Trim().ToLowerInvariant();
        if (sortBy is not ("occurredatutc" or "eventcode" or "resultcode"
            or "actoruserid" or "targetuserid" or "sourceip" or "correlationid"))
            sortBy = "occurredatutc";
        bool descending = !string.Equals(
            query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        if (sortBy == "sourceip")
        {
            var joined =
                from value in values
                join auditContext in context.SecurityAuditContexts.AsNoTracking()
                    on value.Id equals auditContext.SecurityAuditEventId into contexts
                from auditContext in contexts.DefaultIfEmpty()
                select new { Value = value, Context = auditContext };
            var orderedBySourceIp = descending
                ? joined.OrderByDescending(item => item.Context.SourceIp)
                    .ThenByDescending(item => item.Value.Id)
                : joined.OrderBy(item => item.Context.SourceIp)
                    .ThenBy(item => item.Value.Id);
            return orderedBySourceIp.Select(item => new SecurityAuditResult(
                item.Value.Id, item.Value.EventCode, item.Value.ResultCode,
                item.Value.OccurredAtUtc, item.Value.ActorUserId,
                item.Value.TargetUserId, item.Value.CorrelationId,
                item.Value.DetailsJson, item.Context.SourceIp,
                item.Context.UserAgent));
        }

        IOrderedQueryable<SecurityAuditEvent> ordered = (sortBy, descending) switch
        {
            ("eventcode", false) => values.OrderBy(value => value.EventCode),
            ("eventcode", true) => values.OrderByDescending(value => value.EventCode),
            ("resultcode", false) => values.OrderBy(value => value.ResultCode),
            ("resultcode", true) => values.OrderByDescending(value => value.ResultCode),
            ("actoruserid", false) => values.OrderBy(value => value.ActorUserId),
            ("actoruserid", true) => values.OrderByDescending(value => value.ActorUserId),
            ("targetuserid", false) => values.OrderBy(value => value.TargetUserId),
            ("targetuserid", true) => values.OrderByDescending(value => value.TargetUserId),
            ("correlationid", false) => values.OrderBy(value => value.CorrelationId),
            ("correlationid", true) => values.OrderByDescending(value => value.CorrelationId),
            ("occurredatutc", false) => values.OrderBy(value => value.OccurredAtUtc),
            _ => values.OrderByDescending(value => value.OccurredAtUtc),
        };

        IOrderedQueryable<SecurityAuditEvent> stable = descending
            ? ordered.ThenByDescending(value => value.Id)
            : ordered.ThenBy(value => value.Id);
        return Project(stable);
    }

    private Task<bool> IsEligibleTargetAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(
            value => value.Id == userId && !value.IsProtectedOwner,
            cancellationToken);

    private Task<bool> TargetExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(value => value.Id == userId, cancellationToken);

    private async Task<int> RevokeWhereAsync(
        Guid userId,
        Guid? excludedSessionId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = await DatabaseNowAsync(cancellationToken);
        return await context.ServerSessions
            .Where(value => value.UserId == userId
                && value.RevokedAtUtc == null
                && (!excludedSessionId.HasValue || value.Id != excludedSessionId.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.RevokedAtUtc, now),
                cancellationToken);
    }

    private Task WriteAsync(Guid actor, Guid? target, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            new SecurityAuditEntry(SecurityAuditCodes.SessionRevoked, "success", actor, target),
            cancellationToken);

    private Task<DateTimeOffset> DatabaseNowAsync(CancellationToken cancellationToken) =>
        context.Database.SqlQuery<DateTimeOffset>(
            $"SELECT statement_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
}
