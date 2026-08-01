import { useTranslation } from 'react-i18next'

interface BuiltInRoleNameProps {
  name: string
}

export function BuiltInRoleName({ name }: BuiltInRoleNameProps) {
  const { t } = useTranslation('administration')

  switch (name) {
    case 'System Administrator':
      return <>{t('roleSystemAdministrator')}</>
    case 'User Administrator':
      return <>{t('roleUserAdministrator')}</>
    case 'Security Administrator':
      return <>{t('roleSecurityAdministrator')}</>
    case 'Application User':
      return <>{t('roleApplicationUser')}</>
    case 'Manager / Approver':
      return <>{t('roleManagerApprover')}</>
    case 'Auditor / Reporting User':
      return <>{t('roleAuditorReportingUser')}</>
    default:
      return <>{name}</>
  }
}
