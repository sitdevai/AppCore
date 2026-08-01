import {
  BellOutlined,
  HomeOutlined,
  MenuOutlined,
  UserOutlined,
} from '@ant-design/icons'
import {
  Badge,
  Breadcrumb,
  Button,
  Dropdown,
  Flex,
  Layout,
  Menu,
  Space,
  Spin,
  Typography,
} from 'antd'
import type { MenuProps } from 'antd'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Link,
  Navigate,
  Outlet,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import { QueryErrorNotifier } from '@/shared/feedback/QueryErrorNotifier'
import { getCurrentUser, logout } from '@/features/authentication/authApi'
import {
  hasPermission,
  permissions,
} from '@/features/authorization/permissions'
import { resetAuthenticationState } from '@/lib/queryClient'
import { useBranding } from '@/features/branding/useBranding'

const { Header, Content } = Layout

export function AppShell() {
  const { t, i18n } = useTranslation(['common', 'navigation'])
  const location = useLocation()
  const navigate = useNavigate()
  const branding = useBranding()
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
    retry: false,
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => void navigate('/login', { replace: true }),
  })
  useEffect(() => {
    if (currentUser.isError) {
      void resetAuthenticationState()
    }
  }, [currentUser.isError])

  useEffect(() => {
    if (currentUser.isSuccess) {
      document.getElementById('application')?.focus()
    }
  }, [currentUser.isSuccess, location.pathname])

  const navigationItems = useMemo<MenuProps['items']>(
    () => [
      { key: '/', label: t('navigation:home'), icon: <HomeOutlined /> },
      ...(hasPermission(currentUser.data, permissions.usersView)
        ? [{ key: '/administration/users', label: t('navigation:users') }]
        : []),
      ...(hasPermission(currentUser.data, permissions.rolesView)
        ? [{ key: '/administration/roles', label: t('navigation:roles') }]
        : []),
      ...(hasPermission(currentUser.data, permissions.sessionsViewOwn)
        ? [{ key: '/account/sessions', label: t('navigation:sessions') }]
        : []),
      ...(hasPermission(currentUser.data, permissions.auditSecurityView)
        ? [
            {
              key: '/administration/security-audit',
              label: t('navigation:securityAudit'),
            },
          ]
        : []),
      ...(hasPermission(
        currentUser.data,
        permissions.settingsVisualIdentityView,
      )
        ? [
            {
              key: '/settings/visual-identity',
              label: t('navigation:visualIdentity'),
            },
          ]
        : []),
      { key: '/patterns', label: t('navigation:patterns') },
      {
        key: '/errors',
        label: i18n.language.startsWith('ar') ? 'سجل الأخطاء' : 'Error history',
      },
    ],
    [currentUser.data, i18n.language, t],
  )

  const selectedKey = navigationItems
    ?.map((item) => (item && 'key' in item ? String(item.key) : ''))
    .filter((key) =>
      key === '/'
        ? location.pathname === '/'
        : location.pathname === key || location.pathname.startsWith(`${key}/`),
    )
    .sort((first, second) => second.length - first.length)[0]

  const pageLabel =
    selectedKey === '/patterns'
      ? t('navigation:patterns')
      : selectedKey === '/administration/users'
        ? t('navigation:users')
        : selectedKey === '/administration/roles'
          ? t('navigation:roles')
          : selectedKey === '/account/sessions'
            ? t('navigation:sessions')
            : selectedKey === '/administration/security-audit'
              ? t('navigation:securityAudit')
              : selectedKey === '/settings/visual-identity'
                ? t('navigation:visualIdentity')
                : selectedKey === '/'
                  ? t('navigation:home')
                  : selectedKey === '/errors'
                    ? i18n.language.startsWith('ar')
                      ? 'سجل الأخطاء'
                      : 'Error history'
                    : undefined

  const userItems: MenuProps['items'] = [
    {
      key: 'sessions',
      label: t('navigation:sessions'),
      onClick: () => void navigate('/account/sessions'),
    },
    {
      key: 'security',
      label: t('common:accountSecurity'),
      onClick: () => void navigate('/account/security'),
    },
    {
      key: 'logout',
      label: t('common:logout'),
      onClick: () => logoutMutation.mutate(),
    },
  ]

  if (currentUser.isPending) {
    return (
      <Flex align="center" justify="center" style={{ minHeight: '100vh' }}>
        <Spin size="large" aria-label={t('common:loading')} />
      </Flex>
    )
  }

  if (currentUser.isError) {
    return <Navigate to="/login" replace />
  }

  return (
    <Layout className="app-layout">
      <QueryErrorNotifier />
      <a className="skip-link" href="#application">
        {t('common:skipToContent')}
      </a>
      <Header className="app-header">
        <Flex className="app-header__row" align="center" gap="middle">
          <Link className="brand" to="/" aria-label={branding.organizationName}>
            {branding.compactLogoUrl ? (
              <img
                className="brand__logo"
                src={branding.compactLogoUrl}
                alt=""
              />
            ) : (
              <span className="brand__mark" aria-hidden="true">
                {branding.shortOrganizationName.slice(0, 1)}
              </span>
            )}
            <Typography.Text className="brand__name" strong>
              {branding.organizationName}
            </Typography.Text>
          </Link>

          <Menu
            className="primary-navigation"
            mode="horizontal"
            overflowedIndicator={<MenuOutlined />}
            items={navigationItems}
            selectedKeys={selectedKey ? [selectedKey] : []}
            onClick={({ key }) => void navigate(key)}
          />

          <Space className="header-actions">
            <Badge dot>
              <Button
                type="text"
                shape="circle"
                icon={<BellOutlined />}
                aria-label={t('common:notifications')}
              />
            </Badge>
            <Dropdown menu={{ items: userItems }} trigger={['click']}>
              <Button
                type="text"
                icon={<UserOutlined />}
                aria-label={t('common:userMenu')}
              >
                <span className="user-label">
                  {currentUser.data?.username ?? t('common:loading')}
                </span>
              </Button>
            </Dropdown>
          </Space>
        </Flex>
      </Header>

      <Content className="app-content">
        <Breadcrumb
          className="app-breadcrumb"
          items={[
            {
              title: (
                <Link to="/">
                  <HomeOutlined /> {t('navigation:breadcrumbHome')}
                </Link>
              ),
            },
            ...(selectedKey === '/' || !pageLabel
              ? []
              : [{ title: pageLabel as React.ReactNode }]),
          ]}
        />
        <main id="application" tabIndex={-1}>
          <Outlet />
        </main>
      </Content>
    </Layout>
  )
}
