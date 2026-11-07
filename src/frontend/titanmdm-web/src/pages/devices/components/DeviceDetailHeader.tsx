import {
  Database,
  Laptop,
  Play,
  RefreshCw,
  Smartphone,
  Wifi,
} from 'lucide-react'

import type {
  DeviceDetails,
} from '../../../types/device'

import { DeviceAliasEditor } from './DeviceAliasEditor'

interface Props {
  device: DeviceDetails
  loading: boolean

  sendingCommand:
    | string
    | null

  onRefresh:
    () => void

  onCommand:
    (
      commandType: string,
    ) => void

  onAliasSaved?:
    (alias: string | null) => void
}

export function DeviceDetailHeader({
  device,
  loading,
  sendingCommand,
  onRefresh,
  onCommand,
  onAliasSaved,
}: Props) {
  const isAndroid =
    device.platform ===
    'Android'

  return (
    <header className="device-detail-header">
      <div className="device-detail-identity">
        <div className="device-detail-device-icon">
          {isAndroid ? (
            <Smartphone
              size={27}
            />
          ) : (
            <Laptop
              size={27}
            />
          )}
        </div>

        <div>
          <div className="device-detail-title-line">
            <h1>
              {device.alias || device.deviceName}
            </h1>

            <DeviceAliasEditor
              deviceId={device.id}
              alias={device.alias}
              onSaved={onAliasSaved}
            />

            <span
              className={
                `device-detail-status ` +
                `device-detail-status--${device.status.toLowerCase()}`
              }
            >
              <Wifi
                size={13}
              />

              {device.status}
            </span>
          </div>

          <p>
            {device.platform}
            {' · '}

            {device.manufacturer
              ??
              'Fabricante desconocido'}

            {' · '}

            {device.model
              ??
              'Modelo desconocido'}
          </p>

          <small>
            ID: {device.id}
          </small>
        </div>
      </div>

      <div className="device-detail-actions">
        <button
          type="button"
          className="device-action-secondary"
          disabled={loading}
          onClick={onRefresh}
        >
          <RefreshCw
            size={16}
          />

          Actualizar
        </button>

        <button
          type="button"
          className="device-action-primary"
          disabled={
            sendingCommand !==
            null
          }
          onClick={() =>
            onCommand(
              'PING',
            )
          }
        >
          <Play
            size={16}
          />

          {sendingCommand ===
          'PING'
            ? 'Enviando...'
            : 'PING'}
        </button>

        <button
          type="button"
          className="device-action-primary"
          disabled={
            sendingCommand !==
            null
          }
          onClick={() =>
            onCommand(
              'DEVICE_INFO',
            )
          }
        >
          <Database
            size={16}
          />

          {sendingCommand ===
          'DEVICE_INFO'
            ? 'Solicitando...'
            : 'DEVICE INFO'}
        </button>
      </div>
    </header>
  )
}