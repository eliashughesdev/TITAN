import {
  AlertCircle,
  BadgeCheck,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  RefreshCw,
  ScanLine,
  ShieldAlert,
  Smartphone,
  XCircle,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import {
  parseFindings,
  securityApi,
  type DeviceSecurity,
  type SecurityDashboard,
} from '../../api/securityApi'

import './CompliancePage.css'

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

export function CompliancePage() {
  const [dashboard, setDashboard] =
    useState(emptyDashboard)

  const [devices, setDevices] =
    useState<DeviceSecurity[]>([])

  const [expanded, setExpanded] =
    useState<string | null>(null)

  const [loading, setLoading] =
    useState(true)

  const [running, setRunning] =
    useState<string | null>(null)

  const [message, setMessage] =
    useState<string | null>(null)

  const [error, setError] =
    useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)

      const [summary, deviceList] =
        await Promise.all([
          securityApi.getDashboard(),
          securityApi.getDevices(),
        ])

      setDashboard(summary)
      setDevices(deviceList)
    } catch {
      setError(
        'No fue posible cargar el estado de cumplimiento.',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const pending =
    useMemo(
      () =>
        Math.max(
          dashboard.totalDevices -
            dashboard.evaluatedDevices,
          0,
        ),
      [dashboard],
    )

  async function evaluate(
    deviceId: string,
  ) {
    try {
      setRunning(deviceId)
      setError(null)
      setMessage(null)

      await securityApi.sendComplianceCheck(
        deviceId,
      )

      setMessage(
        'COMPLIANCE_CHECK fue enviado al dispositivo.',
      )
    } catch {
      setError(
        'No fue posible iniciar la evaluación.',
      )
    } finally {
      setRunning(null)
    }
  }

  async function evaluateFleet() {
    if (devices.length === 0) {
      return
    }

    try {
      setRunning('ALL')
      setError(null)
      setMessage(null)

      await securityApi.checkAll(devices)

      setMessage(
        `Evaluación enviada a ${devices.length} dispositivo(s).`,
      )
    } catch {
      setError(
        'No fue posible evaluar toda la flota.',
      )
    } finally {
      setRunning(null)
    }
  }

  return (
    <div className="compliance-page">
      <header className="compliance-header">
        <div>
          <span className="compliance-eyebrow">
            TITANMDM COMPLIANCE
          </span>

          <h1>Cumplimiento</h1>

          <p>
            Evaluación centralizada de postura,
            riesgos y requisitos de seguridad.
          </p>
        </div>

        <div className="compliance-actions">
          <button
            type="button"
            className="compliance-secondary"
            onClick={() => void load()}
          >
            <RefreshCw size={16} />
            Actualizar
          </button>

          <button
            type="button"
            className="compliance-primary"
            onClick={() =>
              void evaluateFleet()
            }
            disabled={
              running !== null ||
              devices.length === 0
            }
          >
            <ScanLine size={16} />
            Evaluar flota
          </button>
        </div>
      </header>

      {message && (
        <div className="compliance-message success">
          {message}
        </div>
      )}

      {error && (
        <div className="compliance-message error">
          {error}
        </div>
      )}

      <section className="compliance-stats">
        <Stat
          icon={<Smartphone size={20} />}
          label="Evaluados"
          value={`${dashboard.evaluatedDevices}/${dashboard.totalDevices}`}
        />

        <Stat
          icon={<CheckCircle2 size={20} />}
          label="Conformes"
          value={dashboard.compliantDevices}
          good
        />

        <Stat
          icon={<XCircle size={20} />}
          label="No conformes"
          value={
            dashboard.nonCompliantDevices
          }
          danger
        />

        <Stat
          icon={<AlertCircle size={20} />}
          label="Pendientes"
          value={pending}
        />

        <Stat
          icon={<BadgeCheck size={20} />}
          label="Score promedio"
          value={`${dashboard.averageComplianceScore}%`}
        />
      </section>

      <section className="compliance-panel">
        <div className="compliance-panel-header">
          <div>
            <strong>
              Evaluaciones por dispositivo
            </strong>
            <span>
              Expande un dispositivo para consultar
              sus hallazgos.
            </span>
          </div>
        </div>

        {devices.length === 0 &&
        !loading ? (
          <div className="compliance-empty">
            No existen evaluaciones todavía.
          </div>
        ) : (
          <div className="compliance-device-list">
            {devices.map((device) => {
              const findings =
                parseFindings(
                  device.findingsJson,
                )

              const isExpanded =
                expanded ===
                device.deviceId

              return (
                <article
                  key={device.deviceId}
                  className="compliance-device"
                >
                  <div className="compliance-device-main">
                    <button
                      type="button"
                      className="compliance-expand"
                      onClick={() =>
                        setExpanded(
                          isExpanded
                            ? null
                            : device.deviceId,
                        )
                      }
                    >
                      {isExpanded ? (
                        <ChevronUp
                          size={17}
                        />
                      ) : (
                        <ChevronDown
                          size={17}
                        />
                      )}
                    </button>

                    <div className="compliance-device-name">
                      <strong>
                        {device.deviceName}
                      </strong>
                      <span>
                        {device.platform}
                      </span>
                    </div>

                    <StatusBadge
                      status={
                        device.complianceStatus
                      }
                    />

                    <RiskBadge
                      risk={
                        device.riskLevel
                      }
                    />

                    <div className="compliance-score">
                      <strong>
                        {
                          device.complianceScore
                        }
                        %
                      </strong>
                      <span>Score</span>
                    </div>

                    <div className="compliance-checks">
                      <span className="passed">
                        {
                          device.passedChecks
                        }{' '}
                        aprobadas
                      </span>

                      <span className="failed">
                        {
                          device.failedChecks
                        }{' '}
                        fallidas
                      </span>
                    </div>

                    <button
                      type="button"
                      className="compliance-evaluate"
                      disabled={
                        running !== null
                      }
                      onClick={() =>
                        void evaluate(
                          device.deviceId,
                        )
                      }
                    >
                      <ScanLine
                        size={15}
                      />
                      {running ===
                      device.deviceId
                        ? 'Enviando...'
                        : 'Evaluar'}
                    </button>
                  </div>

                  {isExpanded && (
                    <div className="compliance-findings">
                      {findings.length ===
                      0 ? (
                        <div className="compliance-no-findings">
                          No existen hallazgos
                          almacenados.
                        </div>
                      ) : (
                        findings.map(
                          (finding) => (
                            <div
                              key={
                                finding.code
                              }
                              className={
                                finding.compliant
                                  ? 'compliance-finding pass'
                                  : 'compliance-finding fail'
                              }
                            >
                              <div className="compliance-finding-icon">
                                {finding.compliant ? (
                                  <CheckCircle2
                                    size={17}
                                  />
                                ) : (
                                  <ShieldAlert
                                    size={17}
                                  />
                                )}
                              </div>

                              <div>
                                <div className="compliance-finding-title">
                                  <strong>
                                    {
                                      finding.title
                                    }
                                  </strong>

                                  <span>
                                    {
                                      finding.severity
                                    }
                                  </span>
                                </div>

                                <p>
                                  {
                                    finding.description
                                  }
                                </p>

                                <small>
                                  {
                                    finding.category
                                  }{' '}
                                  · {finding.code}
                                </small>
                              </div>
                            </div>
                          ),
                        )
                      )}
                    </div>
                  )}
                </article>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}

function Stat({
  icon,
  label,
  value,
  good = false,
  danger = false,
}: {
  icon: ReactNode
  label: string
  value: string | number
  good?: boolean
  danger?: boolean
}) {
  return (
    <article
      className={[
        'compliance-stat',
        good ? 'good' : '',
        danger ? 'danger' : '',
      ].join(' ')}
    >
      <div className="compliance-stat-icon">
        {icon}
      </div>

      <div>
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
    </article>
  )
}

function StatusBadge({
  status,
}: {
  status: string
}) {
  const compliant =
    status.toLowerCase() ===
    'compliant'

  return (
    <span
      className={
        compliant
          ? 'compliance-status compliant'
          : 'compliance-status noncompliant'
      }
    >
      {status}
    </span>
  )
}

function RiskBadge({
  risk,
}: {
  risk: string
}) {
  return (
    <span
      className={`compliance-risk ${risk.toLowerCase()}`}
    >
      {risk}
    </span>
  )
}