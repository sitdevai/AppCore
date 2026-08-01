namespace AppCore.Application.Security;

public static class SecurityAuditCodes
{
    public const string SessionCreated = "security.session.created";
    public const string SessionRotated = "security.session.rotated";
    public const string SessionRevoked = "security.session.revoked";
    public const string SessionViewed = "security.session.viewed";
    public const string ConcurrentSessionRevoked = "security.session.concurrent_revoked";
    public const string ChallengeIssued = "security.challenge.issued";
    public const string ChallengeConsumed = "security.challenge.consumed";
    public const string MfaReplayRejected = "security.mfa.replay_rejected";
    public const string BootstrapStateChanged = "security.bootstrap.state_changed";
    public const string LoginFailed = "security.login.failed";
    public const string LoginSucceeded = "security.login.succeeded";
    public const string LoginThrottleStarted = "security.login.throttle_started";
    public const string LoginThrottleEnded = "security.login.throttle_ended";
    public const string Logout = "security.logout";
    public const string PasswordChanged = "security.password.changed";
    public const string MfaChallengeIssued = "security.mfa.challenge_issued";
    public const string MfaChallengeFailed = "security.mfa.challenge_failed";
    public const string AccountCreated = "security.account.created";
    public const string AccountStateChanged = "security.account.state_changed";
    public const string ActivationCompleted = "security.activation.completed";
    public const string PasswordResetCompleted = "security.password_reset.completed";
    public const string MfaEnrollmentCompleted = "security.mfa.enrollment_completed";
    public const string MfaRecoveryStarted = "security.mfa.recovery_started";
    public const string MfaRecoverySessionCreated =
        "security.mfa.recovery_session_created";
    public const string RoleAssigned = "security.authorization.role_assigned";
    public const string RoleRemoved = "security.authorization.role_removed";
    public const string RoleCreated = "security.authorization.role_created";
    public const string RoleRenamed = "security.authorization.role_renamed";
    public const string RoleArchived = "security.authorization.role_archived";
    public const string RolePermissionsChanged =
        "security.authorization.role_permissions_changed";
    public const string BootstrapPrivilegeGranted =
        "security.bootstrap.privilege_granted";
    public const string AdministrationAction = "security.administration.action";
    public const string AuditViewed = "security.audit.viewed";
    public const string AuditExported = "security.audit.exported";
    public const string VisualIdentityChanged = "settings.visual_identity.changed";

    public static bool IsSafe(string code) =>
        !string.IsNullOrWhiteSpace(code)
        && code.Length <= 128
        && code.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or '-');
}
