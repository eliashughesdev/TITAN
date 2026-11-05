import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import {
  MemoryRouter,
} from 'react-router-dom'

import type {
  ReactElement,
} from 'react'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  androidEnterpriseApi,
} from '../../../../api/androidEnterpriseApi'

import {
  devicesApi,
} from '../../../../api/devicesApi'

import type {
  AndroidDeviceCapabilities,
} from '../../../../types/androidEnterprise'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
} from '../../../../types/device'

import {
  AndroidRemoteConsole,
} from './AndroidRemoteConsole'

const auth = vi.hoisted(() => ({
  allowed: true,
}))

vi.mock('../../../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: () => auth.allowed,
  }),
}))

vi.mock('../../../../api/androidEnterpriseApi', () => ({
  androidEnterpriseApi: {
    getDeviceCapabilities: vi.fn(),
    getAudit: vi.fn(),
    executeDeviceAction: vi.fn(),
    synchronizeDevices: vi.fn(),
  },
}))

vi.mock('../../../../api/devicesApi', () => ({
  devicesApi: {
    getAndroidDeviceDetails: vi.fn(),
    getDeviceById: vi.fn(),
  },
}))

const capabilities: AndroidDeviceCapabilities = {
  deviceId: 'device-1',
  deviceName: 'Pixel 8',
  manufacturer: 'Google',
  model: 'Pixel 8',
  apiLevel: 34,
  androidVersion: '14',
  securityPatchLevel: '2026-09-01',
  managementMode: 'WorkProfile',
  ownership: 'CompanyOwned',
  isManaged: true,
  isDeletedInGoogle: false,
  hasGoogleMobileServices: true,
  requiresAlternativeRoute: false,
  alternativeRouteNote: null,
  remoteDiagnosticsSupported: true,
  remoteVisionSupported: false,
  remoteControlSupported: false,
  remoteSupportNote: 'Sin control interactivo desatendido.',
  actions: [
    { action: 'lock', status: 'Available', reason: null },
    { action: 'reboot', status: 'Available', reason: null },
    { action: 'reset-password', status: 'Available', reason: null },
    { action: 'start-lost-mode', status: 'Available', reason: null },
    { action: 'stop-lost-mode', status: 'Available', reason: null },
    { action: 'wipe', status: 'Available', reason: null },
    {
      action: 'delete',
      status: 'NotSupported',
      reason: 'No aplica al modo de administración.',
    },
  ],
}

const details = {
  securityPatchLevel: '2026-09-01',
  encryptionStatus: 'Encrypted',
  appliedPolicyName: 'Política Titán',
  appliedPolicyState: 'APPLIED',
  securityPosture: 'SECURE',
  lastPolicySyncTimeUtc: '2026-10-14T10:00:00Z',
} as unknown as AndroidDeviceDetails

const device: DeviceDetails = {
  id: 'device-1',
  organizationId: 'organization-1',
  deviceName: 'Pixel 8',
  platform: 'Android',
  status: 'Online',
  complianceStatus: 'Compliant',
  serialNumber: 'SERIAL-1',
  imei: null,
  manufacturer: 'Google',
  model: 'Pixel 8',
  operatingSystem: 'Android',
  operatingSystemVersion: '14',
  agentVersion: null,
  ipAddress: '10.0.0.8',
  macAddress: null,
  assignedUser: null,
  department: null,
  batteryLevel: 72,
  isManaged: true,
  enrolledAtUtc: '2026-10-01T10:00:00Z',
  lastSeenAtUtc: '2026-10-14T10:00:00Z',
  interactiveUser: null,
  interactiveSessionId: null,
  interactiveUserUpdatedAtUtc: null,
  createdAtUtc: '2026-10-01T10:00:00Z',
  updatedAtUtc: '2026-10-14T10:00:00Z',
}

function renderConsole(ui: ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>)
}

