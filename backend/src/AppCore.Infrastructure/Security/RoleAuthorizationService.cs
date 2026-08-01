using System.Data;
using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class RoleAuthorizationService(
    ApplicationDbContext context,
    ISecurityStateRevocationService revocation,
    ISecurityAuditWriter auditWriter) : IRoleAuthorizationService
{
    public async Task<Guid?> CreateRoleAsync(
        Guid actorUserId,
        string name,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        string normalizedKey = normalizedName.ToUpperInvariant();
        if (normalizedName.Length is < 1 or > 128
            || !await ActorPossessesAsync(
                actorUserId,
                [SystemPermissions.RolesCreate],
                cancellationToken)
            || await context.Roles.AnyAsync(
                value => value.NormalizedName == normalizedKey,
                cancellationToken))
        {
            return null;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            NormalizedName = normalizedKey,
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        };
        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);
        await WriteRoleAuditAsync(SecurityAuditCodes.RoleCreated, actorUserId, role.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return role.Id;
    }

    public async Task<bool> RenameRoleAsync(
        Guid actorUserId,
        Guid roleId,
        string name,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();
        string normalizedKey = normalizedName.ToUpperInvariant();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        ApplicationRole? role = await context.Roles.SingleOrDefaultAsync(
            value => value.Id == roleId
                && !value.IsBuiltIn
                && !value.IsProtected
                && !value.IsArchived
                && value.ConcurrencyStamp == expectedRoleConcurrencyStamp,
            cancellationToken);
        if (role is null
            || normalizedName.Length is < 1 or > 128
            || !await ActorPossessesAsync(actorUserId, [SystemPermissions.RolesUpdate], cancellationToken)
            || await context.Roles.AnyAsync(
                value => value.Id != roleId
                    && value.NormalizedName == normalizedKey,
                cancellationToken))
        {
            return false;
        }

        role.Name = normalizedName;
        role.NormalizedName = normalizedKey;
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await WriteRoleAuditAsync(SecurityAuditCodes.RoleRenamed, actorUserId, role.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveRoleAsync(
        Guid actorUserId,
        Guid roleId,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        ApplicationRole? role = await context.Roles.SingleOrDefaultAsync(
            value => value.Id == roleId
                && !value.IsBuiltIn
                && !value.IsProtected
                && !value.IsArchived
                && value.ConcurrencyStamp == expectedRoleConcurrencyStamp,
            cancellationToken);
        if (role is null
            || !await ActorPossessesAsync(actorUserId, [SystemPermissions.RolesArchive], cancellationToken)
            || await context.UserRoles.AnyAsync(value => value.RoleId == roleId, cancellationToken))
        {
            return false;
        }

        role.IsArchived = true;
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await WriteRoleAuditAsync(SecurityAuditCodes.RoleArchived, actorUserId, role.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        Guid roleId,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId || roleId == SystemRoleIds.SystemAdministrator)
        {
            return false;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        ApplicationRole? role = await context.Roles.SingleOrDefaultAsync(
            value => value.Id == roleId
                && !value.IsArchived
                && value.ConcurrencyStamp == expectedRoleConcurrencyStamp,
            cancellationToken);
        ApplicationUser? target = await context.Users.SingleOrDefaultAsync(
            value => value.Id == targetUserId,
            cancellationToken);
        if (role is null || target is null || target.IsProtectedOwner)
        {
            return false;
        }

        string[] grantedPermissions = await context.RolePermissions
            .Where(value => value.RoleId == roleId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        if (!await ActorPossessesAsync(actorUserId, grantedPermissions, cancellationToken)
            || !await TargetMeetsMfaAssignmentRuleAsync(
                targetUserId,
                grantedPermissions,
                cancellationToken)
            || await CreatesToxicCombinationAsync(targetUserId, roleId, cancellationToken))
        {
            return false;
        }

        bool exists = await context.UserRoles.AnyAsync(
            value => value.UserId == targetUserId && value.RoleId == roleId,
            cancellationToken);
        if (exists)
        {
            return true;
        }

        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = targetUserId,
            RoleId = roleId,
        });
        target.AuthorizationVersion++;
        target.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await revocation.RevokeAsync(targetUserId, cancellationToken);
        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.RoleAssigned,
                "success",
                actorUserId,
                targetUserId,
                Details: new Dictionary<string, string?> { ["roleId"] = roleId.ToString("D") }),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId || roleId == SystemRoleIds.SystemAdministrator)
        {
            return false;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        ApplicationUser? target = await context.Users.SingleOrDefaultAsync(
            value => value.Id == targetUserId && !value.IsProtectedOwner,
            cancellationToken);
        string[] removedPermissions = await context.RolePermissions
            .Where(value => value.RoleId == roleId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        IdentityUserRole<Guid>? assignment = await context.UserRoles.SingleOrDefaultAsync(
            value => value.UserId == targetUserId && value.RoleId == roleId,
            cancellationToken);
        if (target is null
            || assignment is null
            || !await ActorPossessesAsync(
                actorUserId,
                [SystemPermissions.RolesAssignToUsers, .. removedPermissions],
                cancellationToken))
        {
            return false;
        }

        context.UserRoles.Remove(assignment);
        target.AuthorizationVersion++;
        target.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await revocation.RevokeAsync(targetUserId, cancellationToken);
        await WriteRoleAuditAsync(
            SecurityAuditCodes.RoleRemoved,
            actorUserId,
            roleId,
            cancellationToken,
            targetUserId);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReplaceRolePermissionsAsync(
        Guid actorUserId,
        Guid roleId,
        IReadOnlyCollection<string> permissionIds,
        string expectedRoleConcurrencyStamp,
        CancellationToken cancellationToken = default)
    {
        string[] requested = permissionIds.Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Any(value => SystemPermissions.Find(value) is null))
        {
            return false;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        ApplicationRole? role = await context.Roles.SingleOrDefaultAsync(
            value => value.Id == roleId
                && !value.IsBuiltIn
                && !value.IsProtected
                && !value.IsArchived
                && value.ConcurrencyStamp == expectedRoleConcurrencyStamp,
            cancellationToken);
        if (role is null)
        {
            return false;
        }

        string[] currentPermissions = await context.RolePermissions
            .Where(value => value.RoleId == roleId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        string[] affectedPermissions = currentPermissions
            .Union(requested, StringComparer.Ordinal)
            .ToArray();
        if (!await ActorPossessesAsync(
                actorUserId, affectedPermissions, cancellationToken))
        {
            return false;
        }

        Guid[] affectedUsers = await context.UserRoles
            .Where(value => value.RoleId == roleId)
            .Select(value => value.UserId)
            .ToArrayAsync(cancellationToken);
        if (ContainsElevatedPermission(requested)
            && await context.Users.AnyAsync(
                user => affectedUsers.Contains(user.Id)
                    && !context.MfaAuthenticators.Any(authenticator =>
                        authenticator.UserId == user.Id
                        && authenticator.VerifiedAtUtc != null
                        && authenticator.RevokedAtUtc == null),
                cancellationToken))
        {
            return false;
        }

        await context.RolePermissions
            .Where(value => value.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);
        context.RolePermissions.AddRange(requested.Select(value => new RolePermissionAssignment
        {
            RoleId = roleId,
            PermissionId = value,
        }));
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        ApplicationUser[] users = await context.Users
            .Where(value => affectedUsers.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        foreach (ApplicationUser user in users)
        {
            user.AuthorizationVersion++;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await context.SaveChangesAsync(cancellationToken);
        foreach (Guid userId in affectedUsers)
        {
            await revocation.RevokeAsync(userId, cancellationToken);
        }

        await auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.RolePermissionsChanged,
                "success",
                actorUserId,
                Details: new Dictionary<string, string?> { ["roleId"] = roleId.ToString("D") }),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> ActorPossessesAsync(
        Guid actorUserId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        string[] actorPermissions = await (
            from assignment in context.UserRoles
            join role in context.Roles on assignment.RoleId equals role.Id
            join permission in context.RolePermissions on role.Id equals permission.RoleId
            where assignment.UserId == actorUserId && !role.IsArchived
            select permission.PermissionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return permissions.All(value => actorPermissions.Contains(value, StringComparer.Ordinal));
    }

    private Task<bool> TargetMeetsMfaAssignmentRuleAsync(
        Guid targetUserId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken) =>
        !ContainsElevatedPermission(permissions)
            ? Task.FromResult(true)
            : context.Users.AnyAsync(
                user => user.Id == targetUserId
                    && user.MfaState == MfaState.Active
                    && context.MfaAuthenticators.Any(value =>
                        value.UserId == user.Id
                        && value.VerifiedAtUtc != null
                        && value.RevokedAtUtc == null),
                cancellationToken);

    private async Task<bool> CreatesToxicCombinationAsync(
        Guid targetUserId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        Guid[] assigned = await context.UserRoles
            .Where(value => value.UserId == targetUserId)
            .Select(value => value.RoleId)
            .ToArrayAsync(cancellationToken);
        bool routineConflict = roleId == SystemRoleIds.UserAdministrator
            && assigned.Contains(SystemRoleIds.SecurityAdministrator)
            || roleId == SystemRoleIds.SecurityAdministrator
            && assigned.Contains(SystemRoleIds.UserAdministrator);
        return routineConflict || assigned.Contains(SystemRoleIds.SystemAdministrator);
    }

    private static bool ContainsElevatedPermission(IEnumerable<string> permissionIds) =>
        permissionIds.Any(value => SystemPermissions.Find(value)?.Assurance
            is PermissionAssurance.HighRisk or PermissionAssurance.Emergency);

    private Task WriteRoleAuditAsync(
        string eventCode,
        Guid actorUserId,
        Guid roleId,
        CancellationToken cancellationToken,
        Guid? targetUserId = null) =>
        auditWriter.WriteAsync(
            new SecurityAuditEntry(
                eventCode,
                "success",
                actorUserId,
                targetUserId,
                Details: new Dictionary<string, string?>
                {
                    ["roleId"] = roleId.ToString("D"),
                }),
            cancellationToken);
}
