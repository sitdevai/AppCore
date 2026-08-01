namespace AppCore.Application.Security;

public interface IAdministrationService
{
    Task<IReadOnlyList<AdministrationUserResult>> ListUsersAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<AdministrationUserResult?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AccountCreationResult> CreateUserAsync(
        Guid actorUserId,
        string username,
        string? email,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateEmailAsync(
        Guid actorUserId,
        Guid userId,
        string? email,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<bool> TransitionUserAsync(
        Guid actorUserId,
        Guid userId,
        string operation,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<OneTimeChallengeResult?> IssueChallengeAsync(
        Guid actorUserId,
        Guid userId,
        string purpose,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<OneTimeChallengeResult?> StartMfaRecoveryAsync(
        Guid actorUserId,
        Guid userId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdministrationRoleResult>> ListRolesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdministrationPermissionResult>> ListPermissionsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record AdministrationUserResult(
    Guid UserId,
    string Username,
    string? Email,
    string AccountStatus,
    string CredentialStatus,
    string MfaState,
    long AuthorizationVersion,
    bool IsProtectedOwner,
    IReadOnlyList<Guid> RoleIds);

public sealed record AdministrationRoleResult(
    Guid RoleId,
    string Name,
    bool IsBuiltIn,
    bool IsProtected,
    bool IsArchived,
    string ConcurrencyStamp,
    IReadOnlyList<string> PermissionIds);

public sealed record AdministrationPermissionResult(
    string PermissionId,
    string Assurance,
    string Scope);
