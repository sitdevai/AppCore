import {
  MoreOutlined,
  PlusOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  App as AntApp,
  Button,
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
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import {
  assignRole,
  createUser,
  issueChallenge,
  listRoles,
  listUsers,
  removeRole,
  startMfaRecovery,
  transitionUser,
  updateUserEmail,
  type AdministrationUser,
  type OneTimeChallenge,
} from '@/features/administration/administrationApi'
import { getCurrentUser } from '@/features/authentication/authApi'
import { BuiltInRoleName } from '@/features/authorization/BuiltInRoleName'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { applyApiValidationErrors } from '@/shared/forms/formErrors'
import { formMutationMeta } from '@/shared/forms/formMutation'
import { ControlledTextInput } from '@/shared/forms/ControlledTextInput'
import { FormServerError } from '@/shared/forms/FormServerError'
import { ListPage } from '@/shared/patterns/ListPage'
import { PageHeader } from '@/shared/layout/PageHeader'
import {
  standardTablePagination,
  standardTableScroll,
} from '@/shared/tables/tableDefaults'
import { compareTableText } from '@/shared/tables/tableSorting'

export function Component() {
  const { t } = useTranslation(['administration', 'common'])
  const { modal } = AntApp.useApp()
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [challenge, setChallenge] = useState<OneTimeChallenge>()
  const [createOpen, setCreateOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<AdministrationUser>()
  const [editingEmail, setEditingEmail] = useState('')
  const [roleUser, setRoleUser] = useState<AdministrationUser>()
  const [selectedRole, setSelectedRole] = useState<string>()

  const users = useQuery({
    queryKey: ['administration', 'users', search],
    queryFn: () => listUsers(search),
  })
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })
  const roles = useQuery({
    queryKey: ['administration', 'roles'],
    queryFn: listRoles,
    enabled:
      hasPermission(currentUser.data, permissions.rolesAssignToUsers) ||
      hasPermission(currentUser.data, permissions.usersView),
  })

  const schema = z.object({
    username: z.string().trim().min(1, t('common:requiredField')),
    email: z.string().trim().email().or(z.literal('')),
  })
  type Values = z.infer<typeof schema>
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { username: '', email: '' },
  })

  const refresh = async () =>
    queryClient.invalidateQueries({ queryKey: ['administration', 'users'] })

  const createMutation = useMutation({
    mutationFn: createUser,
    meta: formMutationMeta,
    onSuccess: async () => {
      form.reset()
      setCreateOpen(false)
      await refresh()
    },
    onError: (error) =>
      applyApiValidationErrors(error, form.setError, {
        Username: 'username',
        Email: 'email',
      }),
  })

  const actionMutation = useMutation({
    mutationFn: async (action: () => Promise<unknown>) => action(),
    onSuccess: refresh,
  })

  const confirmAndRun = (action: () => Promise<unknown>) => {
    modal.confirm({
      title: t('common:confirmTitle'),
      content: t('common:confirmDescription'),
      okText: t('common:confirm'),
      cancelText: t('common:cancel'),
      onOk: () => actionMutation.mutateAsync(action),
    })
  }

  const accountStatus = (value: string) => {
    switch (value) {
      case 'Enabled':
        return <Tag color="success">{t('statusEnabled')}</Tag>
      case 'Disabled':
        return <Tag>{t('statusDisabled')}</Tag>
      case 'Suspended':
        return <Tag color="warning">{t('statusSuspended')}</Tag>
      case 'Archived':
        return <Tag color="default">{t('statusArchived')}</Tag>
      default:
        return <Tag>{value}</Tag>
    }
  }

  const mfaStatus = (value: string) => {
    switch (value) {
      case 'Active':
        return <Tag color="success">{t('mfaActive')}</Tag>
      case 'RecoveryPending':
        return <Tag color="warning">{t('mfaRecoveryPending')}</Tag>
      case 'NotEnrolled':
        return <Tag>{t('mfaNotEnrolled')}</Tag>
      default:
        return <Tag>{value}</Tag>
    }
  }

  const openEmailEditor = (user: AdministrationUser) => {
    setEditingUser(user)
    setEditingEmail(user.email ?? '')
  }

  const openRoleManager = (user: AdministrationUser) => {
    setRoleUser(user)
    setSelectedRole(undefined)
  }

  const userActions = (user: AdministrationUser): MenuProps['items'] => {
    if (user.isProtectedOwner) {
      return [
        {
          key: 'protected',
          disabled: true,
          label: t('common:protectedOwner'),
        },
      ]
    }

    const items: MenuProps['items'] = []
    if (hasPermission(currentUser.data, permissions.usersUpdate)) {
      items.push({
        key: 'email',
        label: t('editEmail'),
        onClick: () => openEmailEditor(user),
      })
    }
    if (hasPermission(currentUser.data, permissions.rolesAssignToUsers)) {
      items.push({
        key: 'roles',
        label: t('manageRoles'),
        onClick: () => openRoleManager(user),
      })
    }
    if (
      user.accountStatus === 'Disabled' &&
      hasPermission(currentUser.data, permissions.usersEnable)
    ) {
      items.push({
        key: 'enable',
        label: t('enable'),
        onClick: () => confirmAndRun(() => transitionUser(user, 'enable')),
      })
    }
    if (
      user.accountStatus === 'Enabled' &&
      hasPermission(currentUser.data, permissions.usersDisable)
    ) {
      items.push({
        key: 'disable',
        label: t('disable'),
        danger: true,
        onClick: () => confirmAndRun(() => transitionUser(user, 'disable')),
      })
    }
    if (
      user.accountStatus === 'Enabled' &&
      hasPermission(currentUser.data, permissions.usersSuspend)
    ) {
      items.push({
        key: 'suspend',
        label: t('suspend'),
        danger: true,
        onClick: () => confirmAndRun(() => transitionUser(user, 'suspend')),
      })
    }
    if (
      (user.accountStatus === 'Suspended' ||
        user.accountStatus === 'Archived') &&
      hasPermission(currentUser.data, permissions.usersRestore)
    ) {
      items.push({
        key: 'restore',
        label: t('restore'),
        onClick: () => confirmAndRun(() => transitionUser(user, 'restore')),
      })
    }
    if (
      user.accountStatus !== 'Archived' &&
      hasPermission(currentUser.data, permissions.usersArchive)
    ) {
      items.push({
        key: 'archive',
        label: t('archive'),
        danger: true,
        onClick: () => confirmAndRun(() => transitionUser(user, 'archive')),
      })
    }
    if (
      user.credentialStatus === 'ActivationPending' &&
      hasPermission(currentUser.data, permissions.usersIssueActivation)
    ) {
      items.push({
        key: 'activation',
        label: t('activationCode'),
        onClick: () =>
          confirmAndRun(async () =>
            setChallenge(await issueChallenge(user, 'activation')),
          ),
      })
    }
    if (
      user.credentialStatus === 'Active' &&
      hasPermission(currentUser.data, permissions.usersResetPassword)
    ) {
      items.push({
        key: 'reset',
        label: t('resetCode'),
        onClick: () =>
          confirmAndRun(async () =>
            setChallenge(await issueChallenge(user, 'password-reset')),
          ),
      })
    }
    if (
      user.mfaState === 'Active' &&
      hasPermission(currentUser.data, permissions.usersResetMfa) &&
      hasPermission(currentUser.data, permissions.usersIssueMfaRecovery)
    ) {
      items.push({
        key: 'mfa-recovery',
        label: t('mfaRecovery'),
        danger: true,
        onClick: () =>
          confirmAndRun(async () => setChallenge(await startMfaRecovery(user))),
      })
    }
    return items
  }

  return (
    <>
      <PageHeader
        title={t('usersTitle')}
        subtitle={t('usersSubtitle')}
        actions={
          hasPermission(currentUser.data, permissions.usersCreate) ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setCreateOpen(true)}
            >
              {t('createUser')}
            </Button>
          ) : undefined
        }
      />

      <Space orientation="vertical" size="large" className="full-width">
        {challenge && (
          <Alert
            type="warning"
            showIcon
            closable
            onClose={() => setChallenge(undefined)}
            title={t('oneTimeCode')}
            description={
              <Typography.Text copyable code>
                {challenge.code}
              </Typography.Text>
            }
          />
        )}

        <ListPage
          title={t('usersTitle')}
          onSearch={setSearch}
          searchPlaceholder={t('common:search')}
        >
          <Table
            className="app-data-grid"
            rowKey="userId"
            size="middle"
            loading={users.isPending}
            dataSource={users.data ?? []}
            pagination={standardTablePagination}
            scroll={standardTableScroll}
            columns={[
              {
                title: t('username'),
                dataIndex: 'username',
                fixed: 'start',
                sorter: (left, right) =>
                  compareTableText(left.username, right.username),
                render: (value: string) => (
                  <span className="table-primary-cell">{value}</span>
                ),
              },
              {
                title: t('email'),
                dataIndex: 'email',
                sorter: (left, right) =>
                  compareTableText(left.email, right.email),
                render: (value?: string) =>
                  value || <span className="table-muted-cell">—</span>,
              },
              {
                title: t('status'),
                dataIndex: 'accountStatus',
                sorter: (left, right) =>
                  compareTableText(left.accountStatus, right.accountStatus),
                render: accountStatus,
              },
              {
                title: t('mfa'),
                dataIndex: 'mfaState',
                sorter: (left, right) =>
                  compareTableText(left.mfaState, right.mfaState),
                render: mfaStatus,
              },
              {
                title: t('roles'),
                sorter: (left, right) =>
                  compareTableText(
                    [...left.roleIds].sort().join(','),
                    [...right.roleIds].sort().join(','),
                  ),
                render: (_, user: AdministrationUser) => (
                  <Space wrap size={[4, 4]}>
                    {user.roleIds.length === 0 ? (
                      <span className="table-muted-cell">—</span>
                    ) : (
                      user.roleIds.map((roleId) => {
                        const role = roles.data?.find(
                          (value) => value.roleId === roleId,
                        )
                        return (
                          <Tag key={roleId}>
                            {role ? (
                              <BuiltInRoleName name={role.name} />
                            ) : (
                              roleId
                            )}
                          </Tag>
                        )
                      })
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
                render: (_, user: AdministrationUser) => (
                  <Dropdown
                    trigger={['click']}
                    menu={{ items: userActions(user) }}
                  >
                    <Button
                      type="text"
                      icon={<MoreOutlined />}
                      aria-label={`${t('common:actions')} ${user.username}`}
                    />
                  </Dropdown>
                ),
              },
            ]}
          />
        </ListPage>
      </Space>

      <Modal
        className="admin-modal"
        open={createOpen}
        title={t('createUser')}
        onCancel={() => {
          form.reset()
          setCreateOpen(false)
        }}
        footer={[
          <Button
            key="cancel"
            onClick={() => {
              form.reset()
              setCreateOpen(false)
            }}
          >
            {t('common:cancel')}
          </Button>,
          <Button
            key="save"
            type="primary"
            loading={createMutation.isPending}
            onClick={() =>
              void form.handleSubmit((values) =>
                createMutation.mutate(values),
              )()
            }
          >
            {t('createUser')}
          </Button>,
        ]}
      >
        <Typography.Paragraph type="secondary">
          {t('createUserDescription')}
        </Typography.Paragraph>
        <div className="modal-form">
          <FormServerError
            message={form.formState.errors.root?.server?.message}
          />
          <ControlledTextInput
            control={form.control}
            name="username"
            label={t('username')}
            required
          />
          <ControlledTextInput
            control={form.control}
            name="email"
            label={t('email')}
            inputProps={{ type: 'email' }}
          />
        </div>
      </Modal>

      <Modal
        className="admin-modal"
        open={Boolean(editingUser)}
        title={t('editEmail')}
        onCancel={() => setEditingUser(undefined)}
        footer={[
          <Button key="cancel" onClick={() => setEditingUser(undefined)}>
            {t('common:cancel')}
          </Button>,
          <Button
            key="save"
            type="primary"
            loading={actionMutation.isPending}
            onClick={() => {
              if (!editingUser) return
              actionMutation.mutate(
                () => updateUserEmail(editingUser, editingEmail),
                { onSuccess: () => setEditingUser(undefined) },
              )
            }}
          >
            {t('common:save')}
          </Button>,
        ]}
      >
        <Input
          type="email"
          aria-label={t('email')}
          value={editingEmail}
          onChange={(event) => setEditingEmail(event.target.value)}
        />
      </Modal>

      <Modal
        className="admin-modal"
        open={Boolean(roleUser)}
        title={
          roleUser
            ? `${t('manageRoles')} — ${roleUser.username}`
            : t('manageRoles')
        }
        onCancel={() => setRoleUser(undefined)}
        footer={[
          <Button key="close" onClick={() => setRoleUser(undefined)}>
            {t('common:close')}
          </Button>,
        ]}
      >
        {roleUser && (
          <Space orientation="vertical" size="large" className="full-width">
            <Space wrap>
              {roleUser.roleIds.map((roleId) => {
                const role = roles.data?.find(
                  (value) => value.roleId === roleId,
                )
                return (
                  <Tag
                    key={roleId}
                    closable
                    onClose={(event) => {
                      event.preventDefault()
                      confirmAndRun(async () => {
                        await removeRole(roleUser.userId, roleId)
                        setRoleUser(undefined)
                      })
                    }}
                  >
                    {role ? <BuiltInRoleName name={role.name} /> : roleId}
                  </Tag>
                )
              })}
            </Space>
            <Space.Compact block>
              <Select
                className="full-width"
                aria-label={t('roles')}
                value={selectedRole}
                onChange={setSelectedRole}
                options={(roles.data ?? [])
                  .filter(
                    (role) =>
                      !role.isArchived &&
                      !roleUser.roleIds.includes(role.roleId),
                  )
                  .map((role) => ({
                    value: role.roleId,
                    label: <BuiltInRoleName name={role.name} />,
                  }))}
              />
              <Button
                type="primary"
                icon={<SafetyCertificateOutlined />}
                disabled={!selectedRole}
                loading={actionMutation.isPending}
                onClick={() => {
                  const role = roles.data?.find(
                    (value) => value.roleId === selectedRole,
                  )
                  if (!role) return
                  confirmAndRun(async () => {
                    await assignRole(roleUser, role)
                    setRoleUser(undefined)
                  })
                }}
              >
                {t('assignRole')}
              </Button>
            </Space.Compact>
          </Space>
        )}
      </Modal>
    </>
  )
}
