import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/layouts/AppShell'
import { RouteErrorPage } from '@/pages/status/RouteErrorPage'
import { RequirePermission } from '@/features/authorization/RequirePermission'
import { permissions } from '@/features/authorization/permissions'
import { RouteViewport } from '@/app/RouteViewport'

export const router = createBrowserRouter([
  {
    Component: RouteViewport,
    children: [
      {
        path: '/login',
        lazy: () => import('@/pages/authentication/LoginPage'),
      },
      {
        path: '/activation',
        lazy: () => import('@/pages/authentication/ChallengePage'),
      },
      {
        path: '/password-reset',
        lazy: () => import('@/pages/authentication/ChallengePage'),
      },
      {
        path: '/recovery',
        lazy: () => import('@/pages/authentication/RecoveryPage'),
      },
      {
        path: '/error',
        Component: RouteErrorPage,
      },
      {
        path: '/',
        Component: AppShell,
        ErrorBoundary: RouteErrorPage,
        children: [
          {
            index: true,
            lazy: () => import('@/pages/home/HomePage'),
          },
          {
            path: 'patterns',
            lazy: () => import('@/pages/patterns/PatternsPage'),
          },
          {
            path: 'errors',
            lazy: () => import('@/pages/administration/ErrorHistoryPage'),
          },
          {
            path: 'account/security',
            lazy: () => import('@/pages/authentication/SecurityPage'),
          },
          {
            Component: () => (
              <RequirePermission permission={permissions.usersView} />
            ),
            children: [
              {
                path: 'administration/users',
                lazy: () => import('@/pages/administration/UsersPage'),
              },
            ],
          },
          {
            Component: () => (
              <RequirePermission permission={permissions.rolesView} />
            ),
            children: [
              {
                path: 'administration/roles',
                lazy: () => import('@/pages/administration/RolesPage'),
              },
            ],
          },
          {
            Component: () => (
              <RequirePermission permission={permissions.sessionsViewOwn} />
            ),
            children: [
              {
                path: 'account/sessions',
                lazy: () => import('@/pages/administration/SessionsPage'),
              },
            ],
          },
          {
            Component: () => (
              <RequirePermission permission={permissions.auditSecurityView} />
            ),
            children: [
              {
                path: 'administration/security-audit',
                lazy: () => import('@/pages/administration/SecurityAuditPage'),
              },
            ],
          },
          {
            Component: () => (
              <RequirePermission
                permission={permissions.settingsVisualIdentityView}
              />
            ),
            children: [
              {
                path: 'settings/visual-identity',
                lazy: () => import('@/pages/settings/VisualIdentityPage'),
              },
            ],
          },
          {
            path: 'forbidden',
            lazy: () => import('@/pages/status/ForbiddenPage'),
          },
          {
            path: '*',
            lazy: () => import('@/pages/status/NotFoundPage'),
          },
        ],
      },
    ],
  },
])
