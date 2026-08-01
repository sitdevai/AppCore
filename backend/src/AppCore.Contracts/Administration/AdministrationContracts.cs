using System.ComponentModel.DataAnnotations;

namespace AppCore.Contracts.Administration;

public sealed record AdministrationUserResponse(
    Guid UserId,
    string Username,
    string? Email,
    string AccountStatus,
    string CredentialStatus,
    string MfaState,
    long AuthorizationVersion,
    bool IsProtectedOwner,
    IReadOnlyList<Guid> RoleIds);

public sealed record CreateAdministrationUserRequest(
    [Required, StringLength(256)] string Username,
    [EmailAddress, StringLength(256)] string? Email,
    bool Confirmed);

public sealed record UpdateAdministrationUserRequest(
    [EmailAddress, StringLength(256)] string? Email,
    long ExpectedAuthorizationVersion,
    bool Confirmed);

public sealed record UserTransitionRequest(
    long ExpectedAuthorizationVersion,
    bool Confirmed);

public sealed record OneTimeAdministrationChallengeResponse(
    Guid UserId,
    string Code,
    DateTimeOffset ExpiresAtUtc);

public sealed record AdministrationRoleResponse(
    Guid RoleId,
    string Name,
    bool IsBuiltIn,
    bool IsProtected,
    bool IsArchived,
    string ConcurrencyStamp,
    IReadOnlyList<string> PermissionIds);

public sealed record AdministrationPermissionResponse(
    string PermissionId,
    string Assurance,
    string Scope);

public sealed record CreateRoleRequest(
    [Required, StringLength(128)] string Name,
    bool Confirmed);

public sealed record UpdateRoleRequest(
    [Required, StringLength(128)] string Name,
    [Required] string ExpectedConcurrencyStamp,
    bool Confirmed);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyList<string> PermissionIds,
    [Required] string ExpectedConcurrencyStamp,
    bool Confirmed);

public sealed record RoleAssignmentRequest(
    Guid RoleId,
    [Required] string ExpectedRoleConcurrencyStamp,
    bool Confirmed);

public sealed record RemoveRoleAssignmentRequest(bool Confirmed);
