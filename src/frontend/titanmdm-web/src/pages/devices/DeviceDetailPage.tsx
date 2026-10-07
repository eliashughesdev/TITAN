import {
  ArrowLeft,
  CheckCircle2,
  RefreshCw,
  XCircle,
} from 'lucide-react'

import {
  useState,
} from 'react'

import {
  useNavigate,
  useParams,
} from 'react-router-dom'

import {
  DeviceCommandsPanel,
} from './components/DeviceCommandsPanel'

import {
  DeviceDetailHeader,
} from './components/DeviceDetailHeader'

import {
  DeviceDetailKpis,
} from './components/DeviceDetailKpis'

import {
  DeviceDetailTabContent,
} from './components/DeviceDetailTabContent'

import {
  DeviceDetailTabs,
  type DeviceDetailTab,
} from './components/DeviceDetailTabs'

import {
  DeviceOperationalPanel,
} from './components/DeviceOperationalPanel'

import {
  useDeviceDetail,
} from './hooks/useDeviceDetail'

import './DeviceDetailPage.css'

export function DeviceDetailPage() {
  const {
    deviceId,
  } =
    useParams<{
      deviceId: string
    }>()

  const navigate =
    useNavigate()

  const [
    activeTab,
    setActiveTab,
  ] =
    useState<DeviceDetailTab>(
      'overview',
    )

  const {
    device,
    androidDetails,
    commands,

    loading,
    sendingCommand,

    error,
    message,

    loadDevice,
    refreshCommands,
    sendCommand,
  } =
    useDeviceDetail(
      deviceId,
    )

  if (
    loading
    &&
    device === null
  ) {
    return (
      <div className="device-detail-loading">
        <RefreshCw
          size={24}
          className="device-detail-spin"
        />

        <span>
          Cargando dispositivo...
        </span>
      </div>
    )
  }

  if (
    device ===
    null
  ) {
    return (
      <div className="device-detail-error">
        <XCircle
          size={28}
        />

        <h2>
          Dispositivo no disponible
        </h2>

        <p>
          {error
            ??
            'No fue posible localizar el dispositivo.'}
        </p>

        <button
          type="button"
          onClick={() =>
            navigate(
              '/devices',
            )
          }
        >
          Volver a dispositivos
        </button>
      </div>
    )
  }

  const isAndroid =
    device.platform ===
    'Android'

  return (
    <div className="device-detail-page">
      <button
        type="button"
        className="device-detail-back"
        onClick={() =>
          navigate(
            '/devices',
          )
        }
      >
        <ArrowLeft
          size={16}
        />

        Dispositivos
      </button>

      <DeviceDetailHeader
        device={
          device
        }

        loading={
          loading
        }

        sendingCommand={
          sendingCommand
        }

        onRefresh={() =>
          void loadDevice()
        }

        onCommand={
          commandType => {
            void sendCommand(
              commandType,
            )

            setActiveTab(
              'commands',
            )
          }
        }
      />

      {error && (
        <div className="device-detail-notice device-detail-notice--error">
          <XCircle
            size={17}
          />

          <span>
            {error}
          </span>
        </div>
      )}

      {message && (
        <div className="device-detail-notice device-detail-notice--success">
          <CheckCircle2
            size={17}
          />

          <span>
            {message}
          </span>
        </div>
      )}

      <DeviceDetailKpis
        device={
          device
        }
      />

      {deviceId && (
        <DeviceOperationalPanel
          deviceId={
            deviceId
          }
        />
      )}

      <DeviceDetailTabs
        activeTab={
          activeTab
        }

        isAndroid={
          isAndroid
        }

        commandCount={
          commands.length
        }

        onChange={
          setActiveTab
        }
      />

      {activeTab ===
      'commands' ? (
        <DeviceCommandsPanel
          commands={
            commands
          }

          isAndroid={
            isAndroid
          }

          onRefresh={() =>
            void refreshCommands()
          }
        />
      ) : (
        <DeviceDetailTabContent
          tab={
            activeTab
          }

          device={
            device
          }

          androidDetails={
            androidDetails
          }
        />
      )}
    </div>
  )
} 