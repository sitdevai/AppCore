import { App as AntApp, ConfigProvider } from 'antd'
import arEG from 'antd/locale/ar_EG'
import { useQuery } from '@tanstack/react-query'
import { createContext, useEffect, type PropsWithChildren } from 'react'
import { getBranding, defaultBranding, type Branding } from './brandingApi'
import { readableForeground } from './brandingColors'
import { createApplicationTheme } from '@/theme/theme'

const BrandingContext = createContext<Branding>(defaultBranding)

export function BrandingProvider({ children }: PropsWithChildren) {
  const query = useQuery({
    queryKey: ['branding'],
    queryFn: getBranding,
    staleTime: 15 * 60 * 1000,
    retry: 1,
  })
  const branding = query.data ?? defaultBranding

  useEffect(() => {
    document.documentElement.style.setProperty(
      '--brand-primary',
      branding.primaryColor,
    )
    document.documentElement.style.setProperty(
      '--brand-secondary',
      branding.secondaryColor,
    )
    document.documentElement.style.setProperty(
      '--header-background',
      branding.headerColor,
    )
    document.documentElement.style.setProperty(
      '--header-foreground',
      readableForeground(branding.headerColor),
    )
    document.documentElement.style.setProperty(
      '--page-background',
      branding.backgroundColor,
    )
    document.documentElement.style.setProperty(
      '--pattern-color',
      branding.patternColor,
    )
    document.documentElement.dataset.backgroundPattern =
      branding.backgroundPattern.toLowerCase()
    const favicon = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
    if (favicon && branding.faviconUrl) favicon.href = branding.faviconUrl
  }, [branding])

  return (
    <BrandingContext.Provider value={branding}>
      <ConfigProvider
        direction="rtl"
        locale={arEG}
        theme={createApplicationTheme(branding)}
      >
        <AntApp>{children}</AntApp>
      </ConfigProvider>
    </BrandingContext.Provider>
  )
}

export { BrandingContext }
