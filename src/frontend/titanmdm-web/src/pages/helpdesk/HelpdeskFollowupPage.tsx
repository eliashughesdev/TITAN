import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import axios from 'axios'
import apiClient from '../../api/apiClient'
import { useAuth } from '../../auth/AuthContext'
import './HelpdeskFollowupPage.css'

type Ticket = {
  id: string
  number: string
  subject: string
  status: string
  priority: string
  category: string
  assigneeUserId: string | null
  firstResponseDueAtUtc: string | null
  firstRespondedAtUtc: string | null
  resolveDueAtUtc: string | null
  resolvedAtUtc: string | null
}

type Alert = {
  id: string
  ticketId: string
  number: string
  subject: string
  eventType: string
  summary: string
  createdAtUtc: string
}

type Followup = {
  scope: string
  canViewAll: boolean
  generatedAtUtc: string
  attentionTotal: number

  summary: {
    active: number
    unassigned: number
    waitingUser: number
    firstOverdue: number
    resolutionOverdue: number
    approaching: number
  }

  tickets: Ticket[]
  alerts: Alert[]
}

const states: Record<string, string> = {
  new: 'Nuevo',
  open: 'Abierto',
  inprogress: 'En proceso',
  pendinguser: 'En espera del usuario',
}

function date(value: string | null) {
  if (!value) return 'Sin plazo'

  const normalized =
    value.endsWith('Z') || /[+-]\d\d:\d\d$/.test(value)
      ? value
      : value + 'Z'

  return new Date(normalized).toLocaleString('es-DO')
}

function due(
  value: string | null,
  done: string | null,
  now: number,
) {
  if (done || !value) return false

  const normalized =
    value.endsWith('Z') || /[+-]\d\d:\d\d$/.test(value)
      ? value
      : value + 'Z'

  return Date.parse(normalized) <= now
}

