import {
  fireEvent,
  render,
  screen,
} from '@testing-library/react'

import {
  MemoryRouter,
} from 'react-router-dom'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  androidEnterpriseApi,
} from '../../../api/androidEnterpriseApi'

import type {
  AndroidDeviceInventorySummary,
  AndroidInventoryItem,
} from '../../../types/androidEnterprise'

import {
  AndroidFleetCommandCenter,
} from './AndroidFleetCommandCenter'

const authorization = vi.hoisted(() => ({
  manage: true,
}))

vi.mock('../../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: (permission: string) =>
      permission === 'android.enterprise.manage'
        ? authorization.manage
        : true,
  }),
}))

vi.mock('../../../api/androidEnterpriseApi', () => ({
  androidEnterpriseApi: {
    getInventory: vi.fn(),
    getDeviceSummary: vi.fn(),
    getAudit: vi.fn(),
    synchronizeDevices: vi.fn(),
  },
}))

function summary(
  overrides: Partial<AndroidDeviceInventorySummary> = {},
): AndroidDeviceInventorySummary {
  return {
    total: 0,
    managed: 0,
    missingInGoogle: 0,
    fullyManaged: 0,
    dedicated: 0,
    workProfile: 0,
    compliant: 0,
    nonCompliant: 0,
    lastSynchronizationUtc: null,
    ...overrides,
  }
}

function inventoryItem(
  overrides: Partial<AndroidInventoryItem> = {},
): AndroidInventoryItem {
  return {
    deviceId: 'device-1',
    deviceName: 'Pixel 8',
    serialNumber: 'SERIAL-1',
    manufacturer: 'Google',
    model: 'Pixel 8',
    status: 'Online',
    complianceStatus: 'Compliant',
    isManaged: true,
    lastSeenAtUtc: '2026-10-18T12:00:00Z',
    managementMode: 'FullyManaged',
    ownership: 'CompanyOwned',
    state: 'Active',
    appliedPolicyName: 'Política corporativa',
    appliedPolicyVersion: 3,
    appliedPolicyState: 'Applied',
    lastPolicySyncTimeUtc: '2026-10-18T11:50:00Z',
    lastSynchronizedAtUtc: '2026-10-18T12:00:00Z',
    isDeletedInGoogle: false,
    apiLevel: 34,
    androidVersion: '14',
    securityPatchLevel: '2026-10-01',
    ...overrides,
  }
}

function renderCenter() {
  render(
    <MemoryRouter>
      <AndroidFleetCommandCenter />
    </MemoryRouter>,
  )
}

describe('AndroidFleetCommandCenter ANDROID-FIX', () => {
  beforeEach(() => {
    authorization.manage = true
    vi.clearAllMocks()

    vi.mocked(androidEnterpriseApi.getInventory)
      .mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 25,
        totalPages: 1,
      })

    vi.mocked(androidEnterpriseApi.getDeviceSummary)
      .mockResolvedValue(summary())

    vi.mocked(androidEnterpriseApi.getAudit)
      .mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 8,
        totalPages: 1,
      })
  })

  it('keeps the configuration available when the Android inventory is empty', async () => {
    renderCenter()

    expect(
      await screen.findByText(
        'No hay dispositivos Android Enterprise enrolados.',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText(/siguen disponibles para configurar la flota/),
    ).toBeInTheDocument()

    expect(
      screen.getByRole('link', {
        name: /Configurar inscripción Android/,
      }),
    ).toHaveAttribute(
      'href',
      '/enrollment?workspace=android',
    )

    expect(
      screen.getAllByRole('link', {
        name: /Geolocalización/,
      })[0],
    ).toHaveAttribute(
      'href',
      '/fleet-map?workspace=android',
    )
  })

  it('shows Android version and last inventory synchronization in the device details', async () => {
    vi.mocked(androidEnterpriseApi.getInventory)
      .mockResolvedValue({
        items: [inventoryItem()],
        totalCount: 1,
        page: 1,
        pageSize: 25,
        totalPages: 1,
      })

    vi.mocked(androidEnterpriseApi.getDeviceSummary)
      .mockResolvedValue(summary({
        total: 1,
        managed: 1,
        fullyManaged: 1,
        compliant: 1,
        lastSynchronizationUtc: '2026-10-18T12:00:00Z',
      }))

    renderCenter()

    expect(
      await screen.findByText('SERIAL-1'),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', {
        name: /Gestionar/,
      }),
    )

    expect(
      screen.getByText('Versión Android'),
    ).toBeInTheDocument()

    expect(
      screen.getByText('14 · API 34'),
    ).toBeInTheDocument()

    expect(
      screen.getByText(
        'Última sincronización de inventario',
      ),
    ).toBeInTheDocument()
  })
})
