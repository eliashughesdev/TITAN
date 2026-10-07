import type { ReactNode } from 'react'

import {
  Navigate,
  useLocation,
} from 'react-router-dom'

import { useAuth } from './AuthContext'

interface ProtectedRouteProps {
  children: ReactNode
}

export function ProtectedRoute({
  children,
}: ProtectedRouteProps) {
  const {
    isAuthenticated,
    isLoading,
  } = useAuth()

  const location = useLocation()

  if (isLoading) {
    return (
      <div className="app-loading">
        <div className="app-loading__logo">
          T
        </div>

        <div className="app-loading__spinner" />

        <p>Iniciando TitanMDM...</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from: location.pathname,
        }}
      />
    )
  }

  return <>{children}</>
}