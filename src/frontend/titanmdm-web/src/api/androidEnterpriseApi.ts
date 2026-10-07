import apiClient from './apiClient'

import type {
  AndroidDeviceInventorySummary,
  AndroidDeviceSyncResult,
  AndroidEnterpriseStatus,
  AndroidEnrollment,
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
}