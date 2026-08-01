import { fireEvent, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Component as ChallengePage } from '@/pages/authentication/ChallengePage'
import { renderWithProviders } from '@/test/render'

describe('ChallengePage', () => {
  it('allows paste and password-manager autofill for activation passwords', () => {
    renderWithProviders(<ChallengePage />, ['/activation'])

    expect(
      screen.getByRole('heading', { name: 'تفعيل الحساب' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('كلمة المرور الجديدة')).toHaveAttribute(
      'autocomplete',
      'new-password',
    )
    expect(screen.getByLabelText('تأكيد كلمة المرور')).toHaveAttribute(
      'autocomplete',
      'new-password',
    )
  })

  it('rejects an incomplete activation form locally', async () => {
    renderWithProviders(<ChallengePage />, ['/activation'])

    fireEvent.click(screen.getByRole('button', { name: 'تأكيد' }))

    expect(await screen.findAllByText('القيمة غير صالحة.')).toHaveLength(4)
  })
})
