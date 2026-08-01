import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { AppShell } from '@/layouts/AppShell'
import { renderWithProviders } from '@/test/render'

vi.mock('@/features/authentication/authApi', () => ({
  getCurrentUser: vi.fn().mockResolvedValue({
    userId: '00000000-0000-0000-0000-000000000001',
    username: 'owner',
    accountStatus: 'Enabled',
    mfaState: 'Active',
    permissions: [
      'Users.View',
      'Roles.View',
      'Sessions.ViewOwn',
      'Audit.Security.View',
      'Settings.VisualIdentity.View',
    ],
  }),
  logout: vi.fn().mockResolvedValue(undefined),
}))

describe('AppShell', () => {
  it('renders the translated horizontal navigation in RTL', async () => {
    renderWithProviders(<AppShell />)

    expect(
      await screen.findByRole('link', { name: 'نظام التطبيق' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('menuitem', { name: /الرئيسية/ }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('menuitem', { name: /إدارة المستخدمين/ }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('menuitem', { name: /الأدوار والصلاحيات/ }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('menuitem', { name: /الجلسات/ }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('menuitem', { name: /التدقيق الأمني/ }),
    ).toBeInTheDocument()
    expect(document.querySelector('.primary-navigation')).toHaveClass(
      'ant-menu-horizontal',
    )
    expect(
      screen.getByRole('link', {
        name: 'تجاوز إلى المحتوى الرئيسي',
      }),
    ).toHaveAttribute('href', '#application')
    expect(document.getElementById('application')).toHaveFocus()
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
  })

  it('does not select Home for an unknown route', async () => {
    renderWithProviders(<AppShell />, ['/missing-page'])

    expect(
      await screen.findByRole('menuitem', { name: /الرئيسية/ }),
    ).not.toHaveClass('ant-menu-item-selected')
  })

  it('does not select a navigation item for a prefix-only route', async () => {
    renderWithProviders(<AppShell />, ['/patterns-old'])

    expect(
      await screen.findByRole('menuitem', { name: /أنماط الواجهة/ }),
    ).not.toHaveClass('ant-menu-item-selected')
  })
})
