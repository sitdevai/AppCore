import type { ThemeConfig } from 'antd'
import type { Branding } from '@/features/branding/brandingApi'

export const createApplicationTheme = (branding: Branding): ThemeConfig => ({
  token: {
    colorPrimary: branding.primaryColor,
    colorInfo: branding.primaryColor,
    colorLink: branding.primaryColor,
    borderRadius: 8,
    fontFamily: '"Noto Sans Arabic", "Segoe UI", Tahoma, Arial, sans-serif',
  },
  components: {
    Layout: {
      headerBg: branding.headerColor,
      bodyBg: branding.backgroundColor,
    },
    Menu: {
      horizontalItemSelectedColor: branding.primaryColor,
      itemBorderRadius: 6,
    },
  },
})
