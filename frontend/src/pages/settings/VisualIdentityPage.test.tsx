import { fireEvent, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/lib/queryClient'
import { renderWithProviders } from '@/test/render'
import { Component } from './VisualIdentityPage'

vi.mock('@/features/branding/brandingApi', () => ({
  defaultBranding: {
    organizationName: 'University',
    shortOrganizationName: 'DU',
    primaryColor: '#111111',
    secondaryColor: '#222222',
    headerColor: '#ffffff',
    backgroundColor: '#f4f6f8',
    patternColor: '#1d4ed8',
    backgroundPattern: 'Dots',
    lightLogoUrl: '/light-logo.png',
    darkLogoUrl: '/dark-logo.png',
    compactLogoUrl: '/compact-logo.png',
    faviconUrl: '/favicon.png',
    version: 1,
  },
  getBranding: vi.fn().mockResolvedValue({
    organizationName: 'University',
    shortOrganizationName: 'DU',
    primaryColor: '#111111',
    secondaryColor: '#222222',
    headerColor: '#ffffff',
    backgroundColor: '#f4f6f8',
    patternColor: '#1d4ed8',
    backgroundPattern: 'Dots',
    lightLogoUrl: '/light-logo.png',
    darkLogoUrl: '/dark-logo.png',
    compactLogoUrl: '/compact-logo.png',
    faviconUrl: '/favicon.png',
    version: 1,
  }),
  updateBranding: vi.fn(),
  uploadBrandingAsset: vi.fn(),
  restoreBrandingDefaults: vi.fn(),
}))

vi.mock('@/features/authentication/authApi', () => ({
  getCurrentUser: vi.fn().mockResolvedValue({
    userId: '00000000-0000-0000-0000-000000000001',
    username: 'owner',
    permissions: ['Settings.VisualIdentity.Update'],
  }),
}))

describe('VisualIdentityPage', () => {
  beforeEach(() => queryClient.clear())

  it('shows saved assets and opens the associated replacement file picker', async () => {
    const click = vi.spyOn(HTMLInputElement.prototype, 'click')
    renderWithProviders(<Component />)

    expect(await screen.findByAltText('شعار للخلفيات الفاتحة')).toHaveAttribute(
      'src',
      '/light-logo.png',
    )
    expect(screen.getByAltText('شعار للخلفيات الداكنة')).toHaveAttribute(
      'src',
      '/dark-logo.png',
    )
    expect(screen.getByAltText('الشعار المختصر')).toHaveAttribute(
      'src',
      '/compact-logo.png',
    )
    expect(screen.getByAltText('أيقونة المتصفح')).toHaveAttribute(
      'src',
      '/favicon.png',
    )
    expect(
      screen.getByAltText('شعار للخلفيات الفاتحة — معاينة مباشرة'),
    ).toHaveAttribute('src', '/light-logo.png')
    expect(
      screen.getByAltText('شعار للخلفيات الداكنة — معاينة مباشرة'),
    ).toHaveAttribute('src', '/dark-logo.png')

    const buttons = screen.getAllByRole('button', { name: /استبدال/ })
    expect(buttons).toHaveLength(4)

    fireEvent.click(buttons[0])

    expect(click).toHaveBeenCalledOnce()
  })
})
