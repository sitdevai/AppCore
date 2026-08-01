import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ar from '@/i18n/locales/ar'
import en from '@/i18n/locales/en'

void i18n.use(initReactI18next).init({
  resources: { ar, en },
  lng: 'ar',
  fallbackLng: 'ar',
  defaultNS: 'common',
  ns: [
    'common',
    'navigation',
    'pages',
    'auth',
    'administration',
    'securityAdministration',
    'settings',
    'validation',
  ],
  interpolation: { escapeValue: false },
  returnNull: false,
})

document.documentElement.lang = i18n.resolvedLanguage ?? 'ar'
document.documentElement.dir = 'rtl'

export default i18n
