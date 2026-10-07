import apiClient from './apiClient'

import type {
  CreateEnrollmentTokenRequest,
  CreatedEnrollmentToken,
  CreateWindowsInstallerRequest,
  EnrollmentToken,
  EnrollmentTokenValidationResult,
  WindowsAgentPackageInfo,
} from '../types/enrollment'

export const enrollmentApi = {
  async getTokens():
    Promise<EnrollmentToken[]> {
    const response =
      await apiClient.get<
        EnrollmentToken[]
      >(
        '/enrollment/tokens',
      )

    return response.data
  },

  async createToken(
    request:
      CreateEnrollmentTokenRequest,
  ): Promise<CreatedEnrollmentToken> {
    const response =
      await apiClient.post<
        CreatedEnrollmentToken
      >(
        '/enrollment/tokens',
        request,
      )

    return response.data
  },

  async revokeToken(
    enrollmentTokenId:
      string,
  ): Promise<void> {
    await apiClient.post(
      `/enrollment/tokens/${enrollmentTokenId}/revoke`,
    )
  },

  async validateToken(
    token:
      string,
    platform:
      string,
  ): Promise<EnrollmentTokenValidationResult> {
    const response =
      await apiClient.post<
        EnrollmentTokenValidationResult
      >(
        '/enrollment/validate',
        {
          token,
          platform,
        },
      )

    return response.data
  },

  async getWindowsPackageInfo():
    Promise<WindowsAgentPackageInfo> {
    const response =
      await apiClient.get<
        WindowsAgentPackageInfo
      >(
        '/enrollment/windows/package-info',
      )

    return response.data
  },

async downloadWindowsInstaller(
  request:
    CreateWindowsInstallerRequest,
): Promise<Blob> {
  const response =
    await apiClient.post(
      '/enrollment/windows/installer',
      request,
      {
        responseType:
          'blob',
      },
    )

  const contentType =
    String(
      response.headers[
        'content-type'
      ] ?? '',
    ).toLowerCase()

  if (
    contentType.includes(
      'application/json',
    )
  ) {
    const text =
      await response.data.text()

    let message =
      'TitanMDM no pudo generar el instalador Windows.'

    try {
      const payload =
        JSON.parse(text)

      if (
        typeof payload?.message ===
          'string'
        &&
        payload.message.trim()
      ) {
        message =
          payload.message
      }
    } catch {
      if (text.trim()) {
        message =
          text
      }
    }

    throw new Error(
      message,
    )
  }

  if (
    !response.data
    ||
    response.data.size ===
      0
  ) {
    throw new Error(
      'TitanMDM generó una respuesta vacía para el instalador Windows.',
    )
  }

  return response.data
}
}