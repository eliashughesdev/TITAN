import axios, {
  AxiosError,
  type InternalAxiosRequestConfig,
} from 'axios'

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

  return response.data
}

apiClient.interceptors.response.use(
  (response) => response,

  async (error: AxiosError) => {
    const originalRequest =
      error.config as
        | RetryRequestConfig
        | undefined

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