import { bootstrapCsrf } from '@/features/authentication/authApi'
import { apiClient } from '@/lib/api/client'
import { brandTokens } from '@/theme/tokens'

export interface Branding {
  organizationName: string
  shortOrganizationName: string
  primaryColor: string
  secondaryColor: string
  headerColor: string
  backgroundColor: string
  patternColor: string
  backgroundPattern: 'None' | 'Dots' | 'Grid' | 'Diagonal' | 'Geometric'
  lightLogoUrl?: string
  darkLogoUrl?: string
  compactLogoUrl?: string
  faviconUrl?: string
  version: number
}

export const defaultBranding: Branding = {
  organizationName: brandTokens.organizationName,
  shortOrganizationName: brandTokens.shortOrganizationName,
  primaryColor: brandTokens.primaryColor,
  secondaryColor: brandTokens.secondaryColor,
  headerColor: brandTokens.headerColor,
  backgroundColor: brandTokens.backgroundColor,
  patternColor: brandTokens.patternColor,
  backgroundPattern: brandTokens.backgroundPattern,
  version: 1,
}

function absoluteAssets(value: Branding): Branding {
  const origin = new URL(apiClient.defaults.baseURL!, window.location.origin)
    .origin
  const asset = (url?: string) =>
    url ? new URL(url, origin).toString() : undefined
  return {
    ...value,
    lightLogoUrl: asset(value.lightLogoUrl),
    darkLogoUrl: asset(value.darkLogoUrl),
    compactLogoUrl: asset(value.compactLogoUrl),
    faviconUrl: asset(value.faviconUrl),
  }
}

export async function getBranding() {
  return absoluteAssets((await apiClient.get<Branding>('/v1/branding/')).data)
}

export async function updateBranding(value: Branding) {
  const csrf = await bootstrapCsrf()
  return absoluteAssets(
    (
      await apiClient.put<Branding>(
        '/v1/settings/visual-identity/',
        {
          organizationName: value.organizationName,
          shortOrganizationName: value.shortOrganizationName,
          primaryColor: value.primaryColor,
          secondaryColor: value.secondaryColor,
          headerColor: value.headerColor,
          backgroundColor: value.backgroundColor,
          patternColor: value.patternColor,
          backgroundPattern: value.backgroundPattern,
          expectedVersion: value.version,
          confirmed: true,
        },
        { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
      )
    ).data,
  )
}

export async function uploadBrandingAsset(kind: string, file: File) {
  const csrf = await bootstrapCsrf()
  const form = new FormData()
  form.append('file', file)
  form.append('confirmed', 'true')
  return absoluteAssets(
    (
      await apiClient.post<Branding>(
        `/v1/settings/visual-identity/assets/${kind}`,
        form,
        { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
      )
    ).data,
  )
}

export async function restoreBrandingDefaults() {
  const csrf = await bootstrapCsrf()
  return absoluteAssets(
    (
      await apiClient.post<Branding>(
        '/v1/settings/visual-identity/restore-defaults',
        { confirmed: true },
        { headers: { 'X-CSRF-TOKEN': csrf.requestToken } },
      )
    ).data,
  )
}
