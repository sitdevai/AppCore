import { DeleteOutlined, GlobalOutlined } from '@ant-design/icons'
import { Button, Input, Space, Table, Tag } from 'antd'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  listOwnSessions,
  listUserSessions,
  revokeAllSessions,
  revokeGlobalSessions,
  revokeSession,
  type SessionRecord,
} from '@/features/administration/securityAdministrationApi'
import { getCurrentUser } from '@/features/authentication/authApi'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { ConfirmAction } from '@/shared/feedback/ConfirmAction'
import { ListPage } from '@/shared/patterns/ListPage'
import { PageHeader } from '@/shared/layout/PageHeader'
import { resetAuthenticationState } from '@/lib/queryClient'
import {
  standardTablePagination,
  standardTableScroll,
} from '@/shared/tables/tableDefaults'
import {
  compareTableBoolean,
  compareTableDate,
  compareTableText,
} from '@/shared/tables/tableSorting'

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function Component() {
  const { t, i18n } = useTranslation(['securityAdministration', 'common'])
  const client = useQueryClient()
  const navigate = useNavigate()
  const [targetUserId, setTargetUserId] = useState('')
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })
  const own = useQuery({
    queryKey: ['sessions', 'own'],
    queryFn: listOwnSessions,
  })
  const target = useQuery({
    queryKey: ['sessions', 'user', targetUserId],
    queryFn: () => listUserSessions(targetUserId),
    enabled:
      guidPattern.test(targetUserId) &&
      hasPermission(currentUser.data, permissions.sessionsViewForUser),
  })
  const mutation = useMutation({
    mutationFn: async (value: {
      action: () => Promise<unknown>
      signsOut?: boolean
    }) => {
      await value.action()
      return value.signsOut === true
    },
    onSuccess: async (signsOut) => {
      if (signsOut) {
        await resetAuthenticationState()
        void navigate('/login', { replace: true })
        return
      }
      await client.invalidateQueries({ queryKey: ['sessions'] })
    },
  })

  const date = (value: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value))

  const columns = (isOwn: boolean) => [
    {
      title: t('created'),
      dataIndex: 'createdAtUtc',
      sorter: (left: SessionRecord, right: SessionRecord) =>
        compareTableDate(left.createdAtUtc, right.createdAtUtc),
      render: date,
    },
    {
      title: t('lastActivity'),
      dataIndex: 'lastActivityAtUtc',
      sorter: (left: SessionRecord, right: SessionRecord) =>
        compareTableDate(left.lastActivityAtUtc, right.lastActivityAtUtc),
      render: date,
    },
    {
      title: t('expires'),
      dataIndex: 'absoluteExpiresAtUtc',
      sorter: (left: SessionRecord, right: SessionRecord) =>
        compareTableDate(left.absoluteExpiresAtUtc, right.absoluteExpiresAtUtc),
      render: date,
    },
    {
      title: t('authenticationMethods'),
      dataIndex: 'authenticationMethods',
      sorter: (left: SessionRecord, right: SessionRecord) =>
        compareTableText(
          left.authenticationMethods,
          right.authenticationMethods,
        ),
      render: (value: string) => (
        <Space wrap size={[4, 4]}>
          {value.split(',').map((method) => (
            <Tag key={method}>{method.trim()}</Tag>
          ))}
        </Space>
      ),
    },
    {
      title: t('status'),
      sorter: (left: SessionRecord, right: SessionRecord) =>
        compareTableBoolean(left.isCurrent, right.isCurrent),
      render: (_: unknown, value: SessionRecord) =>
        value.isCurrent ? <Tag color="success">{t('current')}</Tag> : null,
    },
    {
      title: t('common:actions'),
      key: 'actions',
      fixed: 'end' as const,
      width: 110,
      align: 'center' as const,
      render: (_: unknown, value: SessionRecord) => {
        const canRevoke = hasPermission(
          currentUser.data,
          isOwn
            ? permissions.sessionsRevokeOwn
            : permissions.sessionsRevokeForUser,
        )
        if (!canRevoke || (isOwn && value.isCurrent)) return null
        return (
          <ConfirmAction
            onConfirm={() =>
              mutation.mutate({
                action: () => revokeSession(value, isOwn),
              })
            }
          >
            <Button danger type="link" icon={<DeleteOutlined />}>
              {t('revoke')}
            </Button>
          </ConfirmAction>
        )
      },
    },
  ]

  const canViewUserSessions = hasPermission(
    currentUser.data,
    permissions.sessionsViewForUser,
  )
  const validTarget = guidPattern.test(targetUserId)

  return (
    <>
      <PageHeader
        title={t('sessionsTitle')}
        subtitle={t('sessionsSubtitle')}
        actions={
          hasPermission(currentUser.data, permissions.sessionsRevokeGlobal) ? (
            <ConfirmAction
              onConfirm={() =>
                mutation.mutate({
                  action: revokeGlobalSessions,
                  signsOut: true,
                })
              }
            >
              <Button danger type="primary" icon={<GlobalOutlined />}>
                {t('revokeGlobal')}
              </Button>
            </ConfirmAction>
          ) : undefined
        }
      />

      <Space orientation="vertical" size="large" className="full-width">
        <ListPage
          title={t('ownSessions')}
          toolbar={
            hasPermission(currentUser.data, permissions.sessionsRevokeOwn) ? (
              <ConfirmAction
                onConfirm={() =>
                  mutation.mutate({
                    action: () => revokeAllSessions(),
                  })
                }
              >
                <Button danger icon={<DeleteOutlined />}>
                  {t('revokeAllOwn')}
                </Button>
              </ConfirmAction>
            ) : undefined
          }
        >
          <Table
            className="app-data-grid"
            rowKey="sessionId"
            size="middle"
            loading={own.isPending}
            dataSource={own.data ?? []}
            columns={columns(true)}
            pagination={standardTablePagination}
            scroll={standardTableScroll}
          />
        </ListPage>

        {canViewUserSessions && (
          <ListPage
            title={t('userSessions')}
            toolbar={
              <Space wrap>
                <Input
                  value={targetUserId}
                  onChange={(event) =>
                    setTargetUserId(event.target.value.trim())
                  }
                  placeholder={t('targetUserId')}
                  aria-label={t('targetUserId')}
                  status={targetUserId && !validTarget ? 'error' : undefined}
                />
                {validTarget &&
                  hasPermission(
                    currentUser.data,
                    permissions.sessionsRevokeForUser,
                  ) && (
                    <ConfirmAction
                      onConfirm={() =>
                        mutation.mutate({
                          action: () => revokeAllSessions(targetUserId),
                        })
                      }
                    >
                      <Button danger icon={<DeleteOutlined />}>
                        {t('revokeAllUser')}
                      </Button>
                    </ConfirmAction>
                  )}
              </Space>
            }
          >
            <Table
              className="app-data-grid"
              rowKey="sessionId"
              size="middle"
              loading={target.isFetching}
              dataSource={target.data ?? []}
              columns={columns(false)}
              pagination={standardTablePagination}
              scroll={standardTableScroll}
            />
          </ListPage>
        )}
      </Space>
    </>
  )
}