describe('AndroidRemoteConsole', () => {
  beforeEach(() => {
    auth.allowed = true
    vi.clearAllMocks()

    vi.mocked(
      androidEnterpriseApi.getDeviceCapabilities,
    ).mockResolvedValue(capabilities)

    vi.mocked(devicesApi.getAndroidDeviceDetails)
      .mockResolvedValue(details)

    vi.mocked(devicesApi.getDeviceById)
      .mockResolvedValue(device)

    vi.mocked(androidEnterpriseApi.getAudit)
      .mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 50,
        totalPages: 0,
      })
  })

  it('renders capabilities and blocks unsupported operations', async () => {
    renderConsole(
      <AndroidRemoteConsole deviceId="device-1" deviceName="Pixel 8" />,
    )

    expect(
      await screen.findByRole('heading', { name: 'Pixel 8' }),
    ).toBeInTheDocument()

    const lockButton = screen.getByRole('button', {
      name: /Bloquear pantalla/,
    })

    await waitFor(() => expect(lockButton).toBeEnabled())

    expect(
      screen.getByRole('button', {
        name: /Eliminar de Android Enterprise/,
      }),
    ).toBeDisabled()

    expect(
      screen.getByText('Sin control interactivo desatendido.'),
    ).toBeInTheDocument()
  })

  it('issues an available command and records the acknowledgement', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    vi.mocked(
      androidEnterpriseApi.executeDeviceAction,
    ).mockResolvedValue({
      deviceId: 'device-1',
      action: 'Lock',
      status: 'Issued',
      googleCommandName: 'operations/1',
      correlationId: 'correlation-1',
      issuedAtUtc: '2026-10-14T12:00:00Z',
    })

    renderConsole(
      <AndroidRemoteConsole deviceId="device-1" deviceName="Pixel 8" />,
    )

    const lockButton = await screen.findByRole('button', {
      name: /Bloquear pantalla/,
    })

    await waitFor(() => expect(lockButton).toBeEnabled())
    fireEvent.click(lockButton)

    await waitFor(() =>
      expect(
        androidEnterpriseApi.executeDeviceAction,
      ).toHaveBeenCalledWith('device-1', { action: 'lock' }),
    )

    expect(
      await screen.findByText('Solicitado · verificación pendiente'),
    ).toBeInTheDocument()
  })

  it('records a failed command when the API rejects the request', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    vi.mocked(
      androidEnterpriseApi.executeDeviceAction,
    ).mockRejectedValue(
      new Error('Google rechazó la operación.'),
    )

    renderConsole(
      <AndroidRemoteConsole deviceId="device-1" deviceName="Pixel 8" />,
    )

    const lockButton = await screen.findByRole('button', {
      name: /Bloquear pantalla/,
    })

    await waitFor(() => expect(lockButton).toBeEnabled())
    fireEvent.click(lockButton)

    expect(
      await screen.findByText('Fallido'),
    ).toBeInTheDocument()

    expect(
      screen.getByRole('alert'),
    ).toHaveTextContent('Google rechazó la operación.')
  })

  it('blocks destructive commands when confirmation does not match', async () => {
    vi.spyOn(window, 'prompt').mockReturnValue('Nombre incorrecto')

    renderConsole(
      <AndroidRemoteConsole deviceId="device-1" deviceName="Pixel 8" />,
    )

    const wipeButton = await screen.findByRole('button', {
      name: /Borrar dispositivo/,
    })

    await waitFor(() => expect(wipeButton).toBeEnabled())
    fireEvent.click(wipeButton)

    expect(
      await screen.findByText(/Confirmación reforzada cancelada/),
    ).toBeInTheDocument()

    expect(
      androidEnterpriseApi.executeDeviceAction,
    ).not.toHaveBeenCalled()
  })

  it('keeps critical commands disabled without action permission', async () => {
    auth.allowed = false

    renderConsole(
      <AndroidRemoteConsole deviceId="device-1" deviceName="Pixel 8" />,
    )

    expect(
      await screen.findByRole('button', { name: /Bloquear pantalla/ }),
    ).toBeDisabled()
    expect(
      screen.getByRole('button', { name: /Borrar dispositivo/ }),
    ).toBeDisabled()
    expect(screen.getAllByText('No autorizado').length).toBeGreaterThan(0)
    expect(
      androidEnterpriseApi.executeDeviceAction,
    ).not.toHaveBeenCalled()
  })
})
