import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { BrandingProvider } from './BrandingProvider'
import { useBranding } from './useBranding'

vi.mock('./brandingApi', async (loadOriginal) => {
  const original = await loadOriginal<typeof import('./brandingApi')>()
  return {
    ...original,
    getBranding: vi.fn().mockResolvedValue({
      ...original.defaultBranding,
      organizationName: 'Custom University',
      primaryColor: '#112233',
      secondaryColor: '#445566',
      headerColor: '#102030',
      backgroundColor: '#eef0f2',
      patternColor: '#663399',
      backgroundPattern: 'Geometric',
      faviconUrl: 'https://assets.example/favicon.png',
      version: 2,
    }),
  }
})

function BrandingProbe() {
  const branding = useBranding()
  return <span>{branding.organizationName}</span>
}

describe('BrandingProvider', () => {
  it('hydrates runtime branding and updates browser assets', async () => {
    const favicon = document.createElement('link')
    favicon.rel = 'icon'
    document.head.append(favicon)
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })

    render(
      <QueryClientProvider client={client}>
        <BrandingProvider>
          <BrandingProbe />
        </BrandingProvider>
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Custom University')).toBeInTheDocument()
    await waitFor(() => {
      expect(
        document.documentElement.style.getPropertyValue('--brand-secondary'),
      ).toBe('#445566')
      expect(
        document.documentElement.style.getPropertyValue('--header-background'),
      ).toBe('#102030')
      expect(
        document.documentElement.style.getPropertyValue('--page-background'),
      ).toBe('#eef0f2')
      expect(
        document.documentElement.style.getPropertyValue('--pattern-color'),
      ).toBe('#663399')
      expect(document.documentElement.dataset.backgroundPattern).toBe(
        'geometric',
      )
      expect(favicon.href).toBe('https://assets.example/favicon.png')
    })
  })
})
