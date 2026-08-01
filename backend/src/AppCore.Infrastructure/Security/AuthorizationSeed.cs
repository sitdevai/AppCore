using AppCore.Application.Security;

namespace AppCore.Infrastructure.Security;

internal static class AuthorizationSeed
{
    internal sealed record RoleDefinition(
        Guid Id,
        string Name,
        bool IsProtected,
        IReadOnlyList<string> Permissions);

    private static readonly string[] OwnSessionPermissions =
    [SystemPermissions.SessionsViewOwn, SystemPermissions.SessionsRevokeOwn];

    internal static IReadOnlyList<RoleDefinition> Roles { get; } =
    [
        new(
            SystemRoleIds.SystemAdministrator,
            "System Administrator",
            true,
            SystemPermissions.Catalog.Select(value => value.Id).ToArray()),
        new(
            SystemRoleIds.UserAdministrator,
            "User Administrator",
            false,
            [
                SystemPermissions.UsersView,
                SystemPermissions.UsersCreate,
                SystemPermissions.UsersUpdate,
                SystemPermissions.UsersEnable,
                SystemPermissions.UsersDisable,
                SystemPermissions.UsersSuspend,
                SystemPermissions.UsersArchive,
                SystemPermissions.UsersRestore,
                SystemPermissions.UsersResetPassword,
                SystemPermissions.UsersIssueActivation,
                SystemPermissions.RolesView,
                SystemPermissions.RolesAssignToUsers,
                SystemPermissions.PermissionsView,
                .. OwnSessionPermissions,
            ]),
        new(
            SystemRoleIds.SecurityAdministrator,
            "Security Administrator",
            false,
            [
                SystemPermissions.UsersView,
                SystemPermissions.UsersResetMfa,
                SystemPermissions.UsersRevokeAuthenticators,
                SystemPermissions.UsersIssueMfaRecovery,
                SystemPermissions.RolesView,
                SystemPermissions.RolesCreate,
                SystemPermissions.RolesUpdate,
                SystemPermissions.RolesArchive,
                SystemPermissions.PermissionsView,
                SystemPermissions.PermissionsAssignToRoles,
                SystemPermissions.AuditSecurityView,
                SystemPermissions.AuditSecurityExport,
                SystemPermissions.SessionsViewForUser,
                SystemPermissions.SessionsRevokeForUser,
                SystemPermissions.SessionsRevokeGlobal,
                .. OwnSessionPermissions,
            ]),
        new(SystemRoleIds.ApplicationUser, "Application User", false, OwnSessionPermissions),
        new(SystemRoleIds.ManagerApprover, "Manager / Approver", false, OwnSessionPermissions),
        new(
            SystemRoleIds.AuditorReportingUser,
            "Auditor / Reporting User",
            false,
            [
                SystemPermissions.RolesView,
                SystemPermissions.PermissionsView,
                SystemPermissions.AuditSecurityView,
                .. OwnSessionPermissions,
            ]),
    ];
}
