import { useEffect } from 'react'
import { useBranding } from '@/features/branding/useBranding'

export function DocumentTitleManager() {
  const branding = useBranding()

  useEffect(() => {
    document.title = branding.organizationName
  }, [branding.organizationName])

  return null
}
