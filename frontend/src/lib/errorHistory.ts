import type { GlobalErrorDetails } from '@/lib/queryClient'

const storageKey = 'appcore:error-history'

export interface ErrorHistoryRecord extends GlobalErrorDetails {
  errorNumber: string
  occurredAtUtc: string
}

export function createErrorRecord(
  details: GlobalErrorDetails,
  now = new Date(),
): ErrorHistoryRecord {
  return {
    ...details,
    errorNumber: `ERR-${now.toISOString().replace(/\D/g, '').slice(0, 14)}-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
    occurredAtUtc: now.toISOString(),
  }
}

export function saveErrorRecord(record: ErrorHistoryRecord) {
  localStorage.setItem(
    storageKey,
    JSON.stringify([record, ...readErrorHistory()].slice(0, 200)),
  )
}

export function readErrorHistory(): ErrorHistoryRecord[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]')
    if (!Array.isArray(value)) return []
    return value.filter(isErrorRecord)
  } catch {
    return []
  }
}

function isErrorRecord(value: unknown): value is ErrorHistoryRecord {
  if (typeof value !== 'object' || value === null) return false
  const record = value as Partial<ErrorHistoryRecord>
  return (
    typeof record.errorNumber === 'string' &&
    typeof record.occurredAtUtc === 'string' &&
    typeof record.code === 'string'
  )
}
