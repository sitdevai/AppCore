import { Button, Result, Space, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate, useRouteError, useSearchParams } from 'react-router-dom'

export function RouteErrorPage() {
  const { t, i18n } = useTranslation(['common', 'pages'])
  const navigate = useNavigate()
  const routeError = useRouteError()
  const [parameters] = useSearchParams()
  const status = parameters.get('status') ?? '500'
  const code = parameters.get('code') ?? 'ROUTE_ERROR'
  const errorNumber = parameters.get('errorNumber')
  const occurredAtUtc = parameters.get('occurredAtUtc')
  const correlationId = parameters.get('correlationId')
  const isArabic = i18n.language.startsWith('ar')

  // Keep the thrown route error out of the UI; only safe diagnostic codes are shown.
  void routeError

  return (
    <Result
      status="500"
      title={t('pages:routeErrorTitle')}
      subTitle={
        <Space orientation="vertical" size="small">
          <Typography.Text>{t('pages:routeErrorDescription')}</Typography.Text>
          <Typography.Text code>
            {isArabic ? 'رمز الخطأ' : 'Error code'}: {code}
          </Typography.Text>
          {errorNumber && (
            <Typography.Text copyable code>
              {isArabic ? 'رقم الخطأ' : 'Error number'}: {errorNumber}
            </Typography.Text>
          )}
          {occurredAtUtc && (
            <Typography.Text>
              {isArabic ? 'التاريخ' : 'Date'}:{' '}
              {new Intl.DateTimeFormat(i18n.language, {
                dateStyle: 'medium',
                timeStyle: 'medium',
              }).format(new Date(occurredAtUtc))}
            </Typography.Text>
          )}
          <Typography.Text code>HTTP: {status}</Typography.Text>
          {correlationId && (
            <Typography.Text copyable code>
              {isArabic ? 'معرف الارتباط' : 'Correlation ID'}: {correlationId}
            </Typography.Text>
          )}
        </Space>
      }
      extra={
        <Space>
          <Button type="primary" onClick={() => void navigate('/')}>
            {t('common:goHome')}
          </Button>
          <Button onClick={() => void navigate('/errors')}>
            {isArabic ? 'سجل الأخطاء' : 'Error history'}
          </Button>
        </Space>
      }
    />
  )
}
