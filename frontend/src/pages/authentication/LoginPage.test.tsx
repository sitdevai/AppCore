import { fireEvent, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Component as LoginPage } from '@/pages/authentication/LoginPage'
import { renderWithProviders } from '@/test/render'

describe('LoginPage', () => {
  it('renders an Arabic RTL-friendly login form with autofill semantics', () => {
    renderWithProviders(<LoginPage />, ['/login'])

    expect(
      screen.getByRole('heading', { name: 'تسجيل الدخول' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('اسم المستخدم')).toHaveAttribute(
      'autocomplete',
      'username',
    )
    expect(screen.getByLabelText('كلمة المرور')).toHaveAttribute(
      'autocomplete',
      'current-password',
    )
  })

  it('validates required credentials before sending a request', async () => {
    renderWithProviders(<LoginPage />, ['/login'])

    fireEvent.click(screen.getByRole('button', { name: 'دخول' }))

    expect(await screen.findAllByText('هذا الحقل مطلوب')).toHaveLength(2)
  })
})
