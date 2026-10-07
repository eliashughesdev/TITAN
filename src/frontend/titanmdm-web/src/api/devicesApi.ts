import apiClient from './apiClient'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
  DeviceListResult,
  DeviceOperationalSnapshot,
} from '../types/device'

export interface DeviceQueryParameters {
  search?: string
  platform?: string
  status?: string
  compliance?: string
  managed?: boolean
  sortBy?: string
  sortDirection?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

export const devicesApi = {
  async getDevices(
    parameters:
      DeviceQueryParameters = {},
  ): Promise<DeviceListResult> {
    const response =
      await apiClient
        .get<DeviceListResult>(
          '/devices',
          {
            params: {
              search:
                parameters.search
                || undefined,

              platform:
                parameters.platform
                || undefined,

              status:
                parameters.status
                || undefined,

              compliance:
                parameters.compliance
                || undefined,

              managed:
                parameters.managed,

              sortBy:
                parameters.sortBy
                || undefined,

              sortDirection:
                parameters.sortDirection
                || undefined,

              page:
                parameters.page
                ?? 1,

              pageSize:
                parameters.pageSize
                ?? 25,
            },
          },
        )

    return response.data
  },

  async getDeviceById(
    deviceId: string,
  ): Promise<DeviceDetails> {
    const response =
      await apiClient
        .get<DeviceDetails>(
          `/devices/${deviceId}`,
        )

    return response.data
  },

  async getAndroidDeviceDetails(
    deviceId: string,
  ): Promise<AndroidDeviceDetails> {
    const response =
      await apiClient
        .get<AndroidDeviceDetails>(
          `/devices/${deviceId}/android`,
        )

    return response.data
  },

  async getOperationalSnapshot(
    deviceId: string,
  ): Promise<DeviceOperationalSnapshot> {
    const response =
      await apiClient
        .get<DeviceOperationalSnapshot>(
          `/devices/${deviceId}/snapshot`,
        )

    return response.data
  },
}