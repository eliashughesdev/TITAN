import { useEffect, useState } from 'react'
import {
  Activity,
  Battery,
  Download,
  Laptop,
  MonitorSmartphone,
  RefreshCw,
  ShieldAlert,
  Smartphone,
  Wifi,
  WifiOff,
} from 'lucide-react'

import type {
  DeviceListItem,
  DeviceStatus,
} from '../../../types/device'
import {
  formatDeviceDateTime,
} from '../utils/devicesPage.utils'
import './DevicesTable.css'

type Props = {
  devices: DeviceListItem[]
  isLoading: boolean
  isWindowsWorkspace: boolean
  isAndroidWorkspace: boolean
  onOpenDevice: (device: DeviceListItem) => void
}

const statuses: Record<string, string> = {
  Pending: 'Pendiente',
  Enrolling: 'Inscribiendo',
  Online: 'En línea',
  Offline: 'Fuera de línea',
  Locked: 'Bloqueado',
  Quarantined: 'En cuarentena',
  Retired: 'Retirado',
  Wiped: 'Borrado',
}

const compliance: Record<string, string> = {
  Unknown: 'Sin evaluar',
  Evaluating: 'Evaluando',
  Compliant: 'Conforme',
  NonCompliant: 'No conforme',
  Quarantined: 'En cuarentena',
}

function platformIcon(platform: string) {
  return platform === 'Android'
    ? <Smartphone size={18} />
    : platform === 'Windows'
      ? <Laptop size={18} />
      : <MonitorSmartphone size={18} />
}

function statusIcon(status: DeviceStatus) {
  return status === 'Online'
    ? <Wifi size={14} />
    : status === 'Offline'
      ? <WifiOff size={14} />
      : status === 'Quarantined'
        ? <ShieldAlert size={14} />
        : <Activity size={14} />
}

