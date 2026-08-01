import { ExportOutlined } from '@ant-design/icons'
import { Button, Input, Space, Table } from 'antd'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { SorterResult } from 'antd/es/table/interface'
import {
  exportAudit,
  searchAudit,
  type AuditFilters,
} from '@/features/administration/securityAdministrationApi'
import { getCurrentUser } from '@/features/authentication/authApi'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { ConfirmAction } from '@/shared/feedback/ConfirmAction'
import { ListPage } from '@/shared/patterns/ListPage'
import { PageHeader } from '@/shared/layout/PageHeader'
import {
  standardTablePagination,
  standardTableScroll,
} from '@/shared/tables/tableDefaults'

export function Component() {
  const { t, i18n } = useTranslation(['securityAdministration', 'common'])
  const [filters, setFilters] = useState<AuditFilters>({
    page: 1,
    pageSize: 20,
    sortBy: 'occurredAtUtc',
    sortDirection: 'desc',
  })
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })
  const audit = useQuery({
    queryKey: ['security-audit', filters],
    queryFn: () => searchAudit(filters),
  })
  const exportMutation = useMutation({ mutationFn: () => exportAudit(filters) })
  const update = (key: keyof AuditFilters, value: string) =>
    setFilters((current) => ({
      ...current,
      [key]: value || undefined,
      page: 1,
    }))

  return (
    <>
      <PageHeader
        title={t('auditTitle')}
        subtitle={t('auditSubtitle')}
        actions={
          hasPermission(currentUser.data, permissions.auditSecurityExport) ? (
            <ConfirmAction onConfirm={() => exportMutation.mutate()}>
              <Button
                type="primary"
                icon={<ExportOutlined />}
                loading={exportMutation.isPending}
              >
                {t('exportCsv')}
              </Button>
            </ConfirmAction>
          ) : undefined
        }
      />
      <ListPage
        title={t('auditTitle')}
        toolbar={
          <Space wrap>
            <Input
              aria-label={t('eventCode')}
              placeholder={t('eventCode')}
              onChange={(event) => update('eventCode', event.target.value)}
            />
            <Input
              aria-label={t('actorUserId')}
              placeholder={t('actorUserId')}
              onChange={(event) => update('actorUserId', event.target.value)}
            />
            <Input
              aria-label={t('targetUserId')}
              placeholder={t('targetUserId')}
              onChange={(event) => update('targetUserId', event.target.value)}
            />
          </Space>
        }
      >
        <Table
          className="app-data-grid"
          rowKey="id"
          size="middle"
          loading={audit.isPending}
          dataSource={audit.data?.items ?? []}
          scroll={standardTableScroll}
          onChange={(_, __, sorter) => {
            const selected = sorter as SorterResult<
              NonNullable<typeof audit.data>['items'][number]
            >
            if (!selected.field || !selected.order) return
            setFilters((current) => ({
              ...current,
              page: 1,
              sortBy: selected.field as AuditFilters['sortBy'],
              sortDirection: selected.order === 'ascend' ? 'asc' : 'desc',
            }))
          }}
          pagination={{
            ...standardTablePagination,
            current: audit.data?.page ?? 1,
            pageSize: audit.data?.pageSize ?? 20,
            total: audit.data?.totalCount ?? 0,
            onChange: (page, pageSize) =>
              setFilters((current) => ({ ...current, page, pageSize })),
          }}
          columns={[
            {
              title: t('occurredAt'),
              dataIndex: 'occurredAtUtc',
              sorter: true,
              sortOrder:
                filters.sortBy === 'occurredAtUtc'
                  ? filters.sortDirection === 'asc'
                    ? 'ascend'
                    : 'descend'
                  : null,
              fixed: 'start',
              render: (value: string) =>
                new Intl.DateTimeFormat(i18n.language, {
                  dateStyle: 'medium',
                  timeStyle: 'medium',
                }).format(new Date(value)),
            },
            {
              title: t('eventCode'),
              dataIndex: 'eventCode',
              sorter: true,
              render: (value: string) => (
                <span className="table-primary-cell">{value}</span>
              ),
            },
            { title: t('result'), dataIndex: 'resultCode', sorter: true },
            { title: t('actorUserId'), dataIndex: 'actorUserId', sorter: true },
            {
              title: t('targetUserId'),
              dataIndex: 'targetUserId',
              sorter: true,
            },
            { title: t('sourceIp'), dataIndex: 'sourceIp', sorter: true },
            {
              title: t('correlationId'),
              dataIndex: 'correlationId',
              sorter: true,
            },
          ]}
        />
      </ListPage>
    </>
  )
}
