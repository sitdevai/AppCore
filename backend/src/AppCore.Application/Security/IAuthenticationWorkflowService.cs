namespace AppCore.Application.Security;

public interface IAuthenticationWorkflowService
{
    Task<LoginWorkflowResult> LoginAsync(
        string username,
        string password,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default);

    Task<LoginWorkflowResult> CompleteMfaLoginAsync(
        Guid challengeId,
        Guid anonymousPreSessionId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        Guid sessionId,
        long authorizationVersion,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CurrentUserResult?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecoveryWorkflowResult?> BeginRecoveryAsync(
        string username,
        string password,
        string recoveryCode,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default);

    Task LogoutRecoveryAsync(
        Guid recoverySessionId,
        CancellationToken cancellationToken = default);
}

public enum LoginWorkflowStatus
{
    Invalid,
    Authenticated,
    MfaRequired,
    RecoveryRequired,
}

public sealed record LoginWorkflowResult(
    LoginWorkflowStatus Status,
    Guid? UserId = null,
    Guid? SessionId = null,
    long AuthorizationVersion = 0,
    Guid? MfaChallengeId = null);

public sealed record CurrentUserResult(
    Guid UserId,
    string Username,
    string? Email,
    string AccountStatus,
    string MfaState,
    IReadOnlyList<string> Permissions);

public sealed record RecoveryWorkflowResult(
    Guid UserId,
    Guid RecoverySessionId);

public interface IAccountLifecycleService
{
    Task<AccountCreationResult> CreateAsync(
        string username,
        string? email,
        bool protectedOwner,
        CancellationToken cancellationToken = default);

    Task<OneTimeChallengeResult> IssueChallengeAsync(
        Guid userId,
        string purpose,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteChallengeAsync(
        string username,
        string purpose,
        string code,
        string newPassword,
        Guid anonymousPreSessionId,
        CancellationToken cancellationToken = default);

    Task<bool> TransitionAsync(
        Guid userId,
        string operation,
        CancellationToken cancellationToken = default);

    Task<OneTimeChallengeResult?> StartMfaRecoveryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record AccountCreationResult(Guid UserId, string Username);

public sealed record OneTimeChallengeResult(
    Guid UserId,
    string Code,
    DateTimeOffset ExpiresAtUtc);

public interface IMfaEnrollmentService
{
    Task<MfaEnrollmentResult?> BeginEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        long? authorizationVersion,
        string? currentPassword,
        bool restrictedRecovery,
        CancellationToken cancellationToken = default);

    Task<MfaVerificationResult?> VerifyEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        long? authorizationVersion,
        bool restrictedRecovery,
        Guid authenticatorId,
        string code,
        CancellationToken cancellationToken = default);
}

public sealed record MfaEnrollmentResult(
    Guid AuthenticatorId,
    string ManualEntryKey,
    string ProvisioningUri);

public sealed record MfaVerificationResult(
    IReadOnlyList<string> RecoveryCodes,
    Guid? SessionId = null,
    long AuthorizationVersion = 0);
