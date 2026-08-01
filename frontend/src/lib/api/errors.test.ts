import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { normalizeApiError } from '@/lib/api/errors'

describe('normalizeApiError', () => {
  it('normalizes problem details without exposing the raw error', () => {
    const error = new AxiosError(
      'sensitive transport message',
      'ERR_BAD_RESPONSE',
      undefined,
      undefined,
      {
        data: {
          title: 'Validation failed',
          type: 'https://example.test/problems/validation',
          correlationId: 'correlation-123',
          errors: { Name: ['Required'] },
        },
        status: 400,
        statusText: 'Bad Request',
        headers: new AxiosHeaders(),
        config: { headers: new AxiosHeaders() },
      },
    )

    expect(normalizeApiError(error)).toMatchObject({
      status: 400,
      title: 'Validation failed',
      problemType: 'https://example.test/problems/validation',
      detail: undefined,
      correlationId: 'correlation-123',
      validationErrors: { Name: ['Required'] },
    })
  })
})
