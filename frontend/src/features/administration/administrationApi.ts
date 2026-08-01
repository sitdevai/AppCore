import { bootstrapCsrf } from '@/features/authentication/authApi'
import { apiClient } from '@/lib/api/client'

export interface AdministrationUser {
  userId: string
  username: string
  email?: string
  accountStatus: string
  credentialStatus: string
  mfaState: string
  authorizationVersion: number
  isProtectedOwner: boolean
  roleIds: string[]
}

export interface AdministrationRole {
  roleId: string
  name: string
  isBuiltIn: boolean
  isProtected: boolean
  isArchived: boolean
  concurrencyStamp: string
  permissionIds: string[]
}

export interface AdministrationPermission {
  permissionId: string
  assurance: string
  scope: string
}

export interface OneTimeChallenge {
  userId: string
  code: string
  expiresAtUtc: string
}

export async function listUsers(search = '') {
  const response = await apiClient.get<AdministrationUser[]>(
    '/v1/administration/users',
    { params: search ? { search } : undefined },
  )
  return response.data
}

export async function createUser(values: { username: string; email?: string }) {
  const response = await csrfMutation<{ userId: string }>(
    'post',
    '/v1/administration/users',
    { ...values, email: values.email || null, confirmed: true },
  )
  return response.data
}

export async function transitionUser(
  user: AdministrationUser,
  operation: string,
) {
  await csrfMutation(
    'post',
    `/v1/administration/users/${user.userId}/transitions/${operation}`,
    {
      expectedAuthorizationVersion: user.authorizationVersion,
      confirmed: true,
    },
  )
}

export async function updateUserEmail(
  user: AdministrationUser,
  email?: string,
) {
  await csrfMutation('put', `/v1/administration/users/${user.userId}`, {
    email: email?.trim() || null,
    expectedAuthorizationVersion: user.authorizationVersion,
    confirmed: true,
  })
}

export async function issueChallenge(
  user: AdministrationUser,
  purpose: 'activation' | 'password-reset',
) {
  const response = await csrfMutation<OneTimeChallenge>(
    'post',
    `/v1/administration/users/${user.userId}/challenges/${purpose}`,
    {
      expectedAuthorizationVersion: user.authorizationVersion,
      confirmed: true,
    },
  )
  return response.data
}

export async function startMfaRecovery(user: AdministrationUser) {
  const response = await csrfMutation<OneTimeChallenge>(
    'post',
    `/v1/administration/users/${user.userId}/mfa-recovery`,
    {
      expectedAuthorizationVersion: user.authorizationVersion,
      confirmed: true,
    },
  )
  return response.data
}

export async function listRoles() {
  const response = await apiClient.get<AdministrationRole[]>(
    '/v1/administration/roles',
  )
  return response.data
}

export async function listPermissions() {
  const response = await apiClient.get<AdministrationPermission[]>(
    '/v1/administration/permissions',
  )
  return response.data
}

export async function createRole(name: string) {
  await csrfMutation('post', '/v1/administration/roles', {
    name,
    confirmed: true,
  })
}

export async function renameRole(role: AdministrationRole, name: string) {
  await csrfMutation('put', `/v1/administration/roles/${role.roleId}`, {
    name: name.trim(),
    expectedConcurrencyStamp: role.concurrencyStamp,
    confirmed: true,
  })
}

export async function archiveRole(role: AdministrationRole) {
  await csrfMutation(
    'post',
    `/v1/administration/roles/${role.roleId}/archive`,
    {
      name: role.name,
      expectedConcurrencyStamp: role.concurrencyStamp,
      confirmed: true,
    },
  )
}

export async function assignRole(
  user: AdministrationUser,
  role: AdministrationRole,
) {
  await csrfMutation('post', `/v1/administration/users/${user.userId}/roles`, {
    roleId: role.roleId,
    expectedRoleConcurrencyStamp: role.concurrencyStamp,
    confirmed: true,
  })
}

export async function removeRole(userId: string, roleId: string) {
  await csrfMutation(
    'delete',
    `/v1/administration/users/${userId}/roles/${roleId}`,
    { confirmed: true },
  )
}

export async function replaceRolePermissions(
  role: AdministrationRole,
  permissionIds: string[],
) {
  await csrfMutation(
    'put',
    `/v1/administration/roles/${role.roleId}/permissions`,
    {
      permissionIds,
      expectedConcurrencyStamp: role.concurrencyStamp,
      confirmed: true,
    },
  )
}

async function csrfMutation<T = void>(
  method: 'post' | 'put' | 'delete',
  url: string,
  data: unknown,
) {
  const csrf = await bootstrapCsrf()
  return apiClient.request<T>({
    method,
    url,
    data,
    headers: { 'X-CSRF-TOKEN': csrf.requestToken },
  })
}
