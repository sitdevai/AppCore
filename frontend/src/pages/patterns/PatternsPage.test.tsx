import { fireEvent, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Component as PatternsPage } from '@/pages/patterns/PatternsPage'
import { renderWithProviders } from '@/test/render'

describe('PatternsPage', () => {
  it('uses Zod validation through React Hook Form inside the create modal', async () => {
    renderWithProviders(<PatternsPage />)

    fireEvent.click(screen.getByRole('button', { name: /إضافة عنصر تجريبي/ }))
    fireEvent.click(screen.getByRole('button', { name: 'حفظ' }))

    expect(await screen.findByText('هذا الحقل مطلوب')).toBeInTheDocument()
    const input = screen.getByLabelText(/حقل تجريبي/)
    const error = screen.getByRole('alert')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAttribute('aria-required', 'true')
    expect(input).toHaveAttribute('aria-describedby', error.id)
  })

  it('creates unique input and error identifiers for repeated modal forms', async () => {
    renderWithProviders(
      <>
        <PatternsPage />
        <PatternsPage />
      </>,
    )

    for (const button of screen.getAllByRole('button', {
      name: /إضافة عنصر تجريبي/,
    })) {
      fireEvent.click(button)
    }

    const inputs = screen.getAllByLabelText(/حقل تجريبي/)
    expect(inputs[0].id).not.toBe(inputs[1].id)

    for (const button of screen.getAllByRole('button', { name: 'حفظ' })) {
      fireEvent.click(button)
    }

    const errors = await screen.findAllByRole('alert')
    expect(errors[0].id).not.toBe(errors[1].id)
    expect(inputs[0]).toHaveAttribute('aria-describedby', errors[0].id)
    expect(inputs[1]).toHaveAttribute('aria-describedby', errors[1].id)
  })
})
