import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'

import {
  androidEnterpriseApi,
} from '../../../api/androidEnterpriseApi'
import { devicesApi } from '../../../api/devicesApi'
import type {
  AndroidDeviceInventorySummary,
  AndroidDeviceSyncResult,
} from '../../../types/androidEnterprise'
import type { DeviceListItem } from '../../../types/device'
import {
  getDevicesErrorMessage,
} from '../utils/devicesPage.utils'

export type DeviceManagedFilter = '' | 'managed' | 'unmanaged'
export type DeviceSortDirection = 'asc' | 'desc'

interface UseDevicesInventoryOptions {
  effectivePlatform: string
  isAndroidWorkspace: boolean
}

export function useDevicesInventory({
  effectivePlatform,
  isAndroidWorkspace,
}: UseDevicesInventoryOptions) {
  const generation = useRef(0)

  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [compliance, setCompliance] = useState('')
  const [managedFilter, setManagedFilter] =
    useState<DeviceManagedFilter>('')
  const [sortBy, setSortBy] = useState('lastSeen')
  const [sortDirection, setSortDirection] =
    useState<DeviceSortDirection>('desc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)

  const [isLoading, setIsLoading] = useState(true)
  const [isSyncingAndroid, setIsSyncingAndroid] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [androidError, setAndroidError] =
    useState<string | null>(null)

  const [androidSummary, setAndroidSummary] =
    useState<AndroidDeviceInventorySummary | null>(null)
  const [lastSyncResult, setLastSyncResult] =
    useState<AndroidDeviceSyncResult | null>(null)

  const loadDevices = useCallback(async () => {
    const request = ++generation.current

    try {
      setIsLoading(true)
      setError(null)

      const response = await devicesApi.getDevices({
        search,
        platform: effectivePlatform,
        status,
        compliance,
        managed:
          managedFilter === 'managed'
            ? true
            : managedFilter === 'unmanaged'
              ? false
              : undefined,
        sortBy,
        sortDirection,
        page,
        pageSize,
      })

      if (request !== generation.current) return

      setDevices(response.items)
      setTotal(response.totalCount)
      setTotalPages(response.totalPages)

      if (response.totalPages > 0 && page > response.totalPages) {
        setPage(response.totalPages)
      }
    } catch (loadError) {
      if (request !== generation.current) return

      setDevices([])
      setTotal(0)
      setTotalPages(0)
      setError(
        getDevicesErrorMessage(
          loadError,
          'No fue posible obtener el inventario de dispositivos.',
        ),
      )
    } finally {
      if (request === generation.current) {
        setIsLoading(false)
      }
    }
  }, [
    search,
    effectivePlatform,
    status,
    compliance,
    managedFilter,
    sortBy,
    sortDirection,
    page,
    pageSize,
  ])

  const loadAndroidSummary = useCallback(async () => {
    if (!isAndroidWorkspace) {
      setAndroidSummary(null)
      setAndroidError(null)
      return
    }

    try {
      const summary = await androidEnterpriseApi.getDeviceSummary()
      setAndroidSummary(summary)
      setAndroidError(null)
    } catch (summaryError) {
      setAndroidError(
        getDevicesErrorMessage(
          summaryError,
          'No fue posible consultar el resumen de Android Enterprise.',
        ),
      )
    }
  }, [isAndroidWorkspace])

  const synchronizeAndroid = useCallback(async () => {
    if (!isAndroidWorkspace || isSyncingAndroid) return

    try {
      setIsSyncingAndroid(true)
      setAndroidError(null)
      setLastSyncResult(null)

      const result =
        await androidEnterpriseApi.synchronizeDevices()

      setLastSyncResult(result)

      await Promise.all([
        loadAndroidSummary(),
        loadDevices(),
      ])
    } catch (syncError) {
      setAndroidError(
        getDevicesErrorMessage(
          syncError,
          'No fue posible sincronizar Android Enterprise.',
        ),
      )
    } finally {
      setIsSyncingAndroid(false)
    }
  }, [
    isAndroidWorkspace,
    isSyncingAndroid,
    loadAndroidSummary,
    loadDevices,
  ])

  useEffect(() => {
    setPage(1)
  }, [
    search,
    effectivePlatform,
    status,
    compliance,
    managedFilter,
    sortBy,
    sortDirection,
    pageSize,
  ])

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      void loadDevices()
    }, 250)

    return () => {
      window.clearTimeout(timeout)
      generation.current++
    }
  }, [loadDevices])

  useEffect(() => {
    if (isAndroidWorkspace) {
      void loadAndroidSummary()
    }
  }, [isAndroidWorkspace, loadAndroidSummary])

  const onlineDevices = useMemo(
    () => devices.filter(x => x.status === 'Online').length,
    [devices],
  )

  const offlineDevices = useMemo(
    () => devices.filter(x => x.status === 'Offline').length,
    [devices],
  )

  const compliantDevices = useMemo(
    () => devices.filter(x =>
      x.complianceStatus === 'Compliant',
    ).length,
    [devices],
  )

  function changePageSize(value: number) {
    setPageSize(value)
    setPage(1)
  }

  function goToPreviousPage() {
    setPage(current => Math.max(1, current - 1))
  }

  function goToNextPage() {
    setPage(current =>
      Math.min(Math.max(totalPages, 1), current + 1),
    )
  }

  return {
    devices,
    total,
    totalPages,
    onlineDevices,
    offlineDevices,
    compliantDevices,

    search,
    setSearch,
    status,
    setStatus,
    compliance,
    setCompliance,
    managedFilter,
    setManagedFilter,
    sortBy,
    setSortBy,
    sortDirection,
    setSortDirection,

    page,
    pageSize,
    changePageSize,
    goToPreviousPage,
    goToNextPage,

    isLoading,
    error,
    androidSummary,
    androidError,
    lastSyncResult,
    isSyncingAndroid,

    loadDevices,
    loadAndroidSummary,
    synchronizeAndroid,
  }
}