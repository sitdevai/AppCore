import type { CurrentUser } from '@/features/authentication/authApi'

export const permissions = {
  usersView: 'Users.View',
  usersCreate: 'Users.Create',
  usersUpdate: 'Users.Update',
  usersEnable: 'Users.Enable',
  usersDisable: 'Users.Disable',
  usersSuspend: 'Users.Suspend',
  usersArchive: 'Users.Archive',
  usersRestore: 'Users.Restore',
  usersResetPassword: 'Users.ResetPassword',
  usersIssueActivation: 'Users.IssueActivation',
  usersResetMfa: 'Users.ResetMfa',
  usersIssueMfaRecovery: 'Users.IssueMfaRecovery',
  rolesView: 'Roles.View',
  rolesCreate: 'Roles.Create',
  rolesUpdate: 'Roles.Update',
  rolesArchive: 'Roles.Archive',
  rolesAssignToUsers: 'Roles.AssignToUsers',
  permissionsView: 'Permissions.View',
  permissionsAssignToRoles: 'Permissions.AssignToRoles',
  auditSecurityView: 'Audit.Security.View',
  auditSecurityExport: 'Audit.Security.Export',
  sessionsViewOwn: 'Sessions.ViewOwn',
  sessionsRevokeOwn: 'Sessions.RevokeOwn',
  sessionsViewForUser: 'Sessions.ViewForUser',
  sessionsRevokeForUser: 'Sessions.RevokeForUser',
  sessionsRevokeGlobal: 'Sessions.RevokeGlobal',
  settingsVisualIdentityView: 'Settings.VisualIdentity.View',
  settingsVisualIdentityUpdate: 'Settings.VisualIdentity.Update',
} as const

export function hasPermission(
  user: CurrentUser | undefined,
  permission: string,
) {
  return user?.permissions.includes(permission) ?? false
}
