import apiClient from './apiClient'

export interface DeviceLocation {
  id: string
  deviceId: string
  latitude: number
  longitude: number
  accuracyMeters: number | null
  altitudeMeters: number | null
  speedMetersPerSecond: number | null
  source: string
  capturedAtUtc: string
  receivedAtUtc: string
}

export interface Geofence {
  id: string
  name: string
  description: string | null
  latitude: number
  longitude: number
  radiusMeters: number
  alertOnEnter: boolean
  alertOnExit: boolean
  isEnabled: boolean
  assignedDevices: number
  createdAtUtc: string
}

export interface CreateGeofenceRequest {
  name: string
  description?: string | null
  latitude: number
  longitude: number
  radiusMeters: number
  alertOnEnter: boolean
  alertOnExit: boolean
}

export const locationApi = {
  async getLatest(
    deviceId: string,
  ): Promise<DeviceLocation> {
    const response =
      await apiClient.get<DeviceLocation>(
        `/location/devices/${deviceId}/latest`,
      )

    return response.data
  },

  async getHistory(
    deviceId: string,
    limit = 100,
  ): Promise<DeviceLocation[]> {
    const response =
      await apiClient.get<DeviceLocation[]>(
        `/location/devices/${deviceId}/history`,
        {
          params: { limit },
        },
      )

    return response.data
  },

  async requestLocation(
    deviceId: string,
  ): Promise<void> {
    await apiClient.post(
      `/location/devices/${deviceId}/request`,
    )
  },

  async getGeofences():
    Promise<Geofence[]> {
    const response =
      await apiClient.get<Geofence[]>(
        '/location/geofences',
      )

    return response.data
  },

  async createGeofence(
    request: CreateGeofenceRequest,
  ): Promise<Geofence> {
    const response =
      await apiClient.post<Geofence>(
        '/location/geofences',
        request,
      )

    return response.data
  },

  async assignDevices(
    geofenceId: string,
    deviceIds: string[],
  ): Promise<void> {
    await apiClient.post(
      `/location/geofences/${geofenceId}/devices`,
      {
        deviceIds,
      },
    )
  },

  async deleteGeofence(
    geofenceId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/location/geofences/${geofenceId}`,
    )
  },
}