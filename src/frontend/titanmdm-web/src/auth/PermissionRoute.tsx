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
  canUseHelpdeskPortal,
} from './helpdeskAccess'

import {
  resolveRequiredWorkspacePermission,
} from './workspaceAccess'

interface PermissionRouteProps {
  children: ReactNode

  anyOf?: string[]

  allOf?: string[]

  /*
   * Cuando true, la ruta pertenece exclusivamente
   * a la consola TIC de Helpdesk.
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

  /*
   * ============================================================
   * AUTH LOADING
   * ============================================================
   */

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

  /*
   * ============================================================
   * AUTHENTICATION
   * ============================================================
   */

  if (
    !isAuthenticated
    ||
    !user
  ) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from:
            location.pathname
            +
            location.search,
        }}
      />
    )
  }

  /*
   * ============================================================
   * WORKSPACE ACCESS
   * ============================================================
   */

  const requiredWorkspacePermission =
    resolveRequiredWorkspacePermission(
      location.pathname,
      location.search,
    )

  /*
   * Ponches mantiene compatibilidad temporal
   * con el permiso legado ponches.manage.
   */

  const hasWorkspaceAccess =
    requiredWorkspacePermission ===
      null
    ||
    hasPermission(
      requiredWorkspacePermission,
    )
    ||
    (
      requiredWorkspacePermission ===
        'workspace.ponches.view'
      &&
      hasPermission(
        'ponches.manage',
      )
    )
    ||
    (
      requiredWorkspacePermission ===
        'workspace.helpdesk.view'
      &&
      (
        canUseHelpdeskPortal(
          hasPermission,
        )
        ||
        canUseHelpdeskConsole(
          hasPermission,
        )
      )
    )

  if (
    !hasWorkspaceAccess
  ) {
    return (
      <Navigate
        to="/forbidden"
        replace
        state={{
          reason:
            'workspace',

          from:
            location.pathname
            +
            location.search,
        }}
      />
    )
  }

  /*
   * ============================================================
   * HELPDESK TIC CONSOLE
   * ============================================================
   */

  if (
    helpdeskConsole
    &&
    !canUseHelpdeskConsole(
      hasPermission,
    )
  ) {
    /*
     * Si es colaborador Helpdesk,
     * lo enviamos a su portal.
     */

    if (
      canUseHelpdeskPortal(
        hasPermission,
      )
    ) {
      return (
        <Navigate
          to="/my-support?workspace=helpdesk"
          replace
        />
      )
    }

    return (
      <Navigate
        to="/forbidden"
        replace
        state={{
          reason:
            'permission',

          from:
            location.pathname
            +
            location.search,
        }}
      />
    )
  }

  /*
   * ============================================================
   * PERMISSIONS
   * ============================================================
   */

  const hasAny =
    anyOf.length ===
      0
    ||
    anyOf.some(
      permission =>
        hasPermission(
          permission,
        ),
    )

  const hasAll =
    allOf.every(
      permission =>
        hasPermission(
          permission,
        ),
    )

  if (
    !hasAny
    ||
    !hasAll
  ) {
    return (
      <Navigate
        to="/forbidden"
        replace
        state={{
          reason:
            'permission',

          from:
            location.pathname
            +
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
