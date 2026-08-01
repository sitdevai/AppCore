import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EmptyState } from '@/shared/feedback/EmptyState'
import { renderWithProviders } from '@/test/render'

describe('EmptyState', () => {
  it('renders translated Arabic empty-state text', () => {
    renderWithProviders(<EmptyState />)

    expect(
      screen.getByText('لا توجد بيانات', { selector: 'strong' }),
    ).toBeInTheDocument()
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
  })
})
