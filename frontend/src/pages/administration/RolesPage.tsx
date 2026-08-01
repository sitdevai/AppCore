import { EditOutlined, MoreOutlined, PlusOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App as AntApp,
  Button,
  Drawer,
  Dropdown,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { MenuProps } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  archiveRole,
  createRole,
  listPermissions,
  listRoles,
  renameRole,
  replaceRolePermissions,
  type AdministrationRole,
} from '@/features/administration/administrationApi'
import { getCurrentUser } from '@/features/authentication/authApi'
import { BuiltInRoleName } from '@/features/authorization/BuiltInRoleName'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { ListPage } from '@/shared/patterns/ListPage'
import { PageHeader } from '@/shared/layout/PageHeader'
import {
  standardTablePagination,
  standardTableScroll,
} from '@/shared/tables/tableDefaults'
import {
  compareTableBoolean,
  compareTableNumber,
  compareTableText,
} from '@/shared/tables/tableSorting'

export function Component() {
  const { t } = useTranslation(['administration', 'common'])
  const { modal } = AntApp.useApp()
  const client = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [newRoleName, setNewRoleName] = useState('')
  const [editingRole, setEditingRole] = useState<AdministrationRole>()
  const [editingName, setEditingName] = useState('')
  const [permissionRole, setPermissionRole] = useState<AdministrationRole>()
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([])

  const roles = useQuery({
    queryKey: ['administration', 'roles'],
    queryFn: listRoles,
  })
  const catalog = useQuery({
    queryKey: ['administration', 'permissions'],
    queryFn: listPermissions,
  })
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })

  const mutation = useMutation({
    mutationFn: (action: () => Promise<unknown>) => action(),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['administration', 'roles'] })
    },
  })

  const confirmAndRun = (action: () => Promise<unknown>) => {
    modal.confirm({
      title: t('common:confirmTitle'),
      content: t('common:confirmDescription'),
      okText: t('common:confirm'),
      cancelText: t('common:cancel'),
      onOk: () => mutation.mutateAsync(action),
    })
  }

  const assuranceLabel = (value: string) => {
    switch (value) {
      case 'Standard':
        return <Tag>{t('assuranceStandard')}</Tag>
      case 'Sensitive':
        return <Tag color="processing">{t('assuranceSensitive')}</Tag>
      case 'HighRisk':
        return <Tag color="warning">{t('assuranceHighRisk')}</Tag>
      case 'Emergency':
        return <Tag color="error">{t('assuranceEmergency')}</Tag>
      default:
        return <Tag>{value}</Tag>
    }
  }

  const scopeLabel = (value: string) => {
    switch (value) {
      case 'OwnAccount':
        return t('scopeOwnAccount')
      case 'AllUsers':
        return t('scopeAllUsers')
      case 'GlobalSystem':
        return t('scopeGlobalSystem')
      case 'AssignedOrganization':
        return t('scopeAssignedOrganization')
      default:
        return value
    }
  }

  const openRoleEditor = (role: AdministrationRole) => {
    setEditingRole(role)
    setEditingName(role.name)
  }

  const openPermissionEditor = (role: AdministrationRole) => {
    setPermissionRole(role)
    setSelectedPermissions(role.permissionIds)
  }

  const roleActions = (role: AdministrationRole): MenuProps['items'] => {
    if (role.isBuiltIn || role.isProtected) {
      return [
        {
          key: 'protected',
          disabled: true,
          label: t('builtIn'),
        },
      ]
    }

    const items: MenuProps['items'] = []
    if (
      !role.isArchived &&
      hasPermission(currentUser.data, permissions.rolesUpdate)
    ) {
      items.push({
        key: 'edit',
        icon: <EditOutlined />,
        label: t('editRole'),
        onClick: () => openRoleEditor(role),
      })
    }
    if (
      !role.isArchived &&
      hasPermission(currentUser.data, permissions.permissionsAssignToRoles)
    ) {
      items.push({
        key: 'permissions',
        label: t('managePermissions'),
        onClick: () => openPermissionEditor(role),
      })
    }
    if (
      !role.isArchived &&
      hasPermission(currentUser.data, permissions.rolesArchive)
    ) {
      items.push({
        key: 'archive',
        label: t('archive'),
        danger: true,
        onClick: () => confirmAndRun(() => archiveRole(role)),
      })
    }
    return items
  }

  return (
    <>
      <PageHeader
        title={t('rolesTitle')}
        subtitle={t('rolesSubtitle')}
        actions={
          hasPermission(currentUser.data, permissions.rolesCreate) ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setCreateOpen(true)}
            >
              {t('createRole')}
            </Button>
          ) : undefined
        }
      />

      <Space orientation="vertical" size="large" className="full-width">
        <ListPage title={t('rolesTitle')}>
          <Table
            className="app-data-grid"
            rowKey="roleId"
            size="middle"
            loading={roles.isPending}
            dataSource={roles.data ?? []}
            pagination={standardTablePagination}
            scroll={standardTableScroll}
            columns={[
              {
                title: t('roleName'),
                dataIndex: 'name',
                fixed: 'start',
                sorter: (left, right) =>
                  compareTableText(left.name, right.name),
                render: (value: string) => (
                  <span className="table-primary-cell">
                    <BuiltInRoleName name={value} />
                  </span>
                ),
              },
              {
                title: t('type'),
                sorter: (left, right) =>
                  compareTableBoolean(left.isBuiltIn, right.isBuiltIn),
                render: (_, role: AdministrationRole) =>
                  role.isBuiltIn ? (
                    <Tag color="processing">{t('builtIn')}</Tag>
                  ) : (
                    <Tag>{t('custom')}</Tag>
                  ),
              },
              {
                title: t('status'),
                sorter: (left, right) =>
                  compareTableBoolean(left.isArchived, right.isArchived),
                render: (_, role: AdministrationRole) =>
                  role.isArchived ? (
                    <Tag>{t('archived')}</Tag>
                  ) : (
                    <Tag color="success">{t('active')}</Tag>
                  ),
              },
              {
                title: t('permissions'),
                sorter: (left, right) =>
                  compareTableNumber(
                    left.permissionIds.length,
                    right.permissionIds.length,
                  ),
                render: (_, role: AdministrationRole) => (
                  <Space wrap size={[4, 4]}>
                    {role.permissionIds.slice(0, 4).map((value) => (
                      <Tag key={value}>{value}</Tag>
                    ))}
                    {role.permissionIds.length > 4 && (
                      <Tag>+{role.permissionIds.length - 4}</Tag>
                    )}
                  </Space>
                ),
              },
              {
                title: t('common:actions'),
                key: 'actions',
                fixed: 'end',
                width: 96,
                align: 'center',
                render: (_, role: AdministrationRole) => (
                  <Dropdown
                    trigger={['click']}
                    menu={{ items: roleActions(role) }}
                  >
                    <Button
                      type="text"
                      icon={<MoreOutlined />}
                      aria-label={`${t('common:actions')} ${role.name}`}
                    />
                  </Dropdown>
                ),
              },
            ]}
          />
        </ListPage>

        <ListPage title={t('permissionCatalog')}>
          <Table
            className="app-data-grid"
            rowKey="permissionId"
            size="middle"
            loading={catalog.isPending}
            dataSource={catalog.data ?? []}
            pagination={standardTablePagination}
            scroll={standardTableScroll}
            columns={[
              {
                title: t('permission'),
                dataIndex: 'permissionId',
                sorter: (left, right) =>
                  compareTableText(left.permissionId, right.permissionId),
                render: (value: string) => (
                  <span className="table-primary-cell">{value}</span>
                ),
              },
              {
                title: t('assurance'),
                dataIndex: 'assurance',
                sorter: (left, right) =>
                  compareTableText(left.assurance, right.assurance),
                render: assuranceLabel,
              },
              {
                title: t('scope'),
                dataIndex: 'scope',
                sorter: (left, right) =>
                  compareTableText(left.scope, right.scope),
                render: scopeLabel,
              },
            ]}
          />
        </ListPage>
      </Space>

      <Modal
        className="admin-modal"
        open={createOpen}
        title={t('createRole')}
        onCancel={() => {
          setCreateOpen(false)
          setNewRoleName('')
        }}
        footer={[
          <Button
            key="cancel"
            onClick={() => {
              setCreateOpen(false)
              setNewRoleName('')
            }}
          >
            {t('common:cancel')}
          </Button>,
          <Button
            key="save"
            type="primary"
            disabled={!newRoleName.trim()}
            loading={mutation.isPending}
            onClick={() =>
              mutation.mutate(() => createRole(newRoleName), {
                onSuccess: () => {
                  setCreateOpen(false)
                  setNewRoleName('')
                },
              })
            }
          >
            {t('createRole')}
          </Button>,
        ]}
      >
        <Typography.Paragraph type="secondary">
          {t('createRoleDescription')}
        </Typography.Paragraph>
        <Input
          aria-label={t('roleName')}
          value={newRoleName}
          onChange={(event) => setNewRoleName(event.target.value)}
        />
      </Modal>

      <Modal
        className="admin-modal"
        open={Boolean(editingRole)}
        title={t('editRole')}
        onCancel={() => setEditingRole(undefined)}
        footer={[
          <Button key="cancel" onClick={() => setEditingRole(undefined)}>
            {t('common:cancel')}
          </Button>,
          <Button
            key="save"
            type="primary"
            disabled={!editingName.trim()}
            loading={mutation.isPending}
            onClick={() => {
              if (!editingRole) return
              mutation.mutate(() => renameRole(editingRole, editingName), {
                onSuccess: () => setEditingRole(undefined),
              })
            }}
          >
            {t('common:save')}
          </Button>,
        ]}
      >
        <Input
          aria-label={t('roleName')}
          value={editingName}
          onChange={(event) => setEditingName(event.target.value)}
        />
      </Modal>

      <Drawer
        open={Boolean(permissionRole)}
        title={
          permissionRole ? (
            <>
              {t('managePermissions')} —{' '}
              <BuiltInRoleName name={permissionRole.name} />
            </>
          ) : (
            t('managePermissions')
          )
        }
        width={720}
        onClose={() => setPermissionRole(undefined)}
        extra={
          <Space>
            <Button onClick={() => setPermissionRole(undefined)}>
              {t('common:cancel')}
            </Button>
            <Button
              type="primary"
              loading={mutation.isPending}
              onClick={() => {
                if (!permissionRole) return
                mutation.mutate(
                  () =>
                    replaceRolePermissions(permissionRole, selectedPermissions),
                  { onSuccess: () => setPermissionRole(undefined) },
                )
              }}
            >
              {t('savePermissions')}
            </Button>
          </Space>
        }
      >
        <Select
          mode="multiple"
          className="full-width"
          aria-label={t('permissions')}
          value={selectedPermissions}
          onChange={setSelectedPermissions}
          optionFilterProp="label"
          options={(catalog.data ?? []).map((value) => ({
            value: value.permissionId,
            label: value.permissionId,
          }))}
        />
      </Drawer>
    </>
  )
}
