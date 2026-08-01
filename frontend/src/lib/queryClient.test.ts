import { describe, expect, it } from 'vitest'
import { ApiClientError } from '@/lib/api/errors'
import {
  getGlobalErrorDetails,
  shouldNotifyGlobalError,
  shouldRetryRequest,
} from '@/lib/queryClient'
import { formMutationMeta } from '@/shared/forms/formMutation'

describe('query error policy', () => {
  it('retries only one transient failure', () => {
    const serverError = new ApiClientError('Server error', undefined, 503)
    const validationError = new ApiClientError('Validation', undefined, 400)

    expect(shouldRetryRequest(0, serverError)).toBe(true)
    expect(shouldRetryRequest(1, serverError)).toBe(false)
    expect(shouldRetryRequest(0, validationError)).toBe(false)
  })

  it('suppresses expected and explicitly handled errors', () => {
    const forbidden = new ApiClientError('Forbidden', undefined, 403)
    const validation = new ApiClientError('Validation', undefined, 400)
    const serverError = new ApiClientError('Server error', undefined, 500)
    const networkError = new ApiClientError('Network error')

    expect(shouldNotifyGlobalError(forbidden)).toBe(false)
    expect(shouldNotifyGlobalError(validation)).toBe(true)
    expect(
      shouldNotifyGlobalError(
        validation,
        formMutationMeta.suppressHandledValidationError,
      ),
    ).toBe(false)
    expect(
      shouldNotifyGlobalError(
        serverError,
        formMutationMeta.suppressHandledValidationError,
      ),
    ).toBe(true)
    expect(
      shouldNotifyGlobalError(
        networkError,
        formMutationMeta.suppressHandledValidationError,
      ),
    ).toBe(true)
    expect(shouldNotifyGlobalError(serverError)).toBe(true)
  })

  it('creates safe diagnostics for the error page', () => {
    expect(
      getGlobalErrorDetails(
        new ApiClientError(
          'Server error',
          undefined,
          500,
          undefined,
          'correlation-123',
        ),
      ),
    ).toEqual({
      status: 500,
      code: 'HTTP_500',
      correlationId: 'correlation-123',
    })
    expect(getGlobalErrorDetails(new Error('failure'))).toEqual({
      code: 'UNEXPECTED_CLIENT_ERROR',
    })
  })
})
