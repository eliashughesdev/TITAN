import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

import { authApi } from '../api/authApi'
import {
  AUTH_EVENTS,
  decodeJwtClaims,
  sameSet,
} from './jwtClaims'
import { tokenStorage } from './tokenStorage'

import type {
  AuthenticatedUser,
  LoginRequest,
  LoginResponse,
} from '../types/auth'

/* Resincronizacion periodica de permisos (5 min). */
const PERMISSION_SYNC_INTERVAL_MS = 5 * 60 * 1000

/* Evita rafagas de /me ante varios 403 o eventos de foco. */
const PERMISSION_SYNC_THROTTLE_MS = 15 * 1000

interface AuthContextValue {
  user: AuthenticatedUser | null
  isAuthenticated: boolean
  isLoading: boolean

  login: (
    request: LoginRequest,
  ) => Promise<void>

  logout: () => Promise<void>

  hasPermission: (
    permission: string,
  ) => boolean

  hasRole: (
    role: string,
  ) => boolean
}

const AuthContext =
  createContext<AuthContextValue | undefined>(
    undefined,
  )

interface AuthProviderProps {
  children: ReactNode
}

export function AuthProvider({
  children,
}: AuthProviderProps) {
  const [user, setUser] =
    useState<AuthenticatedUser | null>(
      null,
    )

  const [isLoading, setIsLoading] =
    useState(true)

  const clearSession =
    useCallback(() => {
      tokenStorage.clear()
      setUser(null)
    }, [])

  const restoreSession =
    useCallback(async () => {
      const accessToken =
        tokenStorage.getAccessToken()

      const refreshToken =
        tokenStorage.getRefreshToken()

      if (
        !accessToken &&
        !refreshToken
      ) {
        setIsLoading(false)
        return
      }

      try {
        const currentUser =
          await authApi.me()

        setUser(currentUser)
      } catch {
        clearSession()
      } finally {
        setIsLoading(false)
      }
    }, [clearSession])

  useEffect(() => {
    void restoreSession()
  }, [restoreSession])

  useEffect(() => {
    const handleSessionExpired = () => {
      clearSession()
    }

    window.addEventListener(
      AUTH_EVENTS.sessionExpired,
      handleSessionExpired,
    )

    return () => {
      window.removeEventListener(
        AUTH_EVENTS.sessionExpired,
        handleSessionExpired,
      )
    }
  }, [clearSession])

  /*
   * P1 RBAC - Sincronizacion de UI con permisos.
   *
   * Los menus, rutas y botones dependen de user.permissions.
   * Se mantienen alineados con el backend cuando:
   *   - se renueva el access token (claims nuevos);
   *   - el API responde 403 (rol revocado);
   *   - la pestana recupera el foco;
   *   - cada PERMISSION_SYNC_INTERVAL_MS.
   */
  const applyAuthorization =
    useCallback(
      (
        permissions: readonly string[],
        roles: readonly string[],
      ) => {
        setUser((current) => {
          if (
            !current ||
            (sameSet(current.permissions, permissions) &&
              sameSet(current.roles, roles))
          ) {
            return current
          }

          return {
            ...current,
            permissions: [...permissions],
            roles: [...roles],
          }
        })
      },
      [],
    )

  const lastSyncRef = useRef(0)
  const syncingRef = useRef(false)

  const syncPermissions =
    useCallback(
      async (force = false) => {
        if (!tokenStorage.getAccessToken()) {
          return
        }

        const now = Date.now()

        if (
          syncingRef.current ||
          (!force &&
            now - lastSyncRef.current <
              PERMISSION_SYNC_THROTTLE_MS)
        ) {
          return
        }

        syncingRef.current = true
        lastSyncRef.current = now

        try {
          const current = await authApi.me()
          applyAuthorization(
            current.permissions,
            current.roles,
          )
        } catch {
          // 401 lo resuelve apiClient (refresh o sesion expirada).
        } finally {
          syncingRef.current = false
        }
      },
      [applyAuthorization],
    )

  const hasSession = user !== null

  useEffect(() => {
    if (!hasSession) {
      return
    }

    const handleTokenRefreshed = (event: Event) => {
      const detail =
        (event as CustomEvent<LoginResponse | undefined>)
          .detail

      if (detail?.user) {
        applyAuthorization(
          detail.user.permissions,
          detail.user.roles,
        )
        return
      }

      const claims = decodeJwtClaims(
        tokenStorage.getAccessToken(),
      )

      if (claims) {
        applyAuthorization(
          claims.permissions,
          claims.roles,
        )
      }
    }

    const handleStale = () => {
      void syncPermissions()
    }

    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        void syncPermissions()
      }
    }

    window.addEventListener(
      AUTH_EVENTS.tokenRefreshed,
      handleTokenRefreshed,
    )
    window.addEventListener(
      AUTH_EVENTS.permissionsStale,
      handleStale,
    )
    window.addEventListener('focus', handleStale)
    document.addEventListener(
      'visibilitychange',
      handleVisibility,
    )

    const interval = window.setInterval(
      () => void syncPermissions(true),
      PERMISSION_SYNC_INTERVAL_MS,
    )

    return () => {
      window.removeEventListener(
        AUTH_EVENTS.tokenRefreshed,
        handleTokenRefreshed,
      )
      window.removeEventListener(
        AUTH_EVENTS.permissionsStale,
        handleStale,
      )
      window.removeEventListener('focus', handleStale)
      document.removeEventListener(
        'visibilitychange',
        handleVisibility,
      )
      window.clearInterval(interval)
    }
  }, [hasSession, applyAuthorization, syncPermissions])

  const login =
    useCallback(
      async (
        request: LoginRequest,
      ) => {
        const result =
          await authApi.login(request)

        tokenStorage.setTokens(
          result.accessToken,
          result.refreshToken,
        )

        setUser(result.user)
      },
      [],
    )

  const logout =
    useCallback(async () => {
      const refreshToken =
        tokenStorage.getRefreshToken()

      try {
        if (refreshToken) {
          await authApi.logout(
            refreshToken,
          )
        }
      } finally {
        clearSession()
      }
    }, [clearSession])

  const hasPermission =
    useCallback(
      (permission: string) => {
        return (
          user?.permissions.includes(
            permission,
          ) ?? false
        )
      },
      [user],
    )

  const hasRole =
    useCallback(
      (role: string) => {
        return (
          user?.roles.includes(role) ??
          false
        )
      },
      [user],
    )

  const value =
    useMemo<AuthContextValue>(
      () => ({
        user,
        isAuthenticated:
          user !== null,
        isLoading,
        login,
        logout,
        hasPermission,
        hasRole,
      }),
      [
        user,
        isLoading,
        login,
        logout,
        hasPermission,
        hasRole,
      ],
    )

  return (
    <AuthContext.Provider
      value={value}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth():
  AuthContextValue {
  const context =
    useContext(AuthContext)

  if (!context) {
    throw new Error(
      'useAuth must be used inside AuthProvider.',
    )
  }

  return context
}