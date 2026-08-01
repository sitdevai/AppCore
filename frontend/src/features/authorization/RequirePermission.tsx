import { useQuery } from '@tanstack/react-query'
import { Navigate, Outlet } from 'react-router-dom'
import { getCurrentUser } from '@/features/authentication/authApi'
import { hasPermission } from './permissions'

export function RequirePermission({ permission }: { permission: string }) {
  const currentUser = useQuery({
    queryKey: ['authentication', 'current-user'],
    queryFn: getCurrentUser,
  })

  if (currentUser.isPending) return null
  return hasPermission(currentUser.data, permission) ? (
    <Outlet />
  ) : (
    <Navigate to="/forbidden" replace />
  )
}
