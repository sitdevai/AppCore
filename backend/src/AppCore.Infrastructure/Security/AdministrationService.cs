using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppCore.Infrastructure.Security;

public sealed class AdministrationService(
    ApplicationDbContext context,
    IAccountLifecycleService lifecycle,
    ISecurityStateRevocationService revocation,
    ISecurityAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager) : IAdministrationService
{
    public async Task<IReadOnlyList<AdministrationUserResult>> ListUsersAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        string? value = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ApplicationUser[] users = await context.Users.AsNoTracking()
            .Where(user => value == null
                    || user.UserName!.Contains(value)
                    || user.Email != null && user.Email.Contains(value))
            .OrderBy(user => user.UserName)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        return await MapUsersAsync(users, cancellationToken);
    }

    public async Task<AdministrationUserResult?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await context.Users.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        return user is null
            ? null
            : (await MapUsersAsync([user], cancellationToken)).Single();
    }

    public async Task<AccountCreationResult> CreateUserAsync(
        Guid actorUserId,
        string username,
        string? email,
        CancellationToken cancellationToken = default)
    {
        AccountCreationResult result = await lifecycle.CreateAsync(
            username,
            email,
            protectedOwner: false,
            cancellationToken);
        await WriteAuditAsync(actorUserId, result.UserId, cancellationToken);
        return result;
    }

    public async Task<bool> UpdateEmailAsync(
        Guid actorUserId,
        Guid userId,
        string? email,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        ApplicationUser? user = await context.Users.SingleOrDefaultAsync(
            value => value.Id == userId
                && !value.IsProtectedOwner
                && value.AuthorizationVersion == expectedAuthorizationVersion,
            cancellationToken);
        if (user is null)
        {
            return false;
        }

        string? normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? null
            : userManager.NormalizeEmail(email.Trim());
        if (normalizedEmail is not null
            && await context.Users.AnyAsync(
                value => value.Id != userId && value.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return false;
        }

        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.AuthorizationVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);
        await revocation.RevokeAsync(userId, cancellationToken);
        await WriteAuditAsync(actorUserId, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TransitionUserAsync(
        Guid actorUserId,
        Guid userId,
        string operation,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        bool selfForbidden = actorUserId == userId
            && operation is "disable" or "suspend" or "archive";
        bool versionMatches = await context.Users.AsNoTracking().AnyAsync(
            value => value.Id == userId
                && !value.IsProtectedOwner
                && value.AuthorizationVersion == expectedAuthorizationVersion,
            cancellationToken);
        if (selfForbidden || !versionMatches
            || !await lifecycle.TransitionAsync(userId, operation, cancellationToken))
        {
            return false;
        }

        await WriteAuditAsync(actorUserId, userId, cancellationToken);
        return true;
    }

    public async Task<OneTimeChallengeResult?> IssueChallengeAsync(
        Guid actorUserId,
        Guid userId,
        string purpose,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        bool versionMatches = await context.Users.AsNoTracking().AnyAsync(
            value => value.Id == userId
                && !value.IsProtectedOwner
                && value.AuthorizationVersion == expectedAuthorizationVersion,
            cancellationToken);
        if (!versionMatches)
        {
            return null;
        }

        if (purpose == "password-reset"
            && !await lifecycle.TransitionAsync(userId, "startReset", cancellationToken))
        {
            return null;
        }

        OneTimeChallengeResult result = await lifecycle.IssueChallengeAsync(
            userId,
            purpose,
            cancellationToken);
        await WriteAuditAsync(actorUserId, userId, cancellationToken);
        return result;
    }

    public async Task<OneTimeChallengeResult?> StartMfaRecoveryAsync(
        Guid actorUserId,
        Guid userId,
        long expectedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == userId
            || !await context.Users.AsNoTracking().AnyAsync(
                value => value.Id == userId
                    && !value.IsProtectedOwner
                    && value.AuthorizationVersion == expectedAuthorizationVersion,
                cancellationToken))
        {
            return null;
        }

        OneTimeChallengeResult? result = await lifecycle.StartMfaRecoveryAsync(
            userId,
            cancellationToken);
        if (result is not null)
        {
            await WriteAuditAsync(actorUserId, userId, cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<AdministrationRoleResult>> ListRolesAsync(
        CancellationToken cancellationToken = default)
    {
        ApplicationRole[] roles = await context.Roles.AsNoTracking()
            .OrderBy(value => value.Name)
            .ToArrayAsync(cancellationToken);
        RolePermissionAssignment[] permissions = await context.RolePermissions
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return roles.Select(role => new AdministrationRoleResult(
            role.Id,
            role.Name!,
            role.IsBuiltIn,
            role.IsProtected,
            role.IsArchived,
            role.ConcurrencyStamp!,
            permissions.Where(value => value.RoleId == role.Id)
                .Select(value => value.PermissionId)
                .OrderBy(value => value)
                .ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<AdministrationPermissionResult>> ListPermissionsAsync(
        CancellationToken cancellationToken = default) =>
        await context.Permissions.AsNoTracking()
            .OrderBy(value => value.Id)
            .Select(value => new AdministrationPermissionResult(
                value.Id,
                value.Assurance,
                value.Scope))
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyList<AdministrationUserResult>> MapUsersAsync(
        IReadOnlyCollection<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        Guid[] ids = users.Select(value => value.Id).ToArray();
        IdentityUserRole<Guid>[] assignments = await context.UserRoles.AsNoTracking()
            .Where(value => ids.Contains(value.UserId))
            .ToArrayAsync(cancellationToken);
        return users.Select(user => new AdministrationUserResult(
            user.Id,
            user.UserName!,
            user.Email,
            user.AccountStatus.ToString(),
            user.CredentialStatus.ToString(),
            user.MfaState.ToString(),
            user.AuthorizationVersion,
            user.IsProtectedOwner,
            assignments.Where(value => value.UserId == user.Id)
                .Select(value => value.RoleId)
                .ToArray())).ToArray();
    }

    private Task WriteAuditAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            new SecurityAuditEntry(
                SecurityAuditCodes.AdministrationAction,
                "success",
                actorUserId,
                targetUserId),
            cancellationToken);
}
