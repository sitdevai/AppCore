import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { AppProviders } from '@/app/AppProviders'

export function renderWithProviders(
  element: ReactElement,
  initialEntries: string[] = ['/'],
) {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={initialEntries}>{element}</MemoryRouter>
    </AppProviders>,
  )
}
