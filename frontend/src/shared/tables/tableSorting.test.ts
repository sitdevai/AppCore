import { describe, expect, it } from 'vitest'
import {
  compareTableBoolean,
  compareTableDate,
  compareTableNumber,
  compareTableText,
} from './tableSorting'

describe('table sorting helpers', () => {
  it('sorts Arabic and numeric text consistently', () => {
    expect(compareTableText('قسم 2', 'قسم 10')).toBeLessThan(0)
  })

  it('sorts missing text before populated text', () => {
    expect(compareTableText(undefined, 'admin')).toBeLessThan(0)
  })

  it('sorts ISO dates chronologically', () => {
    expect(
      compareTableDate('2026-01-01T00:00:00Z', '2026-02-01T00:00:00Z'),
    ).toBeLessThan(0)
  })

  it('sorts booleans and numbers', () => {
    expect(compareTableBoolean(false, true)).toBe(-1)
    expect(compareTableNumber(2, 1)).toBe(1)
  })
})