export function HelpdeskFollowupPage() {
  const { hasPermission } = useAuth()

  const manager =
    hasPermission('helpdesk.manage') ||
    hasPermission('settings.manage')

  const [all, setAll] = useState(false)
  const [data, setData] = useState<Followup | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true)

      try {
        const response = await apiClient.get<Followup>(
          '/helpdesk/follow-up',
          {
            params: { all },
            signal,
          },
        )

        if (!signal?.aborted) {
          setData(response.data)
          setError('')
        }
      } catch (error) {
        if (signal?.aborted || axios.isCancel(error)) return

        setError(
          axios.isAxiosError(error) &&
            error.response?.status === 403
            ? 'No tienes permiso para consultar este seguimiento.'
            : 'No se pudo actualizar el seguimiento. ' +
              'Los datos anteriores pueden estar desactualizados.',
        )
      } finally {
        if (!signal?.aborted) setLoading(false)
      }
    },
    [all],
  )

  useEffect(() => {
    setData(null)

    const controller = new AbortController()

    void load(controller.signal)

    const timer = window.setInterval(() => {
      if (!document.hidden) {
        void load(controller.signal)
      }
    }, 60000)

    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [load])

  const now = Date.now()

  const metrics = data
    ? [
        ['Tickets activos', data.summary.active],
        ['Próximos a vencer', data.summary.approaching],
        ['Primera respuesta vencida', data.summary.firstOverdue],
        ['Resolución vencida', data.summary.resolutionOverdue],
        ['Sin técnico', data.summary.unassigned],
        ['En espera del usuario', data.summary.waitingUser],
      ] as const
    : []

  return (
    <main className="hdf">
      <header className="hdf-header">
        <div>
          <h1>Seguimiento de SLA</h1>
          <p>
            Casos que requieren atención y recordatorios registrados
            por el monitor automático.
          </p>
        </div>

        <div className="hdf-controls">
          <select
            aria-label="Alcance del seguimiento"
            value={all ? 'all' : 'mine'}
            disabled={loading}
            onChange={event =>
              setAll(event.target.value === 'all')
            }
          >
            <option value="mine">Mis tickets</option>

            {manager && (
              <option value="all">Toda la mesa de ayuda</option>
            )}
          </select>

          <button
            disabled={loading}
            onClick={() => void load()}
          >
            {loading ? 'Actualizando…' : 'Actualizar'}
          </button>
        </div>
      </header>

      {error && (
        <div className="hdf-error" role="alert">
          {error}
        </div>
      )}

      {!data && !error && <p>Cargando seguimiento…</p>}

      {data && (
        <>
          <div className="hdf-metrics">
            {metrics.map(([label, count]) => (
              <article key={label}>
                <span>{label}</span>
                <strong>{count}</strong>
              </article>
            ))}
          </div>

          <p className="hdf-note">
            Actualizado: {date(data.generatedAtUtc)}. La pantalla
            consulta cada minuto mientras está visible. Los avisos
            automáticos no cuentan como una primera respuesta técnica.
          </p>

          <section className="hdf-panel">
            <h2>
              Requieren atención ({data.attentionTotal})
            </h2>

            <p>
              Se muestran hasta 100 casos. Las fechas se presentan en
              la hora local de tu equipo.
            </p>

            <div className="hdf-table">
              <table>
                <thead>
                  <tr>
                    <th>Ticket</th>
                    <th>Estado</th>
                    <th>Primera respuesta</th>
                    <th>Resolución</th>
                    <th>Atención</th>
                  </tr>
                </thead>

                <tbody>
                  {data.tickets.map(ticket => (
                    <tr key={ticket.id}>
                      <td>
                        <Link
                          to={
                            `/helpdesk/tickets/${ticket.id}` +
                            '?workspace=helpdesk'
                          }
                        >
                          {ticket.number}
                        </Link>
                        <strong>{ticket.subject}</strong>
                        <small>
                          {ticket.category} · {ticket.priority}
                        </small>
                      </td>

                      <td>
                        {states[ticket.status] ?? ticket.status}
                      </td>

                      <td
                        className={
                          due(
                            ticket.firstResponseDueAtUtc,
                            ticket.firstRespondedAtUtc,
                            now,
                          )
                            ? 'hdf-overdue'
                            : ''
                        }
                      >
                        {ticket.firstRespondedAtUtc
                          ? 'Respondido'
                          : date(ticket.firstResponseDueAtUtc)}
                      </td>

                      <td
                        className={
                          due(
                            ticket.resolveDueAtUtc,
                            ticket.resolvedAtUtc,
                            now,
                          )
                            ? 'hdf-overdue'
                            : ''
                        }
                      >
                        {date(ticket.resolveDueAtUtc)}
                      </td>

                      <td>
                        {!ticket.assigneeUserId
                          ? 'Sin responsable'
                          : due(
                                ticket.firstResponseDueAtUtc,
                                ticket.firstRespondedAtUtc,
                                now,
                              ) ||
                              due(
                                ticket.resolveDueAtUtc,
                                ticket.resolvedAtUtc,
                                now,
                              )
                            ? 'Plazo vencido'
                            : 'Próximo vencimiento'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {!data.tickets.length && (
              <p>
                No hay casos que requieran atención en este alcance.
              </p>
            )}
          </section>

          <section className="hdf-panel">
            <h2>
              Recordatorios registrados · últimas 24 horas
            </h2>

            <p>
              Estos son avisos internos. No representan correos
              enviados.
            </p>

            <div className="hdf-alerts">
              {data.alerts.map(alert => (
                <article key={alert.id}>
                  <div>
                    <Link
                      to={
                        `/helpdesk/tickets/${alert.ticketId}` +
                        '?workspace=helpdesk'
                      }
                    >
                      {alert.number}
                    </Link>
                    <time>{date(alert.createdAtUtc)}</time>
                  </div>
                  <p>{alert.summary}</p>
                </article>
              ))}
            </div>

            {!data.alerts.length && (
              <p>
                No hay recordatorios registrados en este alcance.
              </p>
            )}
          </section>
        </>
      )}
    </main>
  )
}