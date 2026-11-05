import axios, {
  AxiosError,
  type InternalAxiosRequestConfig,
} from 'axios'

import { API_ERROR_EVENT, type ApiErrorEventDetail } from './apiEvents'
import { AUTH_EVENTS } from '../auth/jwtClaims'
import { tokenStorage } from '../auth/tokenStorage'
import type { LoginResponse } from '../types/auth'

interface RetryRequestConfig
  extends InternalAxiosRequestConfig {
  _retry?: boolean
}

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use(
  (config) => {
    const accessToken =
      tokenStorage.getAccessToken()

    if (accessToken) {
      config.headers.Authorization =
        `Bearer ${accessToken}`
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  },
)

let refreshPromise:
  Promise<LoginResponse> | null = null

async function refreshSession():
  Promise<LoginResponse> {
  const refreshToken =
    tokenStorage.getRefreshToken()

  if (!refreshToken) {
    throw new Error(
      'Refresh token is not available.',
    )
  }

  const response =
    await axios.post<LoginResponse>(
      '/api/auth/refresh',
      {
        refreshToken,
      },
      {
        headers: {
          'Content-Type': 'application/json',
        },
      },
    )

  tokenStorage.setTokens(
    response.data.accessToken,
    response.data.refreshToken,
  )

  // P1 RBAC: el token nuevo puede traer permisos distintos.
  window.dispatchEvent(
    new CustomEvent(AUTH_EVENTS.tokenRefreshed, {
      detail: response.data,
    }),
  )

  return response.data
}

apiClient.interceptors.response.use(
  (response) => response,

  async (error: AxiosError) => {
    const originalRequest =
      error.config as
        | RetryRequestConfig
        | undefined

    /*
     * Fase 3 - Manejo de errores unificado: fallos de servidor o de red
     * en operaciones de escritura se informan con un toast global.
     * Las lecturas (GET) las presenta cada pantalla en su contexto.
     */
    const status = error.response?.status
    const method = (originalRequest?.method ?? 'get').toUpperCase()

    if (
      method !== 'GET' &&
      (status === undefined || status >= 500) &&
      error.code !== 'ERR_CANCELED'
    ) {
      const detail: ApiErrorEventDetail = {
        status,
        method,
        url: originalRequest?.url,
        message:
          status === undefined
            ? 'Sin conexión con el servidor TitanMDM.'
            : `El servidor respondió ${status}. La operación no se completó.`,
      }

      window.dispatchEvent(new CustomEvent(API_ERROR_EVENT, { detail }))
    }

    /*
     * P1 RBAC: un 403 indica que la UI mostro una accion
     * que el usuario ya no puede ejecutar (rol cambiado).
     * Se solicita resincronizar permisos.
     */
    if (error.response?.status === 403) {
      window.dispatchEvent(
        new Event(AUTH_EVENTS.permissionsStale),
      )

      return Promise.reject(error)
    }

    if (
      error.response?.status !== 401 ||
      !originalRequest ||
      originalRequest._retry
    ) {
      return Promise.reject(error)
    }

    const refreshToken =
      tokenStorage.getRefreshToken()

    if (!refreshToken) {
      tokenStorage.clear()

      return Promise.reject(error)
    }

    originalRequest._retry = true

    try {
      if (!refreshPromise) {
        refreshPromise =
          refreshSession().finally(() => {
            refreshPromise = null
          })
      }

      const refreshed =
        await refreshPromise

      originalRequest.headers.Authorization =
        `Bearer ${refreshed.accessToken}`

      return apiClient(originalRequest)
    } catch (refreshError) {
      tokenStorage.clear()

      window.dispatchEvent(
        new Event(
          'titanmdm:session-expired',
        ),
      )

      return Promise.reject(refreshError)
    }
  },
)

export default apiClient