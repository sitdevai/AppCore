import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Component as SecurityPage } from '@/pages/authentication/SecurityPage'
import { renderWithProviders } from '@/test/render'

describe('SecurityPage', () => {
  it('requires current-password re-entry before MFA enrollment', () => {
    renderWithProviders(<SecurityPage />, ['/account/security'])

    expect(
      screen.getByRole('heading', { name: 'أمان الحساب' }),
    ).toBeInTheDocument()
    expect(screen.getAllByLabelText('كلمة المرور')).toHaveLength(2)
    for (const password of screen.getAllByLabelText('كلمة المرور')) {
      expect(password).toHaveAttribute('autocomplete', 'current-password')
    }
  })
})
