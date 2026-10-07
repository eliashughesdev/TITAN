import {
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  useNavigate,
  useSearchParams,
} from 'react-router-dom'

import {
  useWorkspace,
} from '../../workspace/WorkspaceContext'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  AndroidInventoryPanel,
} from './components/AndroidInventoryPanel'

import {
  DevicesHeader,
} from './components/DevicesHeader'

import {
  DevicesPagination,
} from './components/DevicesPagination'

import {
  DevicesSummary,
} from './components/DevicesSummary'

import {
  DevicesTable,
} from './components/DevicesTable'

import {
  DevicesToolbar,
} from './components/DevicesToolbar'

import {
  useDevicesInventory,
} from './hooks/useDevicesInventory'

import './DevicesPage.css'

export function DevicesPage() {
  const navigate =
    useNavigate()

  const [
    searchParams,
  ] =
    useSearchParams()

  const {
    activeWorkspaceId,
  } =
    useWorkspace()

  /* ============================================================
     WORKSPACE RESOLUTION
     ============================================================ */

  const workspace =
    useMemo(
      () => {
        const urlWorkspace =
          searchParams
            .get(
              'workspace',
            )
            ?.toLowerCase()

        if (
          urlWorkspace ===
          'windows'
        ) {
          return 'windows'
        }

        if (
          urlWorkspace ===
          'android'
        ) {
          return 'android'
        }

        if (
          urlWorkspace ===
          'global'
        ) {
          return 'global'
        }

        if (
          activeWorkspaceId ===
          'windows'
        ) {
          return 'windows'
        }

        if (
          activeWorkspaceId ===
          'android'
        ) {
          return 'android'
        }

        return 'global'
      },
      [
        searchParams,
        activeWorkspaceId,
      ],
    )

  const isWindowsWorkspace =
    workspace ===
    'windows'

  const isAndroidWorkspace =
    workspace ===
    'android'

  const isGlobalWorkspace =
    workspace ===
    'global'

  /* ============================================================
     PLATFORM
     ============================================================ */

  const requestedPlatform =
    searchParams.get(
      'platform',
    )

  const initialGlobalPlatform =
    requestedPlatform ===
      'Windows'
      ||
      requestedPlatform ===
        'Android'
      ? requestedPlatform
      : ''

  const [
    globalPlatform,
    setGlobalPlatform,
  ] =
    useState(
      initialGlobalPlatform,
    )

  useEffect(
    () => {
      if (
        !isGlobalWorkspace
      ) {
        return
      }

      const platformFromUrl =
        searchParams.get(
          'platform',
        )

      if (
        platformFromUrl ===
          'Windows'
        ||
        platformFromUrl ===
          'Android'
      ) {
        setGlobalPlatform(
          platformFromUrl,
        )

        return
      }

      setGlobalPlatform('')
    },
    [
      searchParams,
      isGlobalWorkspace,
    ],
  )

  const effectivePlatform =
    isWindowsWorkspace
      ? 'Windows'
      : isAndroidWorkspace
        ? 'Android'
        : globalPlatform

  /* ============================================================
     INVENTORY STATE / API
     ============================================================ */

  const inventory =
    useDevicesInventory({
      effectivePlatform,
      isAndroidWorkspace,
    })

  /* ============================================================
     PAGE PRESENTATION
     ============================================================ */

  const pageEyebrow =
    isWindowsWorkspace
      ? 'WINDOWS MANAGEMENT'
      : isAndroidWorkspace
        ? 'ANDROID ENTERPRISE'
        : 'TITANMDM ENTERPRISE'

  const pageDescription =
    isWindowsWorkspace
      ? 'Inventario de endpoints Windows administrados por TitanMDM.'
      : isAndroidWorkspace
        ? 'Inventario Android Enterprise administrado por TitanMDM.'
        : 'Inventario centralizado de endpoints administrados por TitanMDM.'

  const workspaceLabel =
    isWindowsWorkspace
      ? 'Workspace Windows'
      : isAndroidWorkspace
        ? 'Workspace Android'
        : 'Vista global'

  useEffect(
    () => {
      document.title =
        isWindowsWorkspace
          ? 'Dispositivos Windows | TitanMDM'
          : isAndroidWorkspace
            ? 'Dispositivos Android | TitanMDM'
            : 'Dispositivos | TitanMDM'
    },
    [
      isWindowsWorkspace,
      isAndroidWorkspace,
    ],
  )

  /* ============================================================
     NAVIGATION
     ============================================================ */

  function openDevice(
    device:
      DeviceListItem,
  ) {
    const detailWorkspace =
      device.platform ===
        'Windows'
        ? 'windows'
        : device.platform ===
            'Android'
          ? 'android'
          : workspace

    const detailPlatform =
      device.platform ===
        'Windows'
        ||
        device.platform ===
          'Android'
        ? `&platform=${device.platform}`
        : ''

    navigate(
      `/devices/${device.id}?workspace=${detailWorkspace}${detailPlatform}`,
    )
  }

  /* ============================================================
     REFRESH
     ============================================================ */

  function refreshPage() {
    if (
      isAndroidWorkspace
    ) {
      void Promise.all([
        inventory.loadDevices(),
        inventory
          .loadAndroidSummary(),
      ])

      return
    }

    void inventory
      .loadDevices()
  }

  /* ============================================================
     RENDER
     ============================================================ */

  return (
    <div className="devices-page">
      <DevicesHeader
        eyebrow={
          pageEyebrow
        }
        description={
          pageDescription
        }
        isAndroidWorkspace={
          isAndroidWorkspace
        }
        isLoading={
          inventory.isLoading
        }
        isSyncingAndroid={
          inventory
            .isSyncingAndroid
        }
        onRefresh={
          refreshPage
        }
        onAndroidSync={() =>
          void inventory
            .synchronizeAndroid()
        }
      />

      <DevicesSummary
        total={
          inventory.total
        }
        onlineDevices={
          inventory
            .onlineDevices
        }
        offlineDevices={
          inventory
            .offlineDevices
        }
        compliantDevices={
          inventory
            .compliantDevices
        }
        isWindowsWorkspace={
          isWindowsWorkspace
        }
        isAndroidWorkspace={
          isAndroidWorkspace
        }
      />

      {isAndroidWorkspace && (
        <AndroidInventoryPanel
          summary={
            inventory
              .androidSummary
          }
          lastSyncResult={
            inventory
              .lastSyncResult
          }
          error={
            inventory
              .androidError
          }
        />
      )}

      <section className="devices-panel">
        <DevicesToolbar
          search={
            inventory.search
          }
          status={
            inventory.status
          }
          compliance={
            inventory
              .compliance
          }
          managedFilter={
            inventory
              .managedFilter
          }
          sortBy={
            inventory.sortBy
          }
          sortDirection={
            inventory
              .sortDirection
          }
          globalPlatform={
            globalPlatform
          }
          effectivePlatform={
            effectivePlatform
          }
          isGlobalWorkspace={
            isGlobalWorkspace
          }
          onSearchChange={
            inventory.setSearch
          }
          onStatusChange={
            inventory.setStatus
          }
          onComplianceChange={
            inventory
              .setCompliance
          }
          onManagedFilterChange={
            inventory
              .setManagedFilter
          }
          onSortByChange={
            inventory
              .setSortBy
          }
          onSortDirectionChange={
            inventory
              .setSortDirection
          }
          onPlatformChange={
            setGlobalPlatform
          }
        />

        {inventory.error && (
          <div className="devices-error">
            <div>
              <strong>
                Error al cargar dispositivos
              </strong>

              <span>
                {inventory.error}
              </span>
            </div>
          </div>
        )}

        <DevicesTable
          devices={
            inventory.devices
          }
          isLoading={
            inventory.isLoading
          }
          isWindowsWorkspace={
            isWindowsWorkspace
          }
          isAndroidWorkspace={
            isAndroidWorkspace
          }
          onOpenDevice={
            openDevice
          }
        />

        <DevicesPagination
          total={
            inventory.total
          }
          page={
            inventory.page
          }
          pageSize={
            inventory.pageSize
          }
          totalPages={
            inventory
              .totalPages
          }
          isLoading={
            inventory.isLoading
          }
          workspaceLabel={
            workspaceLabel
          }
          onPageSizeChange={
            inventory
              .changePageSize
          }
          onPrevious={
            inventory
              .goToPreviousPage
          }
          onNext={
            inventory
              .goToNextPage
          }
        />
      </section>
    </div>
  )
}