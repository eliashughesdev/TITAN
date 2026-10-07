import {
  Activity,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Download,
  RefreshCw,
  Search,
  ShieldAlert,
  UserRound,
  X,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import {
  auditApi,
} from '../../api/auditApi'

import type {
  AuditEvent,
  AuditFilterOptions,
  AuditSummary,
} from '../../types/audit'

import './AuditPage.css'

const emptySummary:
  AuditSummary = {
    totalEvents: 0,
    successfulEvents: 0,
    failedEvents: 0,
    activeEvents: 0,
    eventsLast24Hours: 0,
    uniqueActors: 0,
    uniqueDevices: 0,
  }

const emptyFilters:
  AuditFilterOptions = {
    modules: [],
    actions: [],
    statuses: [],
  }

function formatDate(
  value:
    string |
    null,
) {
  if (!value) {
    return 'N/D'
  }

  return new Date(
    value,
  ).toLocaleString()
}

function formatDuration(
  value:
    number |
    null,
) {
  if (
    value === null
    ||
    value < 0
  ) {
    return 'N/D'
  }

  if (value < 1000) {
    return `${value} ms`
  }

  return `${
    (
      value /
      1000
    ).toFixed(1)
  } s`
}

export function AuditPage() {
  const [
    summary,
    setSummary,
  ] =
    useState(
      emptySummary,
    )

  const [
    filters,
    setFilters,
  ] =
    useState(
      emptyFilters,
    )

  const [
    events,
    setEvents,
  ] =
    useState<
      AuditEvent[]
    >([])

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

  const [
    search,
    setSearch,
  ] =
    useState('')

  const [
    module,
    setModule,
  ] =
    useState('')

  const [
    action,
    setAction,
  ] =
    useState('')

  const [
    status,
    setStatus,
  ] =
    useState('')

  const [
    page,
    setPage,
  ] =
    useState(1)

  const [
    total,
    setTotal,
  ] =
    useState(0)

  const [
    totalPages,
    setTotalPages,
  ] =
    useState(0)

  const [
    selected,
    setSelected,
  ] =
    useState<
      AuditEvent | null
    >(null)

  const pageSize =
    50

  const load =
    useCallback(
      async () => {
        try {
          setLoading(true)

          setError(null)

          const result =
            await auditApi
              .getEvents({
                search,
                module,
                action,
                status,
                page,
                pageSize,
              })

          setEvents(
            result.items,
          )

          setTotal(
            result.totalCount,
          )

          setTotalPages(
            result.totalPages,
          )
        } catch {
          setError(
            'No fue posible cargar la auditoría.',
          )
        } finally {
          setLoading(false)
        }
      },
      [
        search,
        module,
        action,
        status,
        page,
      ],
    )

  useEffect(
    () => {
      const timer =
        window.setTimeout(
          () => {
            void load()
          },
          250,
        )

      return () =>
        window.clearTimeout(
          timer,
        )
    },
    [
      load,
    ],
  )

  useEffect(
    () => {
      void Promise.all([
        auditApi
          .getSummary()
          .then(
            setSummary,
          ),

        auditApi
          .getFilters()
          .then(
            setFilters,
          ),
      ])
    },
    [],
  )

  useEffect(
    () => {
      setPage(1)
    },
    [
      search,
      module,
      action,
      status,
    ],
  )

  return (
    <div className="audit-page">
      <header className="audit-header">
        <div>
          <span>
            TITANMDM AUDIT
          </span>

          <h1>
            Auditoría
          </h1>

          <p>
            Trazabilidad operacional de
            comandos y acciones ejecutadas
            sobre los endpoints.
          </p>
        </div>

        <div className="audit-header-actions">
          <button
            type="button"
            onClick={() =>
              void load()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          <button
            type="button"
            className="primary"
            onClick={() =>
              void auditApi
                .exportCsv({
                  search,
                  module,
                  action,
                  status,
                })
            }
          >
            <Download
              size={16}
            />

            Exportar CSV
          </button>
        </div>
      </header>

      {error && (
        <div className="audit-error">
          {error}
        </div>
      )}

      <section className="audit-stats">
        <Stat
          icon={
            <Activity
              size={19}
            />
          }
          label="Eventos"
          value={
            summary
              .totalEvents
          }
        />

        <Stat
          icon={
            <CheckCircle2
              size={19}
            />
          }
          label="Exitosos"
          value={
            summary
              .successfulEvents
          }
          good
        />

        <Stat
          icon={
            <ShieldAlert
              size={19}
            />
          }
          label="Fallidos"
          value={
            summary
              .failedEvents
          }
          danger
        />

        <Stat
          icon={
            <Activity
              size={19}
            />
          }
          label="Últimas 24h"
          value={
            summary
              .eventsLast24Hours
          }
        />

        <Stat
          icon={
            <UserRound
              size={19}
            />
          }
          label="Actores"
          value={
            summary
              .uniqueActors
          }
        />
      </section>

      <section className="audit-panel">
        <div className="audit-filters">
          <div className="audit-search">
            <Search
              size={16}
            />

            <input
              value={search}
              placeholder="Buscar usuario, correo, dispositivo o acción..."
              onChange={
                event =>
                  setSearch(
                    event
                      .target
                      .value,
                  )
              }
            />
          </div>

          <select
            value={module}
            onChange={
              event =>
                setModule(
                  event
                    .target
                    .value,
                )
            }
          >
            <option value="">
              Todos los módulos
            </option>

            {filters.modules.map(
              value => (
                <option
                  key={value}
                  value={value}
                >
                  {value}
                </option>
              ),
            )}
          </select>

          <select
            value={action}
            onChange={
              event =>
                setAction(
                  event
                    .target
                    .value,
                )
            }
          >
            <option value="">
              Todas las acciones
            </option>

            {filters.actions.map(
              value => (
                <option
                  key={value}
                  value={value}
                >
                  {value}
                </option>
              ),
            )}
          </select>

          <select
            value={status}
            onChange={
              event =>
                setStatus(
                  event
                    .target
                    .value,
                )
            }
          >
            <option value="">
              Todos los estados
            </option>

            {filters.statuses.map(
              value => (
                <option
                  key={value}
                  value={value}
                >
                  {value}
                </option>
              ),
            )}
          </select>
        </div>

        <div className="audit-table-wrap">
          <table className="audit-table">
            <thead>
              <tr>
                <th>
                  Fecha
                </th>

                <th>
                  Usuario
                </th>

                <th>
                  Módulo
                </th>

                <th>
                  Acción
                </th>

                <th>
                  Dispositivo
                </th>

                <th>
                  Estado
                </th>

                <th>
                  Duración
                </th>

                <th />
              </tr>
            </thead>

            <tbody>
              {!loading &&
              events.length ===
                0 && (
                <tr>
                  <td
                    colSpan={8}
                    className="audit-empty"
                  >
                    No hay eventos
                    para mostrar.
                  </td>
                </tr>
              )}

              {events.map(
                event => (
                  <tr
                    key={event.id}
                  >
                    <td>
                      {formatDate(
                        event
                          .createdAtUtc,
                      )}
                    </td>

                    <td>
                      <strong>
                        {event
                          .actorName}
                      </strong>

                      <small>
                        {event
                          .actorEmail}
                      </small>
                    </td>

                    <td>
                      <span className="audit-module">
                        {event.module}
                      </span>
                    </td>

                    <td>
                      <strong>
                        {event.action}
                      </strong>
                    </td>

                    <td>
                      <strong>
                        {event
                          .deviceName}
                      </strong>

                      <small>
                        {event.platform}
                      </small>
                    </td>

                    <td>
                      <span
                        className={
                          `audit-status ${event.status.toLowerCase()}`
                        }
                      >
                        {event.status}
                      </span>
                    </td>

                    <td>
                      {formatDuration(
                        event
                          .durationMilliseconds,
                      )}
                    </td>

                    <td>
                      <button
                        type="button"
                        className="audit-detail-button"
                        onClick={() =>
                          setSelected(
                            event,
                          )
                        }
                      >
                        Ver
                      </button>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>

        <footer className="audit-pagination">
          <span>
            {total}{' '}
            evento
            {total === 1
              ? ''
              : 's'}
          </span>

          <div>
            <button
              type="button"
              disabled={
                page <= 1
                ||
                loading
              }
              onClick={() =>
                setPage(
                  current =>
                    Math.max(
                      1,
                      current - 1,
                    ),
                )
              }
            >
              <ChevronLeft
                size={15}
              />
            </button>

            <span>
              Página {page} de{' '}
              {Math.max(
                totalPages,
                1,
              )}
            </span>

            <button
              type="button"
              disabled={
                page >=
                  totalPages
                ||
                loading
              }
              onClick={() =>
                setPage(
                  current =>
                    current + 1,
                )
              }
            >
              <ChevronRight
                size={15}
              />
            </button>
          </div>
        </footer>
      </section>

      {selected && (
        <div className="audit-modal-backdrop">
          <div className="audit-modal">
            <header>
              <div>
                <span>
                  AUDIT EVENT
                </span>

                <h3>
                  {selected.action}
                </h3>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelected(null)
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="audit-detail-grid">
              <Detail
                label="Usuario"
                value={
                  selected
                    .actorName
                }
              />

              <Detail
                label="Correo"
                value={
                  selected
                    .actorEmail
                }
              />

              <Detail
                label="Dispositivo"
                value={
                  selected
                    .deviceName
                }
              />

              <Detail
                label="Estado"
                value={
                  selected.status
                }
              />

              <Detail
                label="Duración"
                value={
                  formatDuration(
                    selected
                      .durationMilliseconds,
                  )
                }
              />

              <Detail
                label="Intentos"
                value={
                  String(
                    selected
                      .deliveryAttempts,
                  )
                }
              />
            </div>

            {selected.errorMessage && (
              <div className="audit-event-error">
                <strong>
                  Error
                </strong>

                <span>
                  {selected
                    .errorMessage}
                </span>
              </div>
            )}

            <details>
              <summary>
                Payload técnico
              </summary>

              <pre>
                {selected
                  .payloadJson
                ??
                '{}'}
              </pre>
            </details>

            <details>
              <summary>
                Resultado técnico
              </summary>

              <pre>
                {selected
                  .resultJson
                ??
                '{}'}
              </pre>
            </details>
          </div>
        </div>
      )}
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
  icon: React.ReactNode
  label: string
  value: number
  good?: boolean
  danger?: boolean
}) {
  return (
    <article
      className={[
        'audit-stat',
        good ? 'good' : '',
        danger ? 'danger' : '',
      ].join(' ')}
    >
      {icon}

      <div>
        <span>
          {label}
        </span>

        <strong>
          {value}
        </strong>
      </div>
    </article>
  )
}

function Detail({
  label,
  value,
}: {
  label: string
  value: string
}) {
  return (
    <div>
      <span>
        {label}
      </span>

      <strong>
        {value || 'N/D'}
      </strong>
    </div>
  )
}