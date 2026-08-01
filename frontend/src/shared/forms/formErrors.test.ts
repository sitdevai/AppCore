import { describe, expect, it } from 'vitest'
import type { ErrorOption, UseFormSetError } from 'react-hook-form'
import { ApiClientError } from '@/lib/api/errors'
import {
  applyApiValidationErrors,
  translateValidationCode,
} from '@/shared/forms/formErrors'

interface TestFields {
  displayName: string
}

describe('applyApiValidationErrors', () => {
  it('maps backend validation keys to React Hook Form fields', () => {
    const calls: Array<[string, ErrorOption]> = []
    const setError: UseFormSetError<TestFields> = (name, error) => {
      calls.push([name, error])
    }
    const error = new ApiClientError(
      'Validation failed',
      'https://example.test/problems/validation',
      400,
      undefined,
      undefined,
      { DisplayName: ['validation.required'] },
    )

    const applied = applyApiValidationErrors(error, setError, {
      DisplayName: 'displayName',
    })

    expect(applied).toBe(true)
    expect(calls).toEqual([
      ['displayName', { type: 'server', message: 'هذا الحقل مطلوب' }],
    ])
  })

  it('falls back to the localized invalid message for unknown codes', () => {
    expect(translateValidationCode('validation.unknown')).toBe(
      'القيمة غير صالحة.',
    )
    expect(translateValidationCode('raw server text')).toBe('القيمة غير صالحة.')
  })

  it('translates the validation problem title code', () => {
    expect(translateValidationCode('validation.failed')).toBe(
      'تعذر التحقق من البيانات المدخلة.',
    )
  })

  it('sets a localized form error when no server field can be mapped', () => {
    const calls: Array<[string, ErrorOption]> = []
    const setError: UseFormSetError<TestFields> = (name, error) => {
      calls.push([name, error])
    }
    const error = new ApiClientError(
      'validation.failed',
      undefined,
      400,
      undefined,
      undefined,
      { UnknownField: ['validation.required'] },
    )

    const applied = applyApiValidationErrors(error, setError, {})

    expect(applied).toBe(true)
    expect(calls).toEqual([
      [
        'root.server',
        {
          type: 'server',
          message: 'تعذر التحقق من البيانات المدخلة.',
        },
      ],
    ])
  })

  it('preserves mapped errors and reports additional unmapped errors', () => {
    const calls: Array<[string, ErrorOption]> = []
    const setError: UseFormSetError<TestFields> = (name, error) => {
      calls.push([name, error])
    }
    const error = new ApiClientError(
      'validation.failed',
      undefined,
      400,
      undefined,
      undefined,
      {
        DisplayName: ['validation.required'],
        OrganizationScope: ['validation.invalid'],
      },
    )

    const applied = applyApiValidationErrors(error, setError, {
      DisplayName: 'displayName',
    })

    expect(applied).toBe(true)
    expect(calls).toEqual([
      ['displayName', { type: 'server', message: 'هذا الحقل مطلوب' }],
      [
        'root.server',
        {
          type: 'server',
          message: 'تعذر التحقق من البيانات المدخلة.',
        },
      ],
    ])
  })
})
