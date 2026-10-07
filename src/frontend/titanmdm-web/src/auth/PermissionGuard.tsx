import type { ReactNode } from 'react'

import { useAuth } from './AuthContext'

interface PermissionGuardProps {
  permission: string
  children: ReactNode
  fallback?: ReactNode
}

export function PermissionGuard({
  permission,
  children,
  fallback = null,
}: PermissionGuardProps) {
  const { hasPermission } =
    useAuth()

  if (!hasPermission(permission)) {
    return <>{fallback}</>
  }

  return <>{children}</>
}