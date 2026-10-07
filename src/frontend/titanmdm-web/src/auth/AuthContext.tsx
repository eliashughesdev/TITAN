import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import { authApi } from '../api/authApi'
import { tokenStorage } from './tokenStorage'

import type {
  AuthenticatedUser,
  LoginRequest,
} from '../types/auth'

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
      'titanmdm:session-expired',
      handleSessionExpired,
    )

    return () => {
      window.removeEventListener(
        'titanmdm:session-expired',
        handleSessionExpired,
      )
    }
  }, [clearSession])

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