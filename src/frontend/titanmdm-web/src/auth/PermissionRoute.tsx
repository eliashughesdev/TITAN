import type {
  ReactNode,
} from 'react'

import {
  Navigate,
  useLocation,
} from 'react-router-dom'

import {
  useAuth,
} from './AuthContext'

import {
  canUseHelpdeskConsole,
} from './helpdeskAccess'

interface PermissionRouteProps {
  children: ReactNode

  anyOf?: string[]

  allOf?: string[]

  /*
   * Cuando true, esta ruta pertenece
   * exclusivamente a la consola TIC.
   */
  helpdeskConsole?: boolean
}

export function PermissionRoute({
  children,
  anyOf = [],
  allOf = [],
  helpdeskConsole = false,
}: PermissionRouteProps) {
  const {
    user,
    isAuthenticated,
    isLoading,
    hasPermission,
  } =
    useAuth()

  const location =
    useLocation()

  if (
    isLoading
  ) {
    return (
      <div
        className="app-loading"
      >
        <div
          className="app-loading__logo"
        >
          T
        </div>

        <div
          className="app-loading__spinner"
        />

        <p>
          Verificando autorización…
        </p>
      </div>
    )
  }

  if (
    !isAuthenticated ||
    !user
  ) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from:
            location.pathname +
            location.search,
        }}
      />
    )
  }

  /*
   * ============================================================
   * HELPDESK TIC CONSOLE
   * ============================================================
   *
   * Un colaborador jamás debe entrar
   * a una ruta TIC aunque conozca la URL.
   */
  if (
    helpdeskConsole &&
    !canUseHelpdeskConsole(
      hasPermission,
    )
  ) {
    return (
      <Navigate
        to={
          '/my-support' +
          '?workspace=helpdesk'
        }
        replace
      />
    )
  }

  /*
   * ============================================================
   * PONCHES
   * ============================================================
   */
  const ponches =
    location.pathname ===
      '/ponches'
    ||
    location.pathname
      .startsWith(
        '/ponches/',
      )

  const effectiveAnyOf =
    ponches
      ? [
          'workspace.ponches.view',
          'ponches.manage',
        ]
      : anyOf

  const hasAny =
    effectiveAnyOf.length ===
      0
    ||
    effectiveAnyOf.some(
      hasPermission,
    )

  const hasAll =
    allOf.every(
      hasPermission,
    )

  if (
    !hasAny ||
    !hasAll
  ) {
    return (
      <Navigate
        to="/forbidden"
        replace
        state={{
          from:
            location.pathname +
            location.search,
        }}
      />
    )
  }

  return (
    <>
      {children}
    </>
  )
}