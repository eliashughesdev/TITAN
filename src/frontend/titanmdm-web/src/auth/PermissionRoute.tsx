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

/*
 * ================================================================
 * WORKSPACE RESOLUTION
 * ================================================================
 *
 * Esta capa evita depender únicamente de App.tsx.
 *
 * Si alguien escribe una URL manualmente, TitanMDM determina
 * qué workspace corresponde a esa ruta y verifica su permiso.
 */

function resolveRequiredWorkspacePermission(
  pathname: string,
  search: string,
): string | null {
  const params =
    new URLSearchParams(
      search,
    )

  const requestedWorkspace =
    params
      .get(
        'workspace',
      )
      ?.trim()
      .toLowerCase()

  if (
    requestedWorkspace ===
      'windows'
  ) {
    return 'workspace.windows.view'
  }

  if (
    requestedWorkspace ===
      'android'
  ) {
    return 'workspace.android.view'
  }

  if (
    requestedWorkspace ===
      'helpdesk'
  ) {
    return 'workspace.helpdesk.view'
  }

  if (
    requestedWorkspace ===
      'administration'
  ) {
    return 'workspace.administration.view'
  }

  if (
    requestedWorkspace ===
      'ponches'
  ) {
    return 'workspace.ponches.view'
  }

  /*
   * Rutas que pertenecen inequívocamente
   * a un Workspace incluso sin querystring.
   */

  if (
    pathname ===
      '/remote'
    ||
    pathname.startsWith(
      '/remote/',
    )
    ||
    pathname.includes(
      '/control-center',
    )
  ) {
    return 'workspace.windows.view'
  }

  if (
    pathname ===
      '/kiosk'
    ||
    pathname.startsWith(
      '/kiosk/',
    )
    ||
    pathname ===
      '/geofencing'
    ||
    pathname.startsWith(
      '/geofencing/',
    )
  ) {
    return 'workspace.android.view'
  }

  if (
    pathname ===
      '/settings'
    ||
    pathname.startsWith(
      '/settings/',
    )
    ||
    pathname ===
      '/users'
    ||
    pathname.startsWith(
      '/users/',
    )
    ||
    pathname ===
      '/roles'
    ||
    pathname.startsWith(
      '/roles/',
    )
    ||
    pathname ===
      '/sites'
    ||
    pathname.startsWith(
      '/sites/',
    )
    ||
    pathname ===
      '/audit'
    ||
    pathname.startsWith(
      '/audit/',
    )
  ) {
    return 'workspace.administration.view'
  }

  if (
    pathname ===
      '/helpdesk'
    ||
    pathname.startsWith(
      '/helpdesk/',
    )
    ||
    pathname ===
      '/my-support'
    ||
    pathname.startsWith(
      '/my-support/',
    )
  ) {
    return 'workspace.helpdesk.view'
  }

  if (
    pathname ===
      '/ponches'
    ||
    pathname.startsWith(
      '/ponches/',
    )
  ) {
    return 'workspace.ponches.view'
  }

  return null
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