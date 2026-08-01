import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import i18n from '@/i18n'
import { ApiClientError } from '@/lib/api/errors'

const validationCodePrefix = 'validation.'

export function translateValidationCode(code: string): string {
  const key = code.startsWith(validationCodePrefix)
    ? code.slice(validationCodePrefix.length)
    : ''
  const translationKey = `validation:${key}`

  return key && i18n.exists(translationKey)
    ? i18n.t(translationKey)
    : i18n.t('validation:invalid')
}

export function applyApiValidationErrors<TFields extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TFields>,
  fieldMap: Readonly<Record<string, Path<TFields>>>,
): boolean {
  if (
    !(error instanceof ApiClientError) ||
    (error.status !== 400 && !error.validationErrors)
  ) {
    return false
  }

  let appliedFieldError = false
  let hasUnmappedError = false
  for (const [serverField, messages] of Object.entries(
    error.validationErrors ?? {},
  )) {
    const field = fieldMap[serverField]
    const code = messages[0]
    if (field && code) {
      setError(field, {
        type: 'server',
        message: translateValidationCode(code),
      })
      appliedFieldError = true
    } else if (code) {
      hasUnmappedError = true
    }
  }

  if (hasUnmappedError || !appliedFieldError) {
    setError('root.server', {
      type: 'server',
      message: translateValidationCode(error.title),
    })
  }

  return appliedFieldError || hasUnmappedError || error.status === 400
}
