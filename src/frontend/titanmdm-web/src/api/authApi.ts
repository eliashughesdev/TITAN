import apiClient from './apiClient'

import type {
  AuthenticatedUser,
  LoginRequest,
  LoginResponse,
} from '../types/auth'

export const authApi = {
  async login(
    request: LoginRequest,
  ): Promise<LoginResponse> {
    const response =
      await apiClient.post<LoginResponse>(
        '/auth/login',
        request,
      )

    return response.data
  },

  async me():
    Promise<AuthenticatedUser> {
    const response =
      await apiClient.get<AuthenticatedUser>(
        '/auth/me',
      )

    return response.data
  },

  async logout(
    refreshToken: string,
  ): Promise<void> {
    await apiClient.post(
      '/auth/logout',
      {
        refreshToken,
      },
    )
  },
}