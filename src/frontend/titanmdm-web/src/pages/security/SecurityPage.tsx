import {
  AlertTriangle,
  Bug,
  KeyRound,
  LockKeyhole,
  RefreshCw,
  ScanSearch,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  TerminalSquare,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import {
  securityApi,
  type DeviceSecurity,
  type SecurityDashboard,
} from '../../api/securityApi'

import './SecurityPage.css'

const emptyDashboard: SecurityDashboard = {
  totalDevices: 0,
  evaluatedDevices: 0,
  compliantDevices: 0,
  nonCompliantDevices: 0,
  rootedDevices: 0,
  adbEnabledDevices: 0,
  developerModeDevices: 0,
  unsecuredDevices: 0,
  criticalRiskDevices: 0,
  averageComplianceScore: 0,
}

export function SecurityPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canCommand = hasPermission('devices.commands')

  const [dashboard, setDashboard] =
    useState<SecurityDashboard>(emptyDashboard)
  const [devices, setDevices] = useState<DeviceSecurity[]>([])
  const [loading, setLoading] = useState(true)
  const [scanning, setScanning] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)

      const [summary, deviceList] = await Promise.all([
        securityApi.getDashboard(),
        securityApi.getDevices(),
      ])

      setDashboard(summary)
      setDevices(deviceList)
    } catch {
      setError('No fue posible cargar la postura de seguridad.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const securityIssues = useMemo(
    () =>
      dashboard.rootedDevices +
      dashboard.adbEnabledDevices +
      dashboard.developerModeDevices +
      dashboard.unsecuredDevices,
    [dashboard],
  )

  async function scanDevice(deviceId: string) {
    if (!canCommand) return

    try {
      setScanning(deviceId)
      setError(null)
      setMessage(null)

      await securityApi.sendSecurityScan(deviceId)

      setMessage(
        'Comando aceptado por el backend. La ejecución depende del agente.',
      )
    } catch {
      setError('No fue posible enviar el análisis de seguridad.')
    } finally {
      setScanning(null)
    }
  }

  async function scanFleet() {
    if (!canCommand || devices.length === 0) return

    try {
      setScanning('ALL')
      setMessage(null)
      setError(null)

      let accepted = 0
      let failed = 0

      for (let offset = 0; offset < devices.length; offset += 5) {
        const results = await Promise.allSettled(
          devices
            .slice(offset, offset + 5)
            .map(device =>
              securityApi.sendSecurityScan(device.deviceId),
            ),
        )

        accepted += results.filter(
          item => item.status === 'fulfilled',
        ).length

        failed += results.filter(
          item => item.status === 'rejected',
        ).length
      }

      if (failed) {
        setError(`${failed} comandos no pudieron enviarse.`)
      }

      setMessage(
        `Comandos aceptados: ${accepted}. Actualiza después para consultar resultados del agente.`,
      )
    } catch {
      setError(
        'No fue posible iniciar el análisis de toda la flota.',
      )
    } finally {
      setScanning(null)
    }
  }

  return (
    <div className="security-page">
      <header className="security-header">
        <div>
          <span className="security-eyebrow">
            TITANMDM SECURITY
          </span>

          <h1>Seguridad</h1>

          <p>
            Postura de seguridad e integridad de los dispositivos
            administrados.
          </p>
        </div>

        <div className="security-header-actions">
          <button
            className="security-secondary-button"
            type="button"
            onClick={() => void load()}
            disabled={loading}
          >
            <RefreshCw size={16} />
            Actualizar
          </button>

          <button
            className="security-primary-button"
            type="button"
            onClick={() => void scanFleet()}
            disabled={
              !canCommand ||
              scanning !== null ||
              devices.length === 0
            }
          >
            <ScanSearch size={16} />
            Analizar flota
          </button>
        </div>
      </header>

      {message && (
        <div className="security-message success">{message}</div>
      )}

      {error && (
        <div className="security-message error">{error}</div>
      )}

      <section className="security-stats">
        <StatCard
          icon={<Smartphone size={20} />}
          label="Dispositivos"
          value={dashboard.totalDevices}
          helper={`${dashboard.evaluatedDevices} evaluados`}
        />

        <StatCard
          icon={<ShieldCheck size={20} />}
          label="Score promedio"
          value={`${dashboard.averageComplianceScore}%`}
          helper="Cumplimiento de seguridad"
        />

        <StatCard
          icon={<ShieldAlert size={20} />}
          label="Riesgo crítico"
          value={dashboard.criticalRiskDevices}
          helper="Requieren atención"
          danger={dashboard.criticalRiskDevices > 0}
        />

        <StatCard
          icon={<AlertTriangle size={20} />}
          label="Señales detectadas"
          value={securityIssues}
          helper="Root, ADB, desarrollo o bloqueo"
          danger={securityIssues > 0}
        />
      </section>

      <section className="security-risk-grid">
        <RiskCard
          icon={<Bug size={18} />}
          title="Root detectado"
          value={dashboard.rootedDevices}
        />
        <RiskCard
          icon={<TerminalSquare size={18} />}
          title="ADB habilitado"
          value={dashboard.adbEnabledDevices}
        />
        <RiskCard
          icon={<KeyRound size={18} />}
          title="Modo desarrollador"
          value={dashboard.developerModeDevices}
        />
        <RiskCard
          icon={<LockKeyhole size={18} />}
          title="Sin bloqueo seguro"
          value={dashboard.unsecuredDevices}
        />
      </section>

      <section className="security-panel">
        <div className="security-panel-heading">
          <div>
            <strong>Postura por dispositivo</strong>
            <span>
              Estado reportado por los agentes Windows y Android.
              Resumen global de la organización.
            </span>
          </div>
        </div>

        <div className="security-table-wrapper">
          <table className="security-table">
            <thead>
              <tr>
                <th>DISPOSITIVO</th>
                <th>RIESGO</th>
                <th>SCORE</th>
                <th>BLOQUEO</th>
                <th>CIFRADO</th>
                <th>ROOT</th>
                <th>ADB</th>
                <th>PARCHE</th>
                <th>ÚLTIMO ANÁLISIS</th>
                <th>ACCIONES</th>
              </tr>
            </thead>

            <tbody>
              {!loading && devices.length === 0 && (
                <tr>
                  <td colSpan={10} className="security-empty">
                    Aún no existen evaluaciones de seguridad.
                  </td>
                </tr>
              )}

              {devices.map(device => (
                <tr key={device.deviceId}>
                  <td>
                    <button
                      type="button"
                      className="security-device-link"
                      onClick={() =>
                        navigate(`/devices/${device.deviceId}`)
                      }
                    >
                      <strong>{device.deviceName}</strong>
                      <span>{device.platform}</span>
                    </button>
                  </td>

                  <td>
                    <RiskBadge value={device.riskLevel} />
                  </td>

                  <td>
                    <strong>{device.complianceScore}%</strong>
                  </td>

                  <td>
                    <BooleanBadge value={device.deviceSecure} />
                  </td>

                  <td>{device.encryptionStatus}</td>

                  <td>
                    <BooleanBadge
                      value={!device.rootDetected}
                      good="No"
                      bad="Sí"
                    />
                  </td>

                  <td>
                    <BooleanBadge
                      value={!device.adbEnabled}
                      good="Off"
                      bad="On"
                    />
                  </td>

                  <td>{device.securityPatchLevel ?? 'N/D'}</td>

                  <td>
                    {formatDate(device.lastSecurityScanAtUtc)}
                  </td>

                  <td>
                    {device.platform === 'Windows' &&
                      canCommand && (
                        <button
                          type="button"
                          className="security-row-action"
                          onClick={() =>
                            navigate(
                              `/devices/${device.deviceId}/control-center?workspace=windows`,
                            )
                          }
                        >
                          Centro de control
                        </button>
                      )}

                    <button
                      type="button"
                      className="security-row-action"
                      disabled={
                        !canCommand || scanning !== null
                      }
                      onClick={() =>
                        void scanDevice(device.deviceId)
                      }
                    >
                      <ScanSearch size={15} />
                      {scanning === device.deviceId
                        ? 'Enviando...'
                        : 'Analizar'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

function StatCard({
  icon,
  label,
  value,
  helper,
  danger = false,
}: {
  icon: ReactNode
  label: string
  value: string | number
  helper: string
  danger?: boolean
}) {
  return (
    <article
      className={
        danger ? 'security-stat danger' : 'security-stat'
      }
    >
      <div className="security-stat-icon">{icon}</div>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{helper}</small>
      </div>
    </article>
  )
}

function RiskCard({
  icon,
  title,
  value,
}: {
  icon: ReactNode
  title: string
  value: number
}) {
  return (
    <article className="security-risk-card">
      {icon}
      <div>
        <strong>{value}</strong>
        <span>{title}</span>
      </div>
    </article>
  )
}

function RiskBadge({ value }: { value: string }) {
  return (
    <span
      className={`security-risk-badge ${value.toLowerCase()}`}
    >
      {value}
    </span>
  )
}

function BooleanBadge({
  value,
  good = 'Seguro',
  bad = 'Riesgo',
}: {
  value: boolean
  good?: string
  bad?: string
}) {
  return (
    <span
      className={
        value
          ? 'security-boolean good'
          : 'security-boolean bad'
      }
    >
      {value ? good : bad}
    </span>
  )
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'Nunca'
}