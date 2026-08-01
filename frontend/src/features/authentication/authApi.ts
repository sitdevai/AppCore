import { apiClient } from '@/lib/api/client'
import { resetAuthenticationState } from '@/lib/queryClient'

export interface CsrfBootstrap {
  requestToken: string
}

export interface AuthenticationFlowContext extends CsrfBootstrap {
  preSessionId: string
}

export interface LoginResult {
  status: 'authenticated' | 'mfaRequired' | 'recoveryRequired'
  mfaChallengeId?: string
}

export interface CurrentUser {
  userId: string
  username: string
  email?: string
  accountStatus: string
  mfaState: string
  permissions: string[]
}

export async function bootstrapCsrf(): Promise<CsrfBootstrap> {
  const response = await apiClient.get<CsrfBootstrap>('/v1/auth/csrf')
  return response.data
}

export async function bootstrapRecoveryCsrf(): Promise<CsrfBootstrap> {
  const response = await apiClient.get<CsrfBootstrap>('/v1/auth/recovery/csrf')
  return response.data
}

export async function bootstrapAuthenticationFlow(): Promise<AuthenticationFlowContext> {
  const csrf = await bootstrapCsrf()
  const response = await apiClient.post<{ preSessionId: string }>(
    '/v1/auth/pre-session',
    {},
    csrfHeaders(csrf),
  )
  return { ...csrf, preSessionId: response.data.preSessionId }
}

export async function login(
  username: string,
  password: string,
  csrf: AuthenticationFlowContext,
): Promise<LoginResult> {
  const response = await apiClient.post<LoginResult>(
    '/v1/auth/login',
    { username, password, preSessionId: csrf.preSessionId },
    { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
  )
  if (response.data.status === 'authenticated') {
    await resetAuthenticationState()
  }
  return response.data
}

export async function completeMfa(
  challengeId: string,
  code: string,
  csrf: AuthenticationFlowContext,
): Promise<LoginResult> {
  const response = await apiClient.post<LoginResult>(
    '/v1/auth/login/mfa',
    { challengeId, preSessionId: csrf.preSessionId, code },
    { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
  )
  if (response.data.status === 'authenticated') {
    await resetAuthenticationState()
  }
  return response.data
}

export async function completeChallenge(
  purpose: 'activation' | 'password-reset',
  values: {
    username: string
    code: string
    newPassword: string
  },
  csrf: AuthenticationFlowContext,
): Promise<void> {
  const route =
    purpose === 'activation'
      ? '/v1/auth/activation/complete'
      : '/v1/auth/password-reset/complete'
  await apiClient.post(
    route,
    { ...values, preSessionId: csrf.preSessionId },
    { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
  )
  await resetAuthenticationState()
}

function csrfHeaders(csrf: CsrfBootstrap) {
  return { headers: { 'X-CSRF-TOKEN': csrf.requestToken } }
}

export async function beginRecovery(
  values: { username: string; password: string; recoveryCode: string },
  csrf: AuthenticationFlowContext,
): Promise<void> {
  await apiClient.post(
    '/v1/auth/recovery',
    { ...values, preSessionId: csrf.preSessionId },
    csrfHeaders(csrf),
  )
  await resetAuthenticationState()
}

export async function beginMfaEnrollment(
  currentPassword?: string,
  recovery = false,
): Promise<{
  authenticatorId: string
  manualEntryKey: string
  provisioningUri: string
}> {
  const csrf = recovery ? await bootstrapRecoveryCsrf() : await bootstrapCsrf()
  const response = await apiClient.post<{
    authenticatorId: string
    manualEntryKey: string
    provisioningUri: string
  }>(
    recovery ? '/v1/auth/recovery/mfa/enrollment' : '/v1/auth/mfa/enrollment',
    recovery ? {} : { currentPassword },
    csrfHeaders(csrf),
  )
  return response.data
}

export async function verifyMfaEnrollment(
  authenticatorId: string,
  code: string,
  recovery = false,
): Promise<string[]> {
  const csrf = recovery ? await bootstrapRecoveryCsrf() : await bootstrapCsrf()
  const response = await apiClient.post<{ recoveryCodes: string[] }>(
    recovery
      ? '/v1/auth/recovery/mfa/enrollment/verify'
      : '/v1/auth/mfa/enrollment/verify',
    { authenticatorId, code },
    csrfHeaders(csrf),
  )
  await resetAuthenticationState()
  return response.data.recoveryCodes
}

export async function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  const csrf = await bootstrapCsrf()
  await apiClient.post(
    '/v1/auth/password/change',
    { currentPassword, newPassword },
    csrfHeaders(csrf),
  )
  await resetAuthenticationState()
}

export async function getCurrentUser(): Promise<CurrentUser> {
  const response = await apiClient.get<CurrentUser>('/v1/auth/me')
  return response.data
}

export async function logout(): Promise<void> {
  const csrf = await bootstrapCsrf()
  await apiClient.post('/v1/auth/logout', {}, csrfHeaders(csrf))
  await resetAuthenticationState()
}
