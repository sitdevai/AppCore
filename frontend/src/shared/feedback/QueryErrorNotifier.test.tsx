import { act, render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AppProviders } from '@/app/AppProviders'
import type { ErrorHistoryRecord } from '@/lib/errorHistory'
import { queryErrorEventName } from '@/lib/queryClient'
import { RouteErrorPage } from '@/pages/status/RouteErrorPage'
import { QueryErrorNotifier } from './QueryErrorNotifier'

describe('QueryErrorNotifier', () => {
  it('navigates unexpected failures to the diagnostic error page', async () => {
    const router = createMemoryRouter(
      [
        { path: '/', element: <QueryErrorNotifier /> },
        { path: '/error', element: <RouteErrorPage /> },
      ],
      { initialEntries: ['/'] },
    )

    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    )

    act(() => {
      window.dispatchEvent(
        new CustomEvent<ErrorHistoryRecord>(queryErrorEventName, {
          detail: {
            status: 503,
            code: 'HTTP_503',
            correlationId: 'correlation-123',
            errorNumber: 'ERR-20260801010101-ABC12345',
            occurredAtUtc: '2026-08-01T01:01:01.000Z',
          },
        }),
      )
    })

    expect(await screen.findByText(/HTTP_503/)).toBeInTheDocument()
    expect(screen.getByText(/HTTP: 503/)).toBeInTheDocument()
    expect(screen.getByText(/correlation-123/)).toBeInTheDocument()
    expect(screen.getByText(/ERR-20260801010101-ABC12345/)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/error')
  })
})
