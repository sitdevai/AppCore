using Microsoft.AspNetCore.Identity;

namespace AppCore.Infrastructure.Security;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Disabled;
    public CredentialStatus CredentialStatus { get; set; } =
        CredentialStatus.ActivationPending;
    public MfaState MfaState { get; set; } = MfaState.NotEnrolled;
    public long AuthorizationVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? TemporarilyThrottledUntilUtc { get; set; }
    public DateTimeOffset? FailedLoginWindowStartedAtUtc { get; set; }
    public bool IsProtectedOwner { get; set; }
}