function csvCell(value: unknown) {
  const raw = String(value ?? '')
  const safe = /^[\s]*[=+@-]/.test(raw) ? "'" + raw : raw
  return '"' + safe.replace(/"/g, '""') + '"'
}

export function DevicesTable({
  devices,
  isLoading,
  isWindowsWorkspace,
  isAndroidWorkspace,
  onOpenDevice,
}: Props) {
  const [selected, setSelected] = useState<string[]>([])

  useEffect(() => {
    setSelected(current =>
      current.filter(id => devices.some(x => x.id === id)),
    )
  }, [devices])

  const all =
    devices.length > 0 &&
    devices.every(x => selected.includes(x.id))

  const rows = devices.filter(x => selected.includes(x.id))

  const toggle = (id: string) => {
    setSelected(current =>
      current.includes(id)
        ? current.filter(x => x !== id)
        : [...current, id],
    )
  }

  function exportRows() {
    if (!rows.length) return

    const headers = [
      'Equipo',
      'Plataforma',
      'Estado',
      'Usuario',
      'Departamento',
      'Fabricante',
      'Modelo',
      'Serial',
      'Sistema operativo',
      'Versión',
      'IP',
      'Administrado',
      'Cumplimiento',
      'Última comunicación UTC',
    ]

    const lines = rows.map(x => [
      x.deviceName,
      x.platform,
      statuses[x.status] ?? x.status,
      x.assignedUser,
      x.department,
      x.manufacturer,
      x.model,
      x.serialNumber,
      x.operatingSystem,
      x.operatingSystemVersion,
      x.ipAddress,
      x.isManaged ? 'Sí' : 'No',
      compliance[x.complianceStatus] ?? x.complianceStatus,
      x.lastSeenAtUtc,
    ])

    const csv = '\uFEFF' +
      [headers, ...lines]
        .map(x => x.map(csvCell).join(';'))
        .join('\r\n')

    const url = URL.createObjectURL(
      new Blob([csv], { type: 'text/csv;charset=utf-8' }),
    )

    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download =
      `TitanMDM-inventario-${new Date().toISOString().slice(0, 10)}.csv`

    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()

    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  const columns = isWindowsWorkspace ? 8 : 9

  return (
    <div className="wit">
      <div className="wit-actions">
        <div>
          <strong>{selected.length} seleccionados</strong>
          <span>
            La selección y exportación corresponden a esta página.
          </span>
        </div>

        <button
          disabled={isLoading || !rows.length}
          onClick={exportRows}
        >
          <Download size={16} /> Exportar selección
        </button>

        <button
          disabled={!selected.length || isLoading}
          onClick={() => setSelected([])}
        >
          Limpiar selección
        </button>
      </div>

      <div className="devices-table-wrapper">
        <table
          className="devices-table"
          aria-label="Inventario de dispositivos"
        >
          <thead>
            <tr>
              <th>
                <input
                  type="checkbox"
                  aria-label="Seleccionar todos los equipos de esta página"
                  checked={all}
                  disabled={isLoading || !devices.length}
                  onChange={e => setSelected(
                    e.target.checked
                      ? devices.map(x => x.id)
                      : [],
                  )}
                />
              </th>
              <th>Dispositivo</th>
              <th>Sistema operativo</th>
              <th>Estado</th>
              <th>Usuario y área</th>
              <th>Red</th>
              <th>Cumplimiento</th>
              {!isWindowsWorkspace && <th>Batería</th>}
              <th>Última comunicación</th>
            </tr>
          </thead>

          <tbody>
            {isLoading && !devices.length ? (
              <tr>
                <td colSpan={columns} className="devices-empty">
                  <RefreshCw
                    size={22}
                    className="devices-icon-spinning"
                  />
                  Cargando inventario…
                </td>
              </tr>
            ) : !devices.length ? (
              <tr>
                <td colSpan={columns} className="devices-empty">
                  <MonitorSmartphone size={28} />
                  <strong>No hay dispositivos</strong>
                  <span>
                    {isWindowsWorkspace
                      ? 'No existen equipos Windows para este filtro.'
                      : isAndroidWorkspace
                        ? 'No existen dispositivos Android para este filtro.'
                        : 'No existen dispositivos para este filtro.'}
                  </span>
                </td>
              </tr>
            ) : devices.map(device => (
              <tr key={device.id}>
                <td>
                  <input
                    type="checkbox"
                    checked={selected.includes(device.id)}
                    disabled={isLoading}
                    aria-label={`Seleccionar ${device.deviceName}`}
                    onChange={() => toggle(device.id)}
                  />
                </td>

                <td>
                  <div className="device-identity">
                    <div className="device-platform-icon">
                      {platformIcon(device.platform)}
                    </div>

                    <div>
                      <button
                        className="wit-device-link"
                        onClick={() => onOpenDevice(device)}
                      >
                        {device.deviceName}
                      </button>

                      <span>
                        {device.manufacturer ??
                          'Fabricante sin reportar'}
                        {' · '}
                        {device.model ?? 'Modelo sin reportar'}
                      </span>

                      <small>
                        SN: {device.serialNumber || 'Sin reportar'}
                      </small>
                    </div>
                  </div>
                </td>

                <td>
                  <div className="device-user">
                    <strong>
                      {device.operatingSystem ?? device.platform}
                    </strong>
                    <span>
                      {device.operatingSystemVersion ??
                        'Versión sin reportar'}
                    </span>
                  </div>
                </td>

                <td>
                  <span
                    className={
                      `device-status device-status--${device.status.toLowerCase()}`
                    }
                  >
                    {statusIcon(device.status)}
                    {statuses[device.status] ?? device.status}
                  </span>

                  <small className="wit-managed">
                    {device.isManaged
                      ? 'Administrado'
                      : 'Sin administrar'}
                  </small>
                </td>

                <td>
                  <div className="device-user">
                    <strong>
                      {device.assignedUser ?? 'Sin asignar'}
                    </strong>
                    <span>
                      {device.department ?? 'Sin departamento'}
                    </span>
                  </div>
                </td>

                <td>
                  <code>
                    {device.ipAddress ?? 'Sin IP reportada'}
                  </code>
                  <small className="wit-managed">
                    {device.platform}
                  </small>
                </td>

                <td>
                  <span className={
                    `device-compliance device-compliance--${device.complianceStatus.toLowerCase()}`
                  }>
                    {compliance[device.complianceStatus] ??
                      device.complianceStatus}
                  </span>
                </td>

                {!isWindowsWorkspace && (
                  <td>
                    <div className="device-battery">
                      <Battery size={16} />
                      <span>
                        {device.batteryLevel !== null
                          ? `${device.batteryLevel}%`
                          : 'N/D'}
                      </span>
                    </div>
                  </td>
                )}

                <td>
                  {formatDeviceDateTime(
                    device.lastSeenAtUtc,
                    'Sin comunicación',
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}