namespace AppCore.Application.Security;

public sealed record SecurityAuditEntry(
    string EventCode,
    string ResultCode,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null,
    string? CorrelationId = null,
    string? SourceIp = null,
    string? UserAgent = null,
    IReadOnlyDictionary<string, string?>? Details = null);
