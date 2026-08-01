import { Card, Flex, Input } from 'antd'
import type { PropsWithChildren, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

interface ListPageProps extends PropsWithChildren {
  title: ReactNode
  actions?: ReactNode
  toolbar?: ReactNode
  onSearch?: (value: string) => void
  searchPlaceholder?: string
}

export function ListPage({
  title,
  actions,
  toolbar,
  onSearch,
  searchPlaceholder,
  children,
}: ListPageProps) {
  const { t } = useTranslation('common')
  const hasToolbar = Boolean(onSearch || toolbar)

  return (
    <Card
      className="section-card list-page"
      title={<span className="section-card__title">{title}</span>}
      extra={actions}
      styles={{ body: { padding: 0 } }}
    >
      {hasToolbar && (
        <Flex
          className="list-toolbar"
          align="center"
          justify="space-between"
          gap="middle"
          wrap
        >
          {onSearch ? (
            <Input.Search
              allowClear
              aria-label={t('search')}
              placeholder={searchPlaceholder ?? t('search')}
              onSearch={onSearch}
            />
          ) : (
            <span />
          )}
          {toolbar && (
            <Flex gap="small" wrap>
              {toolbar}
            </Flex>
          )}
        </Flex>
      )}
      <div className="list-content">{children}</div>
    </Card>
  )
}
