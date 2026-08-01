import type { PropsWithChildren } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import '@/i18n'
import { queryClient } from '@/lib/queryClient'
import { DocumentTitleManager } from '@/app/DocumentTitleManager'
import { BrandingProvider } from '@/features/branding/BrandingProvider'

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <QueryClientProvider client={queryClient}>
      <BrandingProvider>
        <DocumentTitleManager />
        {children}
      </BrandingProvider>
    </QueryClientProvider>
  )
}
