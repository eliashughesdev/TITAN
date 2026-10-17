import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Gauge,
  Inbox,
  RefreshCw,
  XCircle,
  Zap,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useState,
  type ReactNode,
} from 'react'

import {
  automationApi,
  type AutomationDashboard,
  type AutomationQueueStats,
} from '../../api/automationApi'

import {
  describeAutomationError,
  formatDate,
  formatDuration,
} from './automationUi'

export function AutomationDashboardPanel() {
  const [dashboard, setDashboard] =
    useState<AutomationDashboard | null>(null)

  const [queue, setQueue] =
    useState<AutomationQueueStats | null>(null)

  const [loading, setLoading] =
    useState(true)

  const [error, setError] =
    useState<string | null>(null)

  const fetchData = useCallback(
    () =>
      Promise.all([
        automationApi.getDashboard(),
        automationApi.getQueueStats(),
      ]),
    [],
  )

  const applyData = useCallback(
    ([dashboardData, queueData]: [
      AutomationDashboard,
      AutomationQueueStats,
    ]) => {
      setDashboard(dashboardData)
      setQueue(queueData)
      setError(null)
    },
    [],
  )

  const applyError = useCallback(
    (loadError: unknown) => {
      console.error(
        'Automation dashboard error:',
        loadError,
      )

      setError(
        describeAutomationError(
          loadError,
          'No fue posible cargar el panel del motor.',
        ),
      )
    },
    [],
  )

  useEffect(() => {
    let active = true

    fetchData()
      .then((data) => {
        if (active) {
          applyData(data)
        }
      })
      .catch((loadError) => {
        if (active) {
          applyError(loadError)
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [fetchData, applyData, applyError])

  const refresh = useCallback(
    () =>
      fetchData()
        .then((data) => {
          applyData(data)
        })
        .catch((loadError) => {
          applyError(loadError)
        })
        .finally(() => {
          setLoading(false)
        }),
    [fetchData, applyData, applyError],
  )

  return (
    <section className="automation-dashboard">
      <header className="automation-panel-header">
        <div>
          <Gauge size={18} />
          <div>
            <strong>Panel del motor</strong>
            <span>
              Salud operativa, cola y alertas
              recientes.
            </span>
          </div>
        </div>

        <button
          type="button"
          className="automation-secondary"
          disabled={loading}
          onClick={() => {
            setLoading(true)
            void refresh()
          }}
        >
          <RefreshCw size={15} />
          Actualizar
        </button>
      </header>

      {error && (
        <div className="automation-notice error">
          <XCircle size={16} />
          {error}
        </div>
      )}

      {loading && !dashboard ? (
        <div className="automation-panel-loading">
          Cargando panel del motor…
        </div>
      ) : dashboard ? (
        <>
          <div className="automation-kpi-grid">
            <Kpi
              icon={<Zap size={18} />}
              label="Reglas activas"
              value={`${dashboard.activeRules}/${dashboard.totalRules}`}
            />

            <Kpi
              icon={<Activity size={18} />}
              label="Ejecuciones 24 h"
              value={dashboard.executionsLast24Hours}
            />

            <Kpi
              icon={<XCircle size={18} />}
              label="Fallos 24 h"
              value={dashboard.failuresLast24Hours}
            />

            <Kpi
              icon={<CheckCircle2 size={18} />}
              label="Tasa de éxito"
              value={`${dashboard.successRate.toFixed(1)}%`}
            />

            <Kpi
              icon={<Clock3 size={18} />}
              label="Duración media"
              value={formatDuration(
                dashboard.averageDurationMs,
              )}
            />
          </div>

          <div className="automation-dashboard-columns">
            <div className="automation-dashboard-block">
              <div className="automation-block-title">
                <Inbox size={16} />
                <strong>Cola de ejecución</strong>
              </div>

              <div className="automation-queue-grid">
                <QueueCell
                  label="Pendientes"
                  value={queue?.pending ?? dashboard.queuePending}
                />
                <QueueCell
                  label="Reintentos"
                  value={queue?.retried ?? 0}
                />
                <QueueCell
                  label="En curso"
                  value={queue?.running ?? 0}
                />
                <QueueCell
                  label="Dead letter"
                  value={
                    queue?.deadLetter ??
                    dashboard.queueDeadLetter
                  }
                  variant="danger"
                />
              </div>
            </div>

            <div className="automation-dashboard-block">
              <div className="automation-block-title">
                <AlertTriangle size={16} />
                <strong>Alertas recientes</strong>
              </div>

              {dashboard.alerts.length === 0 ? (
                <div className="automation-inline-empty">
                  No existen alertas recientes.
                </div>
              ) : (
                <ul className="automation-alert-list">
                  {dashboard.alerts.map(
                    (alert, index) => (
                      <li
                        key={`${alert.message}-${index}`}
                        className={
                          'automation-alert ' +
                          alert.severity.toLowerCase()
                        }
                      >
                        <span>{alert.severity}</span>
                        <p>{alert.message}</p>
                        <small>
                          {formatDate(alert.createdAtUtc)}
                        </small>
                      </li>
                    ),
                  )}
                </ul>
              )}
            </div>
          </div>

          <div className="automation-history">
            <header>
              <Activity size={16} />
              <div>
                <strong>Ejecuciones recientes</strong>
                <span>
                  Últimas operaciones procesadas
                  por el motor empresarial.
                </span>
              </div>
            </header>

            <div className="automation-table-wrapper">
              <table className="automation-table">
                <thead>
                  <tr>
                    <th>AUTOMATIZACIÓN</th>
                    <th>DISPOSITIVO</th>
                    <th>EVENTO</th>
                    <th>ESTADO</th>
                    <th>INICIO</th>
                    <th>RESULTADO</th>
                  </tr>
                </thead>

                <tbody>
                  {dashboard.recentExecutions
                    .length === 0 ? (
                    <tr>
                      <td
                        colSpan={6}
                        className="automation-table-empty"
                      >
                        No existen ejecuciones.
                      </td>
                    </tr>
                  ) : (
                    dashboard.recentExecutions.map(
                      (execution) => (
                        <tr key={execution.id}>
                          <td>
                            <strong>
                              {execution.automationName}
                            </strong>
                          </td>
                          <td>
                            {execution.deviceName ??
                              'Global'}
                          </td>
                          <td>
                            {execution.triggerType}
                          </td>
                          <td>
                            {execution.status}
                          </td>
                          <td>
                            {formatDate(
                              execution.startedAtUtc,
                            )}
                          </td>
                          <td>
                            <span className="automation-result">
                              {execution.errorMessage ??
                                execution.resultJson ??
                                '—'}
                            </span>
                          </td>
                        </tr>
                      ),
                    )
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      ) : null}
    </section>
  )
}

function Kpi({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: string | number
}) {
  return (
    <article className="automation-kpi">
      <div>{icon}</div>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  )
}

function QueueCell({
  label,
  value,
  variant,
}: {
  label: string
  value: number
  variant?: 'danger'
}) {
  return (
    <div
      className={
        variant === 'danger'
          ? 'automation-queue-cell danger'
          : 'automation-queue-cell'
      }
    >
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
