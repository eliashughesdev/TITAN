import type {
  ReactNode,
} from 'react'
import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'
import {
  MemoryRouter,
  Route,
  Routes,
} from 'react-router-dom'
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
import { FleetMapPage } from './FleetMapPage'

const map = vi.hoisted(() => ({
  setView: vi.fn(),
  fitBounds: vi.fn(),
  getZoom: vi.fn(() => 11),
}))

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: ReactNode }) => (
    <div data-testid="leaflet-map">{children}</div>
  ),
  TileLayer: ({
    url,
    attribution,
  }: {
    url: string
    attribution: string
  }) => (
    <div
      data-testid="tile-layer"
      data-url={url}
      data-attribution={attribution}
    />
  ),
  CircleMarker: ({
    center,
  }: {
    center: readonly [number, number]
  }) => (
    <div data-testid="fleet-marker" data-center={center.join(',')} />
  ),
  Tooltip: ({ children }: { children?: ReactNode }) => (
    <span data-testid="fleet-tooltip">{children}</span>
  ),
  useMap: () => map,
}))

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({ hasPermission: () => true }),
}))

vi.mock('../../api/deviceCommandsApi', () => ({
  deviceCommandsApi: {},
}))

vi.mock('../../api/locationApi', () => ({
  locationApi: {
    getFleet: vi.fn(),
  },
}))

const navigate = vi.hoisted(() => vi.fn())

vi.mock('react-router-dom', async importOriginal => {
  const actual = await importOriginal<typeof import('react-router-dom')>()

  return {
    ...actual,
    useNavigate: () => navigate,
  }
})

function fleetRow(
  overrides: Partial<FleetLocation> = {},
): FleetLocation {
  return {
    deviceId: 'device-1',
    deviceName: 'Tablet Alfa',
    alias: null,
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

function renderMap() {
  return render(
    <MemoryRouter>
      <FleetMapPage />
    </MemoryRouter>,
  )
}

describe('FleetMapPage ANDROID-FIX-02', () => {
  beforeEach(() => {
    navigate.mockReset()
    map.setView.mockReset()
    map.fitBounds.mockReset()
    vi.clearAllMocks()
    vi.mocked(locationApi.getFleet).mockResolvedValue([])
  })

  it('loads the official fleet route with Leaflet and OpenStreetMap', async () => {
    render(
      <MemoryRouter initialEntries={['/fleet-map?workspace=android']}>
        <Routes>
          <Route path="/fleet-map" element={<FleetMapPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByTestId('leaflet-map')).toBeInTheDocument()
    expect(screen.getByTestId('tile-layer')).toHaveAttribute(
      'data-url',
      'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    )
    expect(screen.getByTestId('tile-layer')).toHaveAttribute(
      'data-attribution',
      expect.stringContaining('OpenStreetMap'),
    )
    expect(screen.queryByText(/Google Maps/i)).not.toBeInTheDocument()
  })

  it('creates markers only for finite coordinates inside valid ranges', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue([
      fleetRow(),
      fleetRow({
        deviceId: 'device-null',
        deviceName: 'Sin posición',
        latitude: null,
        longitude: null,
        locationState: 'NoLocation',
      }),
      fleetRow({
        deviceId: 'device-invalid',
        deviceName: 'Coordenada inválida',
        latitude: 95,
        longitude: -58,
      }),
    ])

    renderMap()

    expect(await screen.findByText('Tablet Alfa')).toBeInTheDocument()
    expect(screen.getAllByTestId('fleet-marker')).toHaveLength(1)
    expect(screen.getByTestId('fleet-marker')).toHaveAttribute(
      'data-center',
      '-34.6037,-58.3816',
    )
  })

  it('shows the device alias instead of the system name', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue([
      fleetRow({ alias: 'Tablet Bodega' }),
    ])

    renderMap()

    expect(
      await screen.findByText('Tablet Bodega'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Tablet Alfa'),
    ).not.toBeInTheDocument()
  })

  it('shows the Android enrollment empty state', async () => {
    renderMap()

    expect(
      await screen.findByText(
        'Aún no existen teléfonos Android enrolados en esta organización.',
      ),
    ).toBeInTheDocument()
  })

  it('validates nullable and malformed timestamps before displaying them', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue([
      fleetRow({
        latitude: null,
        longitude: null,
        capturedAtUtc: 'fecha-inválida',
        locationState: 'NoLocation',
        signalAgeMinutes: null,
      }),
    ])

    renderMap()
    fireEvent.click(await screen.findByText('Tablet Alfa'))

    expect(screen.getByText('Sin datos')).toBeInTheDocument()
    expect(
      screen.getByText(/^Este dispositivo no ha reportado coordenadas válidas/i),
    ).toBeInTheDocument()
  })

  it('uses signalAgeMinutes from the real fleet contract', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue([fleetRow()])

    renderMap()
    fireEvent.click(await screen.findByText('Tablet Alfa'))

    expect(screen.getByText('2 min')).toBeInTheDocument()
  })

  it('ignores non-Android rows even if the API returns them', async () => {
    vi.mocked(locationApi.getFleet).mockResolvedValue([
      fleetRow(),
      fleetRow({
        deviceId: 'device-windows',
        deviceName: 'Laptop Windows',
        platform:
          'Windows' as FleetLocation['platform'],
      }),
    ])

    renderMap()

    expect(
      await screen.findByText('Tablet Alfa'),
    ).toBeInTheDocument()

    expect(
      screen.queryByText('Laptop Windows'),
    ).not.toBeInTheDocument()

    expect(
      screen.getAllByTestId('fleet-marker'),
    ).toHaveLength(1)
  })

  it('shows an API error without inventing device positions', async () => {
    vi.mocked(locationApi.getFleet).mockRejectedValue(
      new Error('network down'),
    )

    renderMap()

    expect(
      await screen.findByRole('alert'),
    ).toHaveTextContent(
      'No fue posible cargar la ubicación de la flota Android.',
    )

    expect(
      screen.getByText(
        'Aún no existen teléfonos Android enrolados en esta organización.',
      ),
    ).toBeInTheDocument()

    expect(
      screen.queryByTestId('fleet-marker'),
    ).not.toBeInTheDocument()
  })

  it('sends fleet filters to the backend', async () => {
    renderMap()

    await screen.findByText(
      'Aún no existen teléfonos Android enrolados en esta organización.',
    )

    fireEvent.change(screen.getByLabelText('Estado de ubicación'), {
      target: { value: 'Stale' },
    })

    await waitFor(() => {
      expect(locationApi.getFleet).toHaveBeenCalledWith(
        expect.objectContaining({ locationState: 'Stale' }),
      )
    })

    expect(
      await screen.findByText(
        'No hay dispositivos Android que coincidan con los filtros.',
      ),
    ).toBeInTheDocument()
  })
})
