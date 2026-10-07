import {
  RefreshCw,
  ShieldAlert,
} from 'lucide-react'

import {
  useEffect,
  useState,
} from 'react'

import {
  Navigate,
  useParams,
} from 'react-router-dom'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceDetails,
} from '../../types/device'

export function DeviceEntryPage() {
  const {
    deviceId,
  } =
    useParams<{
      deviceId: string
    }>()

  const [
    device,
    setDevice,
  ] =
    useState<
      DeviceDetails | null
    >(null)

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(null)

  useEffect(
    () => {
      let cancelled =
        false

      async function load() {
        if (
          !deviceId
        ) {
          setError(
            'No se recibió un DeviceId válido.',
          )

          setLoading(
            false,
          )

          return
        }

        try {
          const result =
            await devicesApi
              .getDeviceById(
                deviceId,
              )

          if (
            !cancelled
          ) {
            setDevice(
              result,
            )
          }
        } catch (
          loadError
        ) {
          console.error(
            loadError,
          )

          if (
            !cancelled
          ) {
            setError(
              'No fue posible resolver el dispositivo.',
            )
          }
        } finally {
          if (
            !cancelled
          ) {
            setLoading(
              false,
            )
          }
        }
      }

      void load()

      return () => {
        cancelled =
          true
      }
    },
    [
      deviceId,
    ],
  )

  if (
    loading
  ) {
    return (
      <div className="windows-control-loading">
        <RefreshCw
          size={25}
          className="windows-control-spin"
        />

        Resolviendo dispositivo...
      </div>
    )
  }

  if (
    error
    ||
    !device
  ) {
    return (
      <div className="windows-control-error-page">
        <ShieldAlert
          size={32}
        />

        <h2>
          No fue posible abrir el dispositivo
        </h2>

        <p>
          {error}
        </p>
      </div>
    )
  }

  if (
    device.platform ===
    'Windows'
  ) {
    return (
      <Navigate
        replace
        to={
          `/devices/${device.id}/control-center?workspace=windows`
        }
      />
    )
  }

  if (
    device.platform ===
    'Android'
  ) {
    return (
      <Navigate
        replace
        to={
          `/devices/${device.id}/android?workspace=android`
        }
      />
    )
  }

  return (
    <Navigate
      replace
      to="/devices"
    />
  )
}