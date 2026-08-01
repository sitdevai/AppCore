import { Flex, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'

export function LoadingState() {
  const { t } = useTranslation('common')
  return (
    <Flex vertical align="center" gap="small" className="state-panel">
      <Spin size="large" />
      <Typography.Text>{t('loading')}</Typography.Text>
    </Flex>
  )
}
