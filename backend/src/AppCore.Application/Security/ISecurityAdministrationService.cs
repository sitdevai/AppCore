namespace AppCore.Application.Security;

public sealed record SessionAdministrationResult(
    Guid SessionId,
    Guid UserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset? MfaVerifiedAtUtc,
    string AuthenticationMethods,
    string? DeviceLabel,
    string? ClientCategory,
    bool IsCurrent);

public sealed record SecurityAuditQuery(
    string? EventCode,
    Guid? ActorUserId,
    Guid? TargetUserId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize,
    string SortBy = "occurredAtUtc",
    string SortDirection = "desc");

public sealed record SecurityAuditResult(
    long Id,
    string EventCode,
    string ResultCode,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    Guid? TargetUserId,
    string CorrelationId,
    string? DetailsJson,
    string? SourceIp,
    string? UserAgent);

public sealed record SecurityAuditPage(
    IReadOnlyList<SecurityAuditResult> Items,
    int Page,
    int PageSize,
    int TotalCount);

public interface ISecurityAdministrationService
{
    Task<IReadOnlyList<SessionAdministrationResult>> ListSessionsAsync(
        Guid actorUserId, Guid currentSessionId, Guid? targetUserId,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(
        Guid actorUserId, Guid currentSessionId, Guid targetUserId, Guid sessionId,
        CancellationToken cancellationToken = default);
    Task<int> RevokeUserSessionsAsync(
        Guid actorUserId, Guid currentSessionId, Guid targetUserId,
        CancellationToken cancellationToken = default);
    Task<int> RevokeGlobalSessionsAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);
    Task<SecurityAuditPage> SearchAuditAsync(
        Guid actorUserId, SecurityAuditQuery query,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAuditResult>> ExportAuditAsync(
        Guid actorUserId, SecurityAuditQuery query,
        CancellationToken cancellationToken = default);
}
