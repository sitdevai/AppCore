using System.ComponentModel.DataAnnotations;

namespace AppCore.Contracts.Authentication;

public sealed record CsrfBootstrapResponse(string RequestToken);

public sealed record PreSessionResponse(Guid PreSessionId);

public sealed record LoginRequest(
    [Required, StringLength(256)] string Username,
    [Required, StringLength(128)] string Password,
    Guid PreSessionId);

public sealed record LoginResponse(
    string Status,
    Guid? MfaChallengeId = null);

public sealed record MfaLoginRequest(
    Guid ChallengeId,
    Guid PreSessionId,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);

public sealed record ChallengeCompletionRequest(
    [Required, StringLength(256)] string Username,
    [Required, StringLength(32)] string Code,
    [Required, StringLength(128, MinimumLength = 15)] string NewPassword,
    Guid PreSessionId);

public sealed record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 15)] string NewPassword);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Username,
    string? Email,
    string AccountStatus,
    string MfaState,
    IReadOnlyList<string> Permissions);

public sealed record MfaEnrollmentResponse(
    Guid AuthenticatorId,
    string ManualEntryKey,
    string ProvisioningUri);

public sealed record BeginMfaEnrollmentRequest(
    [Required, StringLength(128)] string CurrentPassword);

public sealed record MfaEnrollmentVerificationRequest(
    Guid AuthenticatorId,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);

public sealed record MfaRecoveryCodesResponse(
    IReadOnlyList<string> RecoveryCodes);

public sealed record BeginRecoveryRequest(
    [Required, StringLength(256)] string Username,
    [Required, StringLength(128)] string Password,
    [Required, StringLength(32)] string RecoveryCode,
    Guid PreSessionId);
