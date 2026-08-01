import { Button, Popconfirm } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

interface ConfirmActionProps {
  children?: ReactNode
  onConfirm: () => void
}

export function ConfirmAction({ children, onConfirm }: ConfirmActionProps) {
  const { t } = useTranslation('common')
  return (
    <Popconfirm
      title={t('confirmTitle')}
      description={t('confirmDescription')}
      okText={t('confirm')}
      cancelText={t('cancel')}
      onConfirm={onConfirm}
    >
      {children ?? <Button>{t('confirm')}</Button>}
    </Popconfirm>
  )
}
