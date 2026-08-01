import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Component as RecoveryPage } from '@/pages/authentication/RecoveryPage'
import { renderWithProviders } from '@/test/render'

describe('RecoveryPage', () => {
  it('renders the Arabic restricted recovery form with safe autofill semantics', () => {
    renderWithProviders(<RecoveryPage />, ['/recovery'])

    expect(
      screen.getByRole('heading', {
        name: 'استرداد المصادقة متعددة العوامل',
      }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('كلمة المرور')).toHaveAttribute(
      'autocomplete',
      'current-password',
    )
    expect(
      screen.getByLabelText('رمز الاسترداد أو التحدي الإداري'),
    ).toHaveAttribute('autocomplete', 'off')
  })
})
