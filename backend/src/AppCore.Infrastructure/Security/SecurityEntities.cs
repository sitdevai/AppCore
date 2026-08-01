namespace AppCore.Infrastructure.Security;

public sealed class ServerSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long AuthorizationVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? MfaVerifiedAtUtc { get; set; }
    public string AuthenticationMethods { get; set; } = "password";
    public string? DeviceLabel { get; set; }
    public string? ClientCategory { get; set; }
}

public sealed class AnonymousPreSession
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class SecurityChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public SecurityChallengePurpose Purpose { get; set; }
    public byte[] KeyedHash { get; set; } = [];
    public int KeyVersion { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? InvalidatedAtUtc { get; set; }
}

public sealed class MfaLoginChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AnonymousPreSessionId { get; set; }
    public long AuthorizationVersionAtIssue { get; set; }
    public Guid AuthenticatorId { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? InvalidatedAtUtc { get; set; }
}

public sealed class MfaAuthenticator
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] ProtectedSecret { get; set; } = [];
    public long? LastAcceptedTimeStep { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class MfaRecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] KeyedHash { get; set; } = [];
    public int KeyVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class RestrictedRecoverySession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class PasswordHistoryEntry
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class BootstrapProgress
{
    public int Id { get; set; } = 1;
    public BootstrapState State { get; set; }
    public Guid? ProtectedOwnerUserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class SecurityAuditEvent
{
    public long Id { get; private set; }
    public string EventCode { get; private set; } = null!;
    public string ResultCode { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public string CorrelationId { get; private set; } = null!;
    public string? DetailsJson { get; private set; }

    private SecurityAuditEvent()
    {
    }

    public SecurityAuditEvent(
        string eventCode,
        string resultCode,
        DateTimeOffset occurredAtUtc,
        Guid? actorUserId,
        Guid? targetUserId,
        string correlationId,
        string? detailsJson)
    {
        EventCode = eventCode;
        ResultCode = resultCode;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        CorrelationId = correlationId;
        DetailsJson = detailsJson;
    }
}

public sealed class SecurityAuditContext
{
    public long Id { get; set; }
    public long SecurityAuditEventId { get; set; }
    public SecurityAuditEvent SecurityAuditEvent { get; set; } = null!;
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
