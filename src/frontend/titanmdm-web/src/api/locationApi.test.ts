import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import apiClient from './apiClient'
import {
  locationApi,
  type FleetLocation,
} from './locationApi'

vi.mock('./apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}))

const baseFleetRow: FleetLocation = {
  deviceId: 'device-1',
  deviceName: 'Tablet Alfa',
  manufacturer: 'Samsung',
  model: 'Tab A9',
  department: 'Operaciones',
  siteId: null,
  siteName: null,
  latitude: -34.6037,
  longitude: -58.3816,
  accuracyMeters: 12.5,
  capturedAtUtc: '2026-10-18T00:00:00Z',
  receivedAtUtc: '2026-10-18T00:00:05Z',
  status: 'Online',
  locationState: 'Fresh',
  signalAgeMinutes: 2,
  batteryLevel: 88,
  lastSeenAtUtc: '2026-10-18T00:00:00Z',
}

describe('locationApi ANDROID-GEO-01 contracts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('requests the fleet endpoint with typed filters', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: [baseFleetRow],
    })

    const result = await locationApi.getFleet({
      search: 'Alfa',
      locationState: 'Fresh',
      maxSignalAgeMinutes: 120,
    })

    expect(apiClient.get).toHaveBeenCalledWith(
      '/location/fleet',
      {
        params: {
          search: 'Alfa',
          locationState: 'Fresh',
          maxSignalAgeMinutes: 120,
        },
      },
    )
    expect(result).toEqual([baseFleetRow])
  })

  it('keeps nullable coordinates in the fleet contract', async () => {
    const noLocationRow: FleetLocation = {
      ...baseFleetRow,
      deviceId: 'device-2',
      latitude: null,
      longitude: null,
      capturedAtUtc: null,
      locationState: 'NoLocation',
      signalAgeMinutes: null,
    }

    vi.mocked(apiClient.get).mockResolvedValue({
      data: [noLocationRow],
    })

    const result = await locationApi.getFleet()

    const row = result[0]
    expect(row.latitude).toBeNull()
    expect(row.longitude).toBeNull()
    expect(row.locationState).toBe('NoLocation')
  })

  it('updates the reporting policy with a typed body', async () => {
    const policy = {
      id: 'policy-1',
      isEnabled: true,
      reportingIntervalMinutes: 15,
      retentionDays: 30,
    }

    vi.mocked(apiClient.post).mockResolvedValue({
      data: policy,
    })

    const result =
      await locationApi.updateReportingPolicy({
        isEnabled: true,
        reportingIntervalMinutes: 15,
        retentionDays: 30,
      })

    expect(apiClient.post).toHaveBeenCalledWith(
      '/location/policy',
      {
        isEnabled: true,
        reportingIntervalMinutes: 15,
        retentionDays: 30,
      },
    )
    expect(result).toEqual(policy)
  })

  it('requests a single on-demand location', async () => {
    vi.mocked(apiClient.post).mockResolvedValue({
      data: {},
    })

    await locationApi.requestLocation('device-1')

    expect(apiClient.post).toHaveBeenCalledWith(
      '/location/devices/device-1/request',
    )
  })

  it('reads the reporting policy from the user endpoint', async () => {
    const policy = {
      id: 'policy-1',
      isEnabled: false,
      reportingIntervalMinutes: 15,
      retentionDays: 30,
    }

    vi.mocked(apiClient.get).mockResolvedValue({
      data: policy,
    })

    await expect(
      locationApi.getReportingPolicy(),
    ).resolves.toEqual(policy)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/location/policy',
    )
  })
})
