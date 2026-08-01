import axios from 'axios'

export class ApiClientError extends Error {
  readonly title: string
  readonly problemType?: string
  readonly status?: number
  readonly detail?: string
  readonly correlationId?: string
  readonly validationErrors?: Record<string, string[]>

  constructor(
    title: string,
    problemType?: string,
    status?: number,
    detail?: string,
    correlationId?: string,
    validationErrors?: Record<string, string[]>,
  ) {
    super(title)
    this.name = 'ApiClientError'
    this.title = title
    this.problemType = problemType
    this.status = status
    this.detail = detail
    this.correlationId = correlationId
    this.validationErrors = validationErrors
  }
}

export function normalizeApiError(error: unknown): ApiClientError {
  if (!axios.isAxiosError(error)) {
    return new ApiClientError('Unexpected client error')
  }

  const data = isRecord(error.response?.data) ? error.response.data : undefined

  return new ApiClientError(
    readString(data, 'title') ?? 'Request failed',
    readString(data, 'type'),
    error.response?.status,
    readString(data, 'detail'),
    readString(data, 'correlationId') ??
      readHeader(error.response?.headers, 'x-correlation-id'),
    readValidationErrors(data?.errors),
  )
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function readString(
  value: Record<string, unknown> | undefined,
  key: string,
): string | undefined {
  const candidate = value?.[key]
  return typeof candidate === 'string' ? candidate : undefined
}

function readHeader(
  headers: Record<string, unknown> | undefined,
  key: string,
): string | undefined {
  const candidate = headers?.[key]
  return typeof candidate === 'string' ? candidate : undefined
}

function readValidationErrors(
  value: unknown,
): Record<string, string[]> | undefined {
  if (!isRecord(value)) return undefined

  const entries = Object.entries(value).filter(
    (entry): entry is [string, string[]] =>
      Array.isArray(entry[1]) &&
      entry[1].every((item) => typeof item === 'string'),
  )

  return entries.length > 0 ? Object.fromEntries(entries) : undefined
}
