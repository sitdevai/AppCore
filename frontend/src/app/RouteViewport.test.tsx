import { fireEvent, render, screen } from '@testing-library/react'
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RouteViewport } from './RouteViewport'

describe('RouteViewport', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('returns the window to the top whenever the pathname changes', () => {
    const scrollTo = vi.fn()
    vi.stubGlobal('scrollTo', scrollTo)

    render(
      <MemoryRouter initialEntries={['/first']}>
        <Routes>
          <Route Component={RouteViewport}>
            <Route path="first" element={<Link to="/second">Continue</Link>} />
            <Route path="second" element={<div>Second page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    )

    expect(scrollTo).toHaveBeenLastCalledWith({
      top: 0,
      left: 0,
      behavior: 'auto',
    })

    fireEvent.click(screen.getByRole('link', { name: 'Continue' }))

    expect(screen.getByText('Second page')).toBeInTheDocument()
    expect(scrollTo).toHaveBeenCalledTimes(2)
  })
})
