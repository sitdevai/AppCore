namespace AppCore.Application.Security;

public interface IRoleAuthorizationService
{
    Task<Guid?> CreateRoleAsync(
        Guid actorUserId,
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> RenameRoleAsync(
        Guid actorUserId,
        Guid roleId,
        string name,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default);

    Task<bool> ArchiveRoleAsync(
        Guid actorUserId,
        Guid roleId,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default);

    Task<bool> AssignRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        Guid roleId,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceRolePermissionsAsync(
        Guid actorUserId,
        Guid roleId,
        IReadOnlyCollection<string> permissionIds,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default);
}
