import { Flex, Typography } from 'antd'
import { useId, type ReactNode } from 'react'

interface PageHeaderProps {
  title: ReactNode
  subtitle?: ReactNode
  actions?: ReactNode
}

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  const titleId = useId()

  return (
    <section className="page-header" aria-labelledby={titleId}>
      <Flex
        className="page-header__content"
        justify="space-between"
        align="center"
        gap="large"
        wrap
      >
        <div className="page-header__text">
          <Typography.Title id={titleId} level={1}>
            {title}
          </Typography.Title>
          {subtitle && (
            <Typography.Paragraph type="secondary">
              {subtitle}
            </Typography.Paragraph>
          )}
        </div>
        {actions && <div className="page-header__actions">{actions}</div>}
      </Flex>
    </section>
  )
}
