import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  locationApi,
  type FleetLocation,
} from '../../api/locationApi'
import {
  FleetMapPage,
} from './FleetMapPage'

const mapsConfig = vi.hoisted(() => ({
  configured: true,
}))

vi.mock('../../config/googleMaps', () => ({
  isGoogleMapsConfigured: () =>
    mapsConfig.configured,
  loadGoogleMaps: () =>
    Promise.resolve(mapsConfig.configured),
}))

vi.mock('../../api/locationApi', () => ({
  locationApi: {
    getFleet: vi.fn(),
  },
}))

const navigate = vi.hoisted(() => vi.fn())

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigate,
}))

function fleetRow(
  overrides: Partial<FleetLocation>,
): FleetLocation {
  return {
    deviceId: 'device-1',
    deviceName: 'Tablet Alfa',
    platform: 'Android',
    manufacturer: 'Samsung',
    model: 'Tab A9',
    department: 'Operaciones',
    siteId: null,
    siteName: 'Oficina Central',
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
    ...overrides,
  }
}

describe('FleetMapPage ANDROID-GEO-01', () => {
  beforeEach(() => {
    mapsConfig.configured = true
    navigate.mockReset()
    vi.clearAllMocks()
    vi.mocked(locationApi.getFleet).mockResolvedValue(
      [],
    )
  })

  it('shows the configuration-pending state when the Maps key is missing', async () => {
    mapsConfig.configured = false

    render(<FleetMapPage />)

    expect(
      await screen.findByText(
        'Configuración Pendiente',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText(
        /Configure la variable de entorno/,
      ),
    ).toBeInTheDocument()
  })

  it('renders the device list with location states', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue(
      [
        fleetRow({}),
        fleetRow({
          deviceId: 'device-2',
          deviceName: 'Tablet Beta',
          latitude: null,
          longitude: null,
          capturedAtUtc: null,
          locationState: 'NoLocation',
          signalAgeMinutes: null,
        }),
        fleetRow({
          deviceId: 'device-3',
          deviceName: 'Tablet Gamma',
          locationState: 'PermissionDenied',
          latitude: null,
          longitude: null,
        }),
      ],
    )

    render(<FleetMapPage />)

    expect(
      await screen.findByText('Tablet Alfa'),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Tablet Beta'),
    ).toBeInTheDocument()

    expect(
      screen.getAllByText('Sin ubicación').length,
    ).toBeGreaterThan(0)

    expect(
      screen.getAllByText('Permiso denegado').length,
    ).toBeGreaterThan(0)

    await waitFor(() => {
      expect(
        locationApi.getFleet,
      ).toHaveBeenCalledTimes(1)
    })
  })

  it('does not invent coordinates for devices without a reported position', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue(
      [
        fleetRow({
          latitude: null,
          longitude: null,
          capturedAtUtc: null,
          locationState: 'NoLocation',
          signalAgeMinutes: null,
        }),
      ],
    )

    render(<FleetMapPage />)

    fireEvent.click(
      await screen.findByText('Tablet Alfa'),
    )

    expect(
      screen.getByText(
        /no ha reportado coordenadas/,
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Sin datos'),
    ).toBeInTheDocument()
  })

  it('shows the signal age for fresh devices', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue(
      [fleetRow({})],
    )

    render(<FleetMapPage />)

    fireEvent.click(
      await screen.findByText('Tablet Alfa'),
    )

    expect(screen.getByText('2 min')).toBeInTheDocument()
  })

  it('sends the locationState filter to the backend', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue(
      [],
    )

    render(<FleetMapPage />)

    await screen.findByText(
      'No hay dispositivos que coincidan con los filtros.',
    )

    fireEvent.change(
      screen.getByLabelText('Estado de ubicación'),
      { target: { value: 'Stale' } },
    )

    await waitFor(() => {
      expect(
        locationApi.getFleet,
      ).toHaveBeenCalledWith(
        expect.objectContaining({
          locationState: 'Stale',
        }),
      )
    })
  })
})
