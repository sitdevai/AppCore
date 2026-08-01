namespace AppCore.Api.Security;

public static class AuthenticationSchemes
{
    public const string Session = "AppCore.Session";
    public const string Recovery = "AppCore.Recovery";
    public const string SessionCookieName = "__Host-AppCore.Session";
    public const string RecoveryCookieName = "__Host-AppCore.Recovery";
    public const string SessionIdClaim = "du_session_id";
    public const string AuthorizationVersionClaim = "du_authorization_version";
    public const string RecoverySessionIdClaim = "du_recovery_session_id";
}
