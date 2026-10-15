import apiClient from './apiClient'

import type {
  AndroidAuditPage,
  AndroidAuditQuery,
  AndroidDeviceInventorySummary,
  AndroidDeviceSyncResult,
  AndroidEnterpriseStatus,
  AndroidEnrollment,
  AndroidInventoryPage,
  AndroidInventoryQuery,
  AndroidRemoteActionRequest,
  AndroidRemoteActionResponse,
  AndroidSignupResponse,
  CreateAndroidEnrollmentRequest,
  CreatedAndroidEnrollment,
} from '../types/androidEnterprise'

export const androidEnterpriseApi = {
  async getStatus(): Promise<AndroidEnterpriseStatus> {
    const response =
      await apiClient.get<AndroidEnterpriseStatus>(
        '/android-enterprise/status',
      )

    return response.data
  },

  async createSignup(): Promise<AndroidSignupResponse> {
    const response =
      await apiClient.post<AndroidSignupResponse>(
        '/android-enterprise/signup',
      )

    return response.data
  },

  async getEnrollments(): Promise<AndroidEnrollment[]> {
    const response =
      await apiClient.get<AndroidEnrollment[]>(
        '/android-enterprise/enrollments',
      )

    return response.data
  },

  async createEnrollment(
    request: CreateAndroidEnrollmentRequest,
  ): Promise<CreatedAndroidEnrollment> {
    const response =
      await apiClient.post<CreatedAndroidEnrollment>(
        '/android-enterprise/enrollments',
        request,
      )

    return response.data
  },

  async revokeEnrollment(
    id: string,
  ): Promise<void> {
    await apiClient.delete(
      `/android-enterprise/enrollments/${id}`,
    )
  },

  async getDeviceSummary():
    Promise<AndroidDeviceInventorySummary> {
    const response =
      await apiClient.get<AndroidDeviceInventorySummary>(
        '/android-enterprise/devices/summary',
      )

    return response.data
  },

  async synchronizeDevices():
    Promise<AndroidDeviceSyncResult> {
    const response =
      await apiClient.post<AndroidDeviceSyncResult>(
        '/android-enterprise/devices/sync',
      )

    return response.data
  },

  async getInventory(
    query: AndroidInventoryQuery = {},
  ): Promise<AndroidInventoryPage> {
    const response =
      await apiClient.get<AndroidInventoryPage>(
        '/android-enterprise/devices',
        {
          params: {
            page: query.page ?? 1,
            pageSize: query.pageSize ?? 50,
            search: query.search?.trim() || undefined,
          },
        },
      )

    return response.data
  },

  async getAudit(
    query: AndroidAuditQuery = {},
  ): Promise<AndroidAuditPage> {
    const response =
      await apiClient.get<AndroidAuditPage>(
        '/android-enterprise/audit',
        {
          params: {
            page: query.page ?? 1,
            pageSize: query.pageSize ?? 50,
            search: query.search?.trim() || undefined,
          },
        },
      )

    return response.data
  },

  async executeDeviceAction(
    deviceId: string,
    request: AndroidRemoteActionRequest,
  ): Promise<AndroidRemoteActionResponse> {
    const response =
      await apiClient.post<AndroidRemoteActionResponse>(
        `/android-enterprise/devices/${deviceId}/actions`,
        request,
      )

    return response.data
  },
}
