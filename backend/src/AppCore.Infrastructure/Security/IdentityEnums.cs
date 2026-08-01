namespace AppCore.Infrastructure.Security;

public enum AccountStatus
{
    Enabled,
    Disabled,
    Suspended,
    Archived,
}

public enum CredentialStatus
{
    ActivationPending,
    Active,
    ResetPending,
}

public enum MfaState
{
    NotEnrolled,
    Active,
    RecoveryPending,
}

public enum SecurityChallengePurpose
{
    Activation,
    PasswordReset,
    AdministrativeMfaRecovery,
}

public enum BootstrapState
{
    NotStarted,
    OwnerCreated,
    ReadyForPrivilegeGrant,
    Completed,
}
