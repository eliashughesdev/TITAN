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
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  locationApi,
  type Geofence,
} from '../../api/locationApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  GeofencingPage,
} from './GeofencingPage'

const authorization = vi.hoisted(() => ({
  manage: true,
}))

const map = vi.hoisted(() => ({
  setView: vi.fn(),
  getZoom: vi.fn(() => 13),
}))

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: (permission: string) =>
      permission === 'geofencing.manage'
        ? authorization.manage
        : true,
  }),
}))

vi.mock('react-leaflet', () => ({
  MapContainer: ({
    children,
  }: {
    children: ReactNode
  }) => (
    <div data-testid="geo-map">{children}</div>
  ),
  TileLayer: ({
    url,
  }: {
    url: string
  }) => (
    <div data-testid="geo-tile-layer" data-url={url} />
  ),
  Circle: ({
    center,
    radius,
    pathOptions,
  }: {
    center: [number, number]
    radius: number
    pathOptions?: {
      dashArray?: string
      bubblingMouseEvents?: boolean
    }
  }) => (
    <div
      data-testid="geo-circle"
      data-center={center.join(',')}
      data-radius={String(radius)}
      data-dashed={pathOptions?.dashArray ?? ''}
    />
  ),
  Marker: ({
    position,
    draggable,
  }: {
    position: [number, number]
    draggable?: boolean
  }) => (
    <div
      data-testid="geo-marker"
      data-position={position.join(',')}
      data-draggable={String(draggable)}
    />
  ),
  useMapEvents: () => null,
  useMap: () => map,
}))

vi.mock('../../api/locationApi', () => ({
  locationApi: {
    getGeofences: vi.fn(),
    createGeofence: vi.fn(),
    assignDevices: vi.fn(),
    deleteGeofence: vi.fn(),
  },
}))

vi.mock('../../api/devicesApi', () => ({
  devicesApi: {
    getDevices: vi.fn(),
  },
}))

function androidDevice(
  overrides: Partial<DeviceListItem> = {},
): DeviceListItem {
  return {
    id: 'android-1',
    deviceName: 'Android Tablet',
    platform: 'Android',
    status: 'Online',
    complianceStatus: 'Compliant',
    serialNumber: 'ANDROID-1',
    manufacturer: 'Samsung',
    model: 'Tab A9',
    operatingSystem: 'Android',
    operatingSystemVersion: '14',
    assignedUser: null,
    department: 'Operaciones',
    ipAddress: null,
    batteryLevel: 80,
    isManaged: true,
    enrolledAtUtc: '2026-10-01T00:00:00Z',
    lastSeenAtUtc: '2026-10-18T00:00:00Z',
    interactiveUser: null,
    interactiveSessionId: null,
    interactiveUserUpdatedAtUtc: null,
    ...overrides,
  }
}

function windowsDevice(
  overrides: Partial<DeviceListItem> = {},
): DeviceListItem {
  return androidDevice({
    id: 'windows-1',
    deviceName: 'Windows Laptop',
    platform: 'Windows',
    serialNumber: 'WINDOWS-1',
    operatingSystem: 'Windows',
    ...overrides,
  })
}

function geofence(
  overrides: Partial<Geofence> = {},
): Geofence {
  return {
    id: 'geofence-1',
    name: 'Oficina principal',
    description: null,
    latitude: 18.4861,
    longitude: -69.9312,
    radiusMeters: 350,
    alertOnEnter: true,
    alertOnExit: true,
    isEnabled: true,
    assignedDevices: 2,
    createdAtUtc: '2026-10-01T00:00:00Z',
    ...overrides,
  }
}

function devicePage(
  items: DeviceListItem[],
  totalCount = items.length,
  page = 1,
) {
  return {
    items,
    totalCount,
    page,
    pageSize: 50,
    totalPages: Math.max(1, Math.ceil(totalCount / 50)),
  }
}

