import apiClient from './apiClient'

export interface LostModeSession {
  id: string
  deviceId: string
  message: string
  phoneNumber: string | null
  status: string
  activatedAtUtc: string
  deactivatedAtUtc: string | null
}

export interface ActivateLostModeRequest {
  deviceId: string
  message: string
  phoneNumber?: string | null
}

export const lostModeApi = {
  async getActive(
    deviceId: string,
  ): Promise<LostModeSession | null> {
    try {
      const response =
        await apiClient.get<LostModeSession>(
          `/lost-mode/${deviceId}`,
        )

      return response.data
    } catch (error: unknown) {
      if (
        typeof error === 'object' &&
        error !== null &&
        'response' in error
      ) {
        const response =
          (
            error as {
              response?: {
                status?: number
              }
            }
          ).response

        if (response?.status === 404) {
          return null
        }
      }

      throw error
    }
  },

  async activate(
    request: ActivateLostModeRequest,
  ): Promise<LostModeSession> {
    const response =
      await apiClient.post<LostModeSession>(
        '/lost-mode/activate',
        request,
      )

    return response.data
  },

  async deactivate(
    deviceId: string,
  ): Promise<void> {
    await apiClient.post(
      `/lost-mode/${deviceId}/deactivate`,
    )
  },
}