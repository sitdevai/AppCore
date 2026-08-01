using System.ComponentModel.DataAnnotations;

namespace AppCore.Contracts.Administration;

public sealed record SessionAdministrationResponse(
    Guid SessionId, Guid UserId, DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc, DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset? MfaVerifiedAtUtc, string AuthenticationMethods,
    string? DeviceLabel, string? ClientCategory, bool IsCurrent);

public sealed record ConfirmedSecurityActionRequest(
    [Required] bool Confirmed);

public sealed record SecurityAuditResponse(
    long Id, string EventCode, string ResultCode, DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId, Guid? TargetUserId, string CorrelationId,
    string? DetailsJson, string? SourceIp, string? UserAgent);

public sealed record SecurityAuditPageResponse(
    IReadOnlyList<SecurityAuditResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
