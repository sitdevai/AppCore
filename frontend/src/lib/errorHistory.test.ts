import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createErrorRecord,
  readErrorHistory,
  saveErrorRecord,
} from './errorHistory'

describe('error history', () => {
  beforeEach(() => localStorage.clear())

  it('creates searchable safe records and persists them newest first', () => {
    vi.spyOn(crypto, 'randomUUID').mockReturnValue(
      'abcdef12-0000-4000-8000-000000000000',
    )
    const record = createErrorRecord(
      { status: 500, code: 'HTTP_500', correlationId: 'correlation-1' },
      new Date('2026-08-01T12:34:56.000Z'),
    )

    saveErrorRecord(record)

    expect(record.errorNumber).toBe('ERR-20260801123456-ABCDEF12')
    expect(readErrorHistory()).toEqual([record])
  })
})
