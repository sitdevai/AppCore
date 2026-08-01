import { useContext } from 'react'
import { BrandingContext } from './BrandingProvider'

export const useBranding = () => useContext(BrandingContext)
