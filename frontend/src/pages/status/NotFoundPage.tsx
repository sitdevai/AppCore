import { Button, Result } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

export function Component() {
  const { t } = useTranslation(['common', 'pages'])
  const navigate = useNavigate()
  return (
    <Result
      status="404"
      title={t('pages:notFoundTitle')}
      subTitle={t('pages:notFoundDescription')}
      extra={
        <Button type="primary" onClick={() => void navigate('/')}>
          {t('common:goHome')}
        </Button>
      }
    />
  )
}
