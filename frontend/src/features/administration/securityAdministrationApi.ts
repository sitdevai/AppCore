import { bootstrapCsrf } from '@/features/authentication/authApi'
import { apiClient } from '@/lib/api/client'

export interface SessionRecord {
  sessionId: string
  userId: string
  createdAtUtc: string
  lastActivityAtUtc: string
  absoluteExpiresAtUtc: string
  mfaVerifiedAtUtc?: string
  authenticationMethods: string
  deviceLabel?: string
  clientCategory?: string
  isCurrent: boolean
}

export interface AuditRecord {
  id: number
  eventCode: string
  resultCode: string
  occurredAtUtc: string
  actorUserId?: string
  targetUserId?: string
  correlationId: string
  detailsJson?: string
  sourceIp?: string
  userAgent?: string
}

export interface AuditPage {
  items: AuditRecord[]
  page: number
  pageSize: number
  totalCount: number
}

export interface AuditFilters {
  eventCode?: string
  actorUserId?: string
  targetUserId?: string
  fromUtc?: string
  toUtc?: string
  page?: number
  pageSize?: number
  sortBy?: keyof Pick<
    AuditRecord,
    | 'occurredAtUtc'
    | 'eventCode'
    | 'resultCode'
    | 'actorUserId'
    | 'targetUserId'
    | 'sourceIp'
    | 'correlationId'
  >
  sortDirection?: 'asc' | 'desc'
}

export async function listOwnSessions() {
  return (await apiClient.get<SessionRecord[]>('/v1/sessions/me')).data
}

export async function listUserSessions(userId: string) {
  return (await apiClient.get<SessionRecord[]>(`/v1/sessions/users/${userId}`))
    .data
}

export async function revokeSession(session: SessionRecord, own: boolean) {
  const csrf = await bootstrapCsrf()
  const path = own
    ? `/v1/sessions/me/${session.sessionId}`
    : `/v1/sessions/users/${session.userId}/${session.sessionId}`
  await apiClient.delete(path, {
    data: { confirmed: true },
    headers: { 'X-CSRF-TOKEN': csrf.requestToken },
  })
}

export async function revokeAllSessions(userId?: string) {
  const csrf = await bootstrapCsrf()
  const path = userId
    ? `/v1/sessions/users/${userId}/revoke-all`
    : '/v1/sessions/me/revoke-all'
  await apiClient.post(
    path,
    { confirmed: true },
    { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
  )
}

export async function revokeGlobalSessions() {
  const csrf = await bootstrapCsrf()
  await apiClient.post(
    '/v1/sessions/revoke-global',
    { confirmed: true },
    { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
  )
}

export async function searchAudit(filters: AuditFilters) {
  return (
    await apiClient.get<AuditPage>('/v1/security-audit', { params: filters })
  ).data
}

export async function exportAudit(filters: AuditFilters) {
  const csrf = await bootstrapCsrf()
  const response = await apiClient.post<Blob>(
    '/v1/security-audit/export',
    { confirmed: true },
    {
      params: filters,
      responseType: 'blob',
      headers: { 'X-CSRF-TOKEN': csrf.requestToken },
    },
  )
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = 'security-audit.csv'
  link.click()
  URL.revokeObjectURL(url)
}
