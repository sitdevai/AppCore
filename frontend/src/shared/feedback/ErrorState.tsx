import { Button, Result } from 'antd'
import { useTranslation } from 'react-i18next'

interface ErrorStateProps {
  onRetry?: () => void
}

export function ErrorState({ onRetry }: ErrorStateProps) {
  const { t } = useTranslation('common')
  return (
    <Result
      status="error"
      title={t('errorTitle')}
      subTitle={t('errorDescription')}
      extra={
        onRetry ? (
          <Button type="primary" onClick={onRetry}>
            {t('retry')}
          </Button>
        ) : undefined
      }
    />
  )
}
