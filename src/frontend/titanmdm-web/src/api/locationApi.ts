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

export type FleetLocationState =
  | 'Fresh'
  | 'Stale'
  | 'Offline'
  | 'NoLocation'
  | 'PermissionDenied'

export interface FleetLocation {
  deviceId: string
  deviceName: string
  platform: 'Android'
  manufacturer: string | null
  model: string | null
  department: string | null
  siteId: string | null
  siteName: string | null
  latitude: number | null
  longitude: number | null
  accuracyMeters: number | null
  capturedAtUtc: string | null
  receivedAtUtc: string | null
  status: string
  locationState: FleetLocationState
  signalAgeMinutes: number | null
  batteryLevel: number | null
  lastSeenAtUtc: string | null
}

export interface FleetLocationFilters {
  search?: string
  manufacturer?: string
  department?: string
  deviceGroupId?: string
  siteId?: string
  status?: string
  locationState?: string
  maxSignalAgeMinutes?: number
}

export interface LocationReportingPolicy {
  id: string
  isEnabled: boolean
  reportingIntervalMinutes: number
  retentionDays: number
}

export interface UpdateReportingPolicyRequest {
  isEnabled: boolean
  reportingIntervalMinutes: number
  retentionDays: number
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

  async getFleet(
    filters: FleetLocationFilters = {},
  ): Promise<FleetLocation[]> {
    const response =
      await apiClient.get<FleetLocation[]>(
        '/location/fleet',
        {
          params: filters,
        },
      )

    return response.data.filter(
      location => location.platform === 'Android',
    )
  },

  async getReportingPolicy():
    Promise<LocationReportingPolicy> {
    const response =
      await apiClient.get<LocationReportingPolicy>(
        '/location/policy',
      )

    return response.data
  },

  async updateReportingPolicy(
    request: UpdateReportingPolicyRequest,
  ): Promise<LocationReportingPolicy> {
    const response =
      await apiClient.post<LocationReportingPolicy>(
        '/location/policy',
        request,
      )

    return response.data
  },
}