describe('GeofencingPage ANDROID-FIX', () => {
  beforeEach(() => {
    authorization.manage = true
    vi.clearAllMocks()

    vi.mocked(locationApi.getGeofences)
      .mockResolvedValue([])

    vi.mocked(devicesApi.getDevices)
      .mockResolvedValue(devicePage([]))
  })

  it('requests only Android devices and ignores non-Android responses', async () => {
    vi.mocked(devicesApi.getDevices).mockResolvedValue(
      devicePage([
        androidDevice(),
        windowsDevice(),
      ]),
    )

    render(<GeofencingPage />)

    expect(
      await screen.findByText('Android Tablet'),
    ).toBeInTheDocument()

    expect(
      screen.queryByText('Windows Laptop'),
    ).not.toBeInTheDocument()

    expect(devicesApi.getDevices).toHaveBeenCalledWith(
      expect.objectContaining({
        platform: 'Android',
        page: 1,
      }),
    )
  })

  it('shows an empty state when the organization has no Android inventory', async () => {
    render(<GeofencingPage />)

    expect(
      await screen.findByText(
        'No hay dispositivos Android disponibles en esta organización.',
      ),
    ).toBeInTheDocument()
  })

  it('paginates beyond the first page instead of limiting to 100 devices', async () => {
    vi.mocked(devicesApi.getDevices)
      .mockImplementation(async (parameters = {}) =>
        devicePage(
          [androidDevice()],
          140,
          parameters.page ?? 1,
        ),
      )

    render(<GeofencingPage />)

    expect(
      await screen.findByText('Página 1 de 3'),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Página siguiente',
      }),
    )

    await waitFor(() => {
      expect(devicesApi.getDevices)
        .toHaveBeenLastCalledWith(
          expect.objectContaining({
            platform: 'Android',
            page: 2,
          }),
        )
    })

    expect(
      await screen.findByText('Página 2 de 3'),
    ).toBeInTheDocument()
  })

  it('creates a geofence with the selected center and radius', async () => {
    const created = geofence()

    vi.mocked(locationApi.getGeofences)
      .mockResolvedValueOnce([])
      .mockResolvedValue([created])

    vi.mocked(locationApi.createGeofence)
      .mockResolvedValue(created)

    render(<GeofencingPage />)

    await screen.findByText(
      'No hay dispositivos Android disponibles en esta organización.',
    )

    fireEvent.change(
      screen.getByPlaceholderText('Oficina principal'),
      { target: { value: 'Oficina principal' } },
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: /Crear geocerca/,
      }),
    )

    await waitFor(() => {
      expect(locationApi.createGeofence)
        .toHaveBeenCalledWith(
          expect.objectContaining({
            name: 'Oficina principal',
            latitude: 18.4861,
            longitude: -69.9312,
            radiusMeters: 250,
          }),
        )
    })

    expect(
      await screen.findByText(
        /creada correctamente/,
      ),
    ).toBeInTheDocument()

    expect(
      screen
        .getAllByTestId('geo-circle')
        .some(
          (element) =>
            element.getAttribute(
              'data-radius',
            ) === '350',
        ),
    ).toBe(true)
  })

  it('renders configured geofences with their delimiter and status', async () => {
    vi.mocked(locationApi.getGeofences)
      .mockResolvedValue([
        geofence(),
        geofence({
          id: 'geofence-2',
          name: 'Zona inactiva',
          isEnabled: false,
          assignedDevices: 0,
        }),
      ])

    render(<GeofencingPage />)

    expect(
      await screen.findByText('Oficina principal'),
    ).toBeInTheDocument()

    expect(screen.getByText('Activa')).toBeInTheDocument()
    expect(
      screen.getByText('Desactivada'),
    ).toBeInTheDocument()

    const circles =
      screen.getAllByTestId('geo-circle')

    expect(
      circles.some(
        (element) =>
          element.getAttribute(
            'data-radius',
          ) === '350',
      ),
    ).toBe(true)
    expect(
      circles.some(
        (element) =>
          element.getAttribute(
            'data-dashed',
          ) === '6 6',
      ),
    ).toBe(true)
  })

  it('keeps mutating actions disabled without geofencing.manage', async () => {
    authorization.manage = false

    render(<GeofencingPage />)

    await screen.findByText(
      'No hay dispositivos Android disponibles en esta organización.',
    )

    expect(
      screen.getByText(/solo lectura/i),
    ).toBeInTheDocument()

    expect(
      screen.getByRole('button', {
        name: /Crear geocerca/,
      }),
    ).toBeDisabled()

    expect(
      screen.getByRole('button', {
        name: 'Asignar a zona',
      }),
    ).toBeDisabled()
  })
})
