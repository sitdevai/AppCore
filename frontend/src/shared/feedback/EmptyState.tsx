import { Empty } from 'antd'
import { useTranslation } from 'react-i18next'

export function EmptyState() {
  const { t } = useTranslation('common')
  return (
    <Empty
      className="state-panel"
      description={
        <>
          <strong>{t('emptyTitle')}</strong>
          <div>{t('emptyDescription')}</div>
        </>
      }
    />
  )
}
