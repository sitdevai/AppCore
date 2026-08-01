namespace AppCore.Application.Security;

public enum PermissionAssurance
{
    Standard,
    Sensitive,
    HighRisk,
    Emergency,
}

public enum PermissionScope
{
    OwnAccount,
    AllUsers,
    GlobalSystem,
    AssignedOrganization,
}

public sealed record PermissionDefinition(
    string Id,
    PermissionAssurance Assurance,
    PermissionScope Scope);

public static class SystemPermissions
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersUpdate = "Users.Update";
    public const string UsersEnable = "Users.Enable";
    public const string UsersDisable = "Users.Disable";
    public const string UsersSuspend = "Users.Suspend";
    public const string UsersArchive = "Users.Archive";
    public const string UsersRestore = "Users.Restore";
    public const string UsersResetPassword = "Users.ResetPassword";
    public const string UsersIssueActivation = "Users.IssueActivation";
    public const string UsersResetMfa = "Users.ResetMfa";
    public const string UsersRevokeAuthenticators = "Users.RevokeAuthenticators";
    public const string UsersIssueMfaRecovery = "Users.IssueMfaRecovery";
    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesUpdate = "Roles.Update";
    public const string RolesArchive = "Roles.Archive";
    public const string RolesAssignToUsers = "Roles.AssignToUsers";
    public const string PermissionsView = "Permissions.View";
    public const string PermissionsAssignToRoles = "Permissions.AssignToRoles";
    public const string AuditSecurityView = "Audit.Security.View";
    public const string AuditSecurityExport = "Audit.Security.Export";
    public const string SessionsViewOwn = "Sessions.ViewOwn";
    public const string SessionsRevokeOwn = "Sessions.RevokeOwn";
    public const string SessionsViewForUser = "Sessions.ViewForUser";
    public const string SessionsRevokeForUser = "Sessions.RevokeForUser";
    public const string SessionsRevokeGlobal = "Sessions.RevokeGlobal";
    public const string SettingsVisualIdentityView = "Settings.VisualIdentity.View";
    public const string SettingsVisualIdentityUpdate = "Settings.VisualIdentity.Update";

    public static IReadOnlyList<PermissionDefinition> Catalog { get; } =
    [
        Sensitive(UsersView, PermissionScope.AllUsers),
        HighRisk(UsersCreate, PermissionScope.AllUsers),
        HighRisk(UsersUpdate, PermissionScope.AllUsers),
        HighRisk(UsersEnable, PermissionScope.AllUsers),
        HighRisk(UsersDisable, PermissionScope.AllUsers),
        HighRisk(UsersSuspend, PermissionScope.AllUsers),
        HighRisk(UsersArchive, PermissionScope.AllUsers),
        HighRisk(UsersRestore, PermissionScope.AllUsers),
        HighRisk(UsersResetPassword, PermissionScope.AllUsers),
        HighRisk(UsersIssueActivation, PermissionScope.AllUsers),
        HighRisk(UsersResetMfa, PermissionScope.AllUsers),
        HighRisk(UsersRevokeAuthenticators, PermissionScope.AllUsers),
        HighRisk(UsersIssueMfaRecovery, PermissionScope.AllUsers),
        Sensitive(RolesView, PermissionScope.GlobalSystem),
        HighRisk(RolesCreate, PermissionScope.GlobalSystem),
        HighRisk(RolesUpdate, PermissionScope.GlobalSystem),
        HighRisk(RolesArchive, PermissionScope.GlobalSystem),
        HighRisk(RolesAssignToUsers, PermissionScope.AllUsers),
        Sensitive(PermissionsView, PermissionScope.GlobalSystem),
        HighRisk(PermissionsAssignToRoles, PermissionScope.GlobalSystem),
        Sensitive(AuditSecurityView, PermissionScope.GlobalSystem),
        HighRisk(AuditSecurityExport, PermissionScope.GlobalSystem),
        Standard(SessionsViewOwn, PermissionScope.OwnAccount),
        Standard(SessionsRevokeOwn, PermissionScope.OwnAccount),
        Sensitive(SessionsViewForUser, PermissionScope.AllUsers),
        HighRisk(SessionsRevokeForUser, PermissionScope.AllUsers),
        new(SessionsRevokeGlobal, PermissionAssurance.Emergency, PermissionScope.GlobalSystem),
        Sensitive(SettingsVisualIdentityView, PermissionScope.GlobalSystem),
        HighRisk(SettingsVisualIdentityUpdate, PermissionScope.GlobalSystem),
    ];

    public static PermissionDefinition? Find(string id) =>
        Catalog.FirstOrDefault(value => value.Id == id);

    private static PermissionDefinition Standard(string id, PermissionScope scope) =>
        new(id, PermissionAssurance.Standard, scope);

    private static PermissionDefinition Sensitive(string id, PermissionScope scope) =>
        new(id, PermissionAssurance.Sensitive, scope);

    private static PermissionDefinition HighRisk(string id, PermissionScope scope) =>
        new(id, PermissionAssurance.HighRisk, scope);
}

public static class SystemRoleIds
{
    public static readonly Guid SystemAdministrator = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid UserAdministrator = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SecurityAdministrator = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid ApplicationUser = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid ManagerApprover = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid AuditorReportingUser = Guid.Parse("10000000-0000-0000-0000-000000000006");
}

public interface IPermissionAuthorizationService
{
    Task<bool> HasPermissionAsync(
        ValidatedSession session,
        string permissionId,
        CancellationToken cancellationToken = default);

    Task<bool> AuthorizeAsync(
        ValidatedSession session,
        string permissionId,
        Guid? targetUserId = null,
        CancellationToken cancellationToken = default);
}
