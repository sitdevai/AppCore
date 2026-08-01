import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { RequirePermission } from './RequirePermission'
import { renderWithProviders } from '@/test/render'

vi.mock('@/features/authentication/authApi', () => ({
  getCurrentUser: vi.fn().mockResolvedValue({
    userId: '00000000-0000-0000-0000-000000000001',
    username: 'ordinary-user',
    accountStatus: 'Enabled',
    mfaState: 'NotEnrolled',
    permissions: [],
  }),
}))

describe('RequirePermission', () => {
  it('redirects direct navigation when the permission is absent', async () => {
    renderWithProviders(
      <Routes>
        <Route element={<RequirePermission permission="Users.View" />}>
          <Route path="/administration/users" element={<div>protected</div>} />
        </Route>
        <Route path="/forbidden" element={<div>forbidden</div>} />
      </Routes>,
      ['/administration/users'],
    )

    expect(await screen.findByText('forbidden')).toBeInTheDocument()
    expect(screen.queryByText('protected')).not.toBeInTheDocument()
  })
})
