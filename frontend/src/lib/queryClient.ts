import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { ApiClientError } from '@/lib/api/errors'
import { createErrorRecord, saveErrorRecord } from '@/lib/errorHistory'

export const queryErrorEventName = 'appcore:query-error'

export interface GlobalErrorDetails {
  status?: number
  code: string
  correlationId?: string
}

export function getGlobalErrorDetails(error: unknown): GlobalErrorDetails {
  if (error instanceof ApiClientError) {
    return {
      status: error.status,
      code:
        error.problemType ??
        (error.status ? `HTTP_${error.status}` : 'NETWORK_ERROR'),
      correlationId: error.correlationId,
    }
  }

  return { code: 'UNEXPECTED_CLIENT_ERROR' }
}

function dispatchGlobalError(error: unknown) {
  const record = createErrorRecord(getGlobalErrorDetails(error))
  saveErrorRecord(record)
  window.dispatchEvent(
    new CustomEvent(queryErrorEventName, {
      detail: record,
    }),
  )
}

export function shouldRetryRequest(
  failureCount: number,
  error: unknown,
): boolean {
  if (failureCount >= 1) return false
  if (!(error instanceof ApiClientError)) return true

  return (
    error.status === undefined ||
    error.status === 408 ||
    error.status === 429 ||
    error.status >= 500
  )
}

export function shouldNotifyGlobalError(
  error: unknown,
  suppressHandledValidationError?: unknown,
) {
  const isValidationError =
    error instanceof ApiClientError && error.status === 400

  if (suppressHandledValidationError === true && isValidationError) {
    return false
  }

  return !(
    error instanceof ApiClientError &&
    error.status !== undefined &&
    [401, 403].includes(error.status)
  )
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error, query) => {
      if (
        shouldNotifyGlobalError(
          error,
          query.meta?.suppressHandledValidationError,
        )
      ) {
        dispatchGlobalError(error)
      }
    },
  }),
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      if (
        shouldNotifyGlobalError(
          error,
          mutation.meta?.suppressHandledValidationError,
        )
      ) {
        dispatchGlobalError(error)
      }
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: shouldRetryRequest,
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: false,
    },
  },
})

export async function resetAuthenticationState() {
  await queryClient.cancelQueries()
  queryClient.clear()
}
