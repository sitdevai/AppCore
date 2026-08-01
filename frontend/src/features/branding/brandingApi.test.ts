import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '@/lib/api/client'
import { getBranding } from './brandingApi'

describe('branding API', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('resolves asset URLs when the API base URL is same-origin and relative', async () => {
    const originalBaseUrl = apiClient.defaults.baseURL
    apiClient.defaults.baseURL = '/api'
    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: {
        organizationName: 'University',
        shortOrganizationName: 'DU',
        primaryColor: '#111111',
        secondaryColor: '#222222',
        lightLogoUrl: '/api/v1/branding/assets/logo',
        version: 1,
      },
    })

    const branding = await getBranding()

    expect(branding.lightLogoUrl).toBe(
      'http://localhost:3000/api/v1/branding/assets/logo',
    )
    apiClient.defaults.baseURL = originalBaseUrl
  })
})
