using AppCore.Application.Security;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AppCore.Infrastructure.Security;

public sealed class PermissionAuthorizationService(
    ApplicationDbContext context,
    IHostEnvironment? environment = null) : IPermissionAuthorizationService
{
    public async Task<bool> HasPermissionAsync(
        ValidatedSession session,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        PermissionDefinition? definition = SystemPermissions.Find(permissionId);
        if (definition is null
            || !await MeetsAssuranceAsync(definition, session, cancellationToken))
        {
            return false;
        }

        return await (
            from user in context.Users.AsNoTracking()
            join assignment in context.UserRoles.AsNoTracking() on user.Id equals assignment.UserId
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join permission in context.RolePermissions.AsNoTracking() on role.Id equals permission.RoleId
            where user.Id == session.UserId
                && user.AuthorizationVersion == session.AuthorizationVersion
                && user.AccountStatus == AccountStatus.Enabled
                && user.CredentialStatus == CredentialStatus.Active
                && !role.IsArchived
                && permission.PermissionId == permissionId
            select permission)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AuthorizeAsync(
        ValidatedSession session,
        string permissionId,
        Guid? targetUserId = null,
        CancellationToken cancellationToken = default)
    {
        PermissionDefinition? definition = SystemPermissions.Find(permissionId);
        if (definition is null
            || !await HasPermissionAsync(session, permissionId, cancellationToken))
        {
            return false;
        }

        return definition.Scope switch
        {
            PermissionScope.OwnAccount => targetUserId == session.UserId,
            PermissionScope.AllUsers => targetUserId.HasValue
                && await IsEligibleTargetAsync(
                    permissionId, targetUserId.Value, cancellationToken),
            PermissionScope.GlobalSystem => true,
            PermissionScope.AssignedOrganization => false,
            _ => false,
        };
    }

    private async Task<bool> MeetsAssuranceAsync(
        PermissionDefinition definition,
        ValidatedSession session,
        CancellationToken cancellationToken)
    {
        if (definition.Assurance is PermissionAssurance.Standard
            or PermissionAssurance.Sensitive)
        {
            return true;
        }

        if (environment?.IsDevelopment() == true
            && await context.Users.AsNoTracking().AnyAsync(
                user => user.Id == session.UserId && user.IsProtectedOwner,
                cancellationToken))
        {
            return true;
        }

        bool hasTotp = session.MfaVerifiedAtUtc.HasValue
            && session.AuthenticationMethods.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Contains("totp", StringComparer.OrdinalIgnoreCase);
        if (!hasTotp)
        {
            return false;
        }

        if (definition.Assurance != PermissionAssurance.Emergency)
        {
            return true;
        }

        DateTimeOffset databaseNow = await context.Database
            .SqlQuery<DateTimeOffset>($"SELECT statement_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        TimeSpan age = databaseNow - session.MfaVerifiedAtUtc!.Value;
        return age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(15);
    }

    private Task<bool> IsEligibleTargetAsync(
        string permissionId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        bool viewOnly = permissionId is SystemPermissions.UsersView
            or SystemPermissions.SessionsViewForUser;
        return
        context.Users.AsNoTracking().AnyAsync(
            user => user.Id == targetUserId
                && (viewOnly || !user.IsProtectedOwner),
            cancellationToken);
    }
}
