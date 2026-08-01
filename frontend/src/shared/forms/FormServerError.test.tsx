import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FormServerError } from '@/shared/forms/FormServerError'

describe('FormServerError', () => {
  it('renders nothing without a message', () => {
    const { container } = render(<FormServerError />)

    expect(container).toBeEmptyDOMElement()
  })

  it('announces and focuses a server error when it appears', () => {
    const { rerender } = render(<FormServerError />)

    rerender(<FormServerError message="تعذر حفظ النموذج." />)

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('تعذر حفظ النموذج.')
    expect(alert.parentElement).toHaveFocus()
  })
})
