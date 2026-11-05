import {
  AlertTriangle,
  BatteryCharging,
  Clock3,
  Cpu,
  HardDrive,
  RefreshCw,
  ShieldCheck,
  Wifi,
} from 'lucide-react'

import type {
  AndroidDeviceCapabilities,
} from '../../../../types/androidEnterprise'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
} from '../../../../types/device'

import {
  formatConsoleDate,
} from './androidRemoteConsole.utils'

interface Props {
  capabilities: AndroidDeviceCapabilities | null
  details: AndroidDeviceDetails | null
  device: DeviceDetails | null
  loading: boolean
  error: string | null
  onRefresh: () => void
}

export function AndroidDiagnosticsPanel({
  capabilities,
  details,
  device,
  loading,
  error,
  onRefresh,
}: Props) {
  return (
    <section className="arc-card" aria-label="Diagnóstico del dispositivo">
      <header className="arc-card__head">
        <div>
          <h3>Diagnóstico de configuración</h3>
          <p>
            Estado reportado por Android Management API y el inventario
            TitanMDM.
          </p>
        </div>
        <button
          type="button"
          className="arc-ghost-button"
          onClick={onRefresh}
          disabled={loading}
        >
          <RefreshCw size={14} />
          Reconsultar
        </button>
      </header>

      {error && (
        <div className="arc-inline-note arc-inline-note--error">
          <AlertTriangle size={16} />
          {error}
        </div>
      )}

      <div className="arc-diagnostic-grid">
        <article className="arc-diagnostic-item">
          <Cpu size={16} />
          <span>Plataforma</span>
          <strong>
            Android {capabilities?.androidVersion ?? 'N/D'}
          </strong>
          <small>
            {capabilities?.apiLevel
              ? `API ${capabilities.apiLevel}`
              : 'Nivel de API sin confirmar'}
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <ShieldCheck size={16} />
          <span>Parche de seguridad</span>
          <strong>
            {details?.securityPatchLevel ??
              capabilities?.securityPatchLevel ??
              'Sin información'}
          </strong>
          <small>
            {details?.encryptionStatus
              ? `Cifrado: ${details.encryptionStatus}`
              : 'Cifrado sin reportar'}
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <BatteryCharging size={16} />
          <span>Política aplicada</span>
          <strong>
            {details?.appliedPolicyName ?? 'Sin política'}
          </strong>
          <small>
            {details?.appliedPolicyState
              ? `Estado: ${details.appliedPolicyState}`
              : 'Estado de aplicación desconocido'}
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <ShieldCheck size={16} />
          <span>Postura de seguridad</span>
          <strong>
            {details?.securityPosture ?? 'Sin información'}
          </strong>
          <small>
            {details?.lastPolicySyncTimeUtc
              ? `Última sincronización de política: ${formatConsoleDate(
                  details.lastPolicySyncTimeUtc,
                )}`
              : 'Sin sincronización de política reportada'}
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <Clock3 size={16} />
          <span>Última sincronización</span>
          <strong>
            {formatConsoleDate(
              details?.lastSynchronizedAtUtc,
            )}
          </strong>
          <small>
            Inventario TitanMDM actualizado desde Android Management API.
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <Clock3 size={16} />
          <span>Último reporte de estado</span>
          <strong>
            {formatConsoleDate(
              details?.lastStatusReportTimeUtc,
            )}
          </strong>
          <small>
            Fecha reportada por Google para el último estado del dispositivo.
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <BatteryCharging size={16} />
          <span>Batería</span>
          <strong>
            {device?.batteryLevel !== null &&
            device?.batteryLevel !== undefined
              ? `${device.batteryLevel}%`
              : 'No reportada'}
          </strong>
          <small>Último valor recibido por el inventario TitanMDM.</small>
        </article>

        <article className="arc-diagnostic-item">
          <Wifi size={16} />
          <span>Conectividad</span>
          <strong>{device?.status ?? 'Sin información'}</strong>
          <small>
            {device?.ipAddress
              ? `IP reportada: ${device.ipAddress}`
              : 'Dirección de red no reportada'}
          </small>
        </article>

        <article className="arc-diagnostic-item">
          <HardDrive size={16} />
          <span>Almacenamiento</span>
          <strong>No reportado</strong>
          <small>
            El contrato actual del agente y Android Management API no expone
            una medición autorizada.
          </small>
        </article>
      </div>
    </section>
  )
}
