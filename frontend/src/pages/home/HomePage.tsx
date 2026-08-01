import { CheckCircleOutlined } from '@ant-design/icons'
import { Card, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/shared/layout/PageHeader'

export function Component() {
  const { t } = useTranslation('pages')
  return (
    <>
      <PageHeader title={t('homeTitle')} subtitle={t('homeSubtitle')} />
      <Card className="section-card">
        <Typography.Title level={3}>
          <CheckCircleOutlined /> {t('foundationTitle')}
        </Typography.Title>
        <Typography.Paragraph>
          {t('foundationDescription')}
        </Typography.Paragraph>
      </Card>
    </>
  )
}
