import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import axios
  from 'axios'

import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Gauge,
  LoaderCircle,
  Play,
  RefreshCcw,
  Route,
  Search,
  ShieldCheck,
  Users,
  WandSparkles,
  XCircle,
} from 'lucide-react'

import {
  getRoutingDiagnostic,
  getRoutingHealth,
  retryOpenRouting,
  retryRoutingTicket,

  type RoutingDiagnostic,
  type RoutingHealth,
  type RoutingQueueResult,
} from '../../api/helpdeskRoutingApi'

import './HelpdeskRoutingOperationsPanel.css'

interface Props {
  readOnly?: boolean
}

function errorMessage(
  exception: unknown,
) {
  if (
    axios.isAxiosError<{
      message?: string
    }>(
      exception,
    )
  ) {
    return (
      exception.response
        ?.data
        ?.message
      ??
      `La operación falló (${exception.response?.status ?? 'sin conexión'}).`
    )
  }

  return exception instanceof Error
    ? exception.message
    : 'La operación no pudo completarse.'
}

function statusClass(
  value: string,
) {
  const normalized =
    value
      .trim()
      .toLowerCase()

  if (
    normalized ===
      'assigned'
    ||
    normalized ===
      'assignable'
  ) {
    return 'is-success'
  }

  if (
    normalized ===
      'failed'
    ||
    normalized ===
      'blocked'
  ) {
    return 'is-danger'
  }

  return 'is-warning'
}

export function HelpdeskRoutingOperationsPanel({
  readOnly = false,
}: Props) {
  const [
    health,
    setHealth,
  ] =
    useState<RoutingHealth | null>(
      null,
    )

  const [
    diagnostic,
    setDiagnostic,
  ] =
    useState<RoutingDiagnostic | null>(
      null,
    )

  const [
    queue,
    setQueue,
  ] =
    useState<RoutingQueueResult | null>(
      null,
    )

  const [
    ticketId,
    setTicketId,
  ] =
    useState(
      '',
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    action,
    setAction,
  ] =
    useState(
      '',
    )

  const [
    error,
    setError,
  ] =
    useState(
      '',
    )

  const loadHealth =
    useCallback(
      async () => {
        try {
          setError(
            '',
          )

          const value =
            await getRoutingHealth()

          setHealth(
            value,
          )
        }
        catch (
          exception
        ) {
          setError(
            errorMessage(
              exception,
            ),
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [],
    )

  useEffect(
    () => {
      void loadHealth()
    },
    [
      loadHealth,
    ],
  )

  async function inspect() {
    const normalized =
      ticketId
        .trim()

    if (!normalized) {
      setError(
        'Introduce el ID interno del ticket.',
      )

      return
    }

    try {
      setAction(
        'diagnostic',
      )

      setError(
        '',
      )

      const value =
        await getRoutingDiagnostic(
          normalized,
        )

      setDiagnostic(
        value,
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )
    }
    finally {
      setAction(
        '',
      )
    }
  }

  async function retryTicket() {
    if (
      !diagnostic
    ) {
      return
    }

    try {
      setAction(
        'retry-ticket',
      )

      setError(
        '',
      )

      const result =
        await retryRoutingTicket(
          diagnostic.ticketId,
        )

      setDiagnostic(
        result.after
        ??
        result.before,
      )

      await loadHealth()
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )
    }
    finally {
      setAction(
        '',
      )
    }
  }

  async function runQueue(
    dryRun: boolean,
  ) {
    try {
      setAction(
        dryRun
          ? 'dry-run'
          : 'live-run',
      )

      setError(
        '',
      )

      const result =
        await retryOpenRouting(
          50,
          dryRun,
        )

      setQueue(
        result,
      )

      await loadHealth()
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )
    }
    finally {
      setAction(
        '',
      )
    }
  }

  return (
    <section
      className="hd-routing"
    >
      <header
        className="hd-routing__header"
      >
        <div>
          <span
            className="hd-routing__eyebrow"
          >
            <Route
              size={15}
            />

            ROUTING ENTERPRISE
          </span>

          <h2>
            Motor de autoasignación
          </h2>

          <p>
            Estado, capacidad,
            diagnóstico y operación
            del motor de distribución
            automática de tickets.
          </p>
        </div>

        <button
          type="button"
          className="hd-routing__secondary"
          disabled={
            loading
          }
          onClick={
            () =>
              void loadHealth()
          }
        >
          <RefreshCcw
            size={16}
          />

          Actualizar
        </button>
      </header>

      {error && (
        <div
          className="hd-routing__alert"
        >
          <AlertTriangle
            size={17}
          />

          {error}
        </div>
      )}

      {loading ? (
        <div
          className="hd-routing__loading"
        >
          <LoaderCircle
            className="hd-routing__spin"
            size={24}
          />

          Consultando motor de routing...
        </div>
      ) : (
        <>
          <div
            className="hd-routing__metrics"
          >
            <Metric
              icon={
                Gauge
              }
              label="Tickets abiertos"
              value={
                health
                  ?.openTickets
                ??
                0
              }
            />

            <Metric
              icon={
                AlertTriangle
              }
              label="Sin asignar"
              value={
                health
                  ?.unassignedTickets
                ??
                0
              }
              warning={
                Boolean(
                  health
                    ?.unassignedTickets,
                )
              }
            />

            <Metric
              icon={
                Clock3
              }
              label="Esperando usuario"
              value={
                health
                  ?.pendingUserTickets
                ??
                0
              }
            />

            <Metric
              icon={
                WandSparkles
              }
              label="Autoasignados 24 h"
              value={
                health
                  ?.autoAssignedLast24Hours
                ??
                0
              }
            />

            <Metric
              icon={
                Users
              }
              label="Técnicos elegibles"
              value={
                health
                  ?.activeMembers
                ??
                0
              }
            />

            <Metric
              icon={
                Activity
              }
              label="En turno ahora"
              value={
                health
                  ?.membersOnDutyNow
                ??
                0
              }
            />

            <Metric
              icon={
                Route
              }
              label="Esperas routing 24 h"
              value={
                health
                  ?.routingWaitingLast24Hours
                ??
                0
              }
              warning={
                Boolean(
                  health
                    ?.routingWaitingLast24Hours,
                )
              }
            />

            <Metric
              icon={
                ShieldCheck
              }
              label="Tiempo medio"
              value={
                `${
                  health
                    ?.averageAutoAssignMinutesLast24Hours
                  ??
                  0
                } min`
              }
            />
          </div>

          {!readOnly && (
            <>
              <div
                className="hd-routing__operations"
              >
                <div
                  className="hd-routing__operation"
                >
                  <div>
                    <h3>
                      Cola pendiente
                    </h3>

                    <p>
                      Evalúa o reintenta
                      tickets abiertos que
                      continúan sin técnico.
                    </p>
                  </div>

                  <div
                    className="hd-routing__actions"
                  >
                    <button
                      type="button"
                      className="hd-routing__secondary"
                      disabled={
                        Boolean(
                          action,
                        )
                      }
                      onClick={
                        () =>
                          void runQueue(
                            true,
                          )
                      }
                    >
                      <Search
                        size={16}
                      />

                      {
                        action ===
                        'dry-run'
                          ? 'Evaluando...'
                          : 'Simular cola'
                      }
                    </button>

                    <button
                      type="button"
                      className="hd-routing__primary"
                      disabled={
                        Boolean(
                          action,
                        )
                      }
                      onClick={
                        () =>
                          void runQueue(
                            false,
                          )
                      }
                    >
                      <Play
                        size={16}
                      />

                      {
                        action ===
                        'live-run'
                          ? 'Procesando...'
                          : 'Procesar cola'
                      }
                    </button>
                  </div>
                </div>

                <div
                  className="hd-routing__operation"
                >
                  <div>
                    <h3>
                      Diagnóstico individual
                    </h3>

                    <p>
                      Explica por qué un
                      ticket puede o no
                      asignarse.
                    </p>
                  </div>

                  <div
                    className="hd-routing__ticket-search"
                  >
                    <input
                      value={
                        ticketId
                      }
                      onChange={
                        event =>
                          setTicketId(
                            event.target
                              .value,
                          )
                      }
                      placeholder="GUID interno del ticket"
                    />

                    <button
                      type="button"
                      className="hd-routing__secondary"
                      disabled={
                        Boolean(
                          action,
                        )
                      }
                      onClick={
                        () =>
                          void inspect()
                      }
                    >
                      <Search
                        size={16}
                      />

                      Diagnosticar
                    </button>
                  </div>
                </div>
              </div>

              {diagnostic && (
                <DiagnosticPanel
                  value={
                    diagnostic
                  }
                  retrying={
                    action ===
                    'retry-ticket'
                  }
                  onRetry={
                    () =>
                      void retryTicket()
                  }
                />
              )}

              {queue && (
                <QueuePanel
                  value={
                    queue
                  }
                />
              )}
            </>
          )}
        </>
      )}
    </section>
  )
}

interface MetricProps {
  icon:
    typeof Gauge

  label:
    string

  value:
    string | number

  warning?:
    boolean
}

function Metric({
  icon:
    Icon,

  label,
  value,
  warning = false,
}: MetricProps) {
  return (
    <article
      className={
        'hd-routing__metric ' +
        (
          warning
            ? 'is-warning'
            : ''
        )
      }
    >
      <span>
        <Icon
          size={17}
        />
      </span>

      <div>
        <small>
          {label}
        </small>

        <strong>
          {value}
        </strong>
      </div>
    </article>
  )
}

function DiagnosticPanel({
  value,
  retrying,
  onRetry,
}: {
  value:
    RoutingDiagnostic

  retrying:
    boolean

  onRetry:
    () => void
}) {
  return (
    <section
      className="hd-routing__diagnostic"
    >
      <header>
        <div>
          <span>
            DIAGNÓSTICO
          </span>

          <h3>
            {
              value.ticketNumber
            }
            {' · '}
            {
              value.subject
              ??
              'Sin asunto'
            }
          </h3>
        </div>

        {!value.alreadyAssigned &&
          !value.pendingUser && (
          <button
            type="button"
            className="hd-routing__primary"
            disabled={
              retrying
            }
            onClick={
              onRetry
            }
          >
            <RefreshCcw
              size={16}
            />

            {
              retrying
                ? 'Reintentando...'
                : 'Reintentar asignación'
            }
          </button>
        )}
      </header>

      <div
        className="hd-routing__diagnostic-summary"
      >
        <span>
          Estado
          <strong>
            {
              value.status
            }
          </strong>
        </span>

        <span>
          Categoría
          <strong>
            {
              value.category
            }
          </strong>
        </span>

        <span>
          Asignado
          <strong>
            {
              value.alreadyAssigned
                ? 'Sí'
                : 'No'
            }
          </strong>
        </span>

        <span>
          Site
          <strong>
            {
              value.siteId
              ??
              'Sin Site'
            }
          </strong>
        </span>
      </div>

      <div
        className="hd-routing__reason"
      >
        <Route
          size={17}
        />

        {
          value.engineReason
        }
      </div>

      <div
        className="hd-routing__candidate-list"
      >
        {value.candidates.length ===
        0 ? (
          <div
            className="hd-routing__empty"
          >
            No se encontraron candidatos.
          </div>
        ) : (
          value.candidates.map(
            candidate => (
              <article
                key={
                  candidate
                    .teamId +
                  candidate
                    .userId
                }
                className="hd-routing__candidate"
              >
                <div>
                  <strong>
                    {
                      candidate
                        .technicianName
                    }
                  </strong>

                  <small>
                    {
                      candidate
                        .teamName
                    }
                  </small>
                </div>

                <div>
                  <span>
                    Carga
                  </span>

                  <strong>
                    {
                      candidate
                        .openTickets
                    }
                    /
                    {
                      candidate
                        .capacity
                    }
                  </strong>
                </div>

                <div>
                  <span>
                    Turno
                  </span>

                  <strong>
                    {
                      candidate
                        .onDutyNow
                        ? 'Activo'
                        : 'Fuera'
                    }
                  </strong>
                </div>

                <div>
                  <span
                    className={
                      'hd-routing__status ' +
                      statusClass(
                        candidate
                          .decision,
                      )
                    }
                  >
                    {
                      candidate
                        .decision ===
                      'assignable'
                        ? (
                          <CheckCircle2
                            size={14}
                          />
                        )
                        : (
                          <XCircle
                            size={14}
                          />
                        )
                    }

                    {
                      candidate
                        .decision
                    }
                  </span>

                  <small>
                    {
                      candidate
                        .reason
                    }
                  </small>
                </div>
              </article>
            ),
          )
        )}
      </div>
    </section>
  )
}

function QueuePanel({
  value,
}: {
  value:
    RoutingQueueResult
}) {
  return (
    <section
      className="hd-routing__queue"
    >
      <header>
        <div>
          <span>
            {
              value.dryRun
                ? 'SIMULACIÓN'
                : 'EJECUCIÓN'
            }
          </span>

          <h3>
            Resultado de la cola
          </h3>
        </div>

        <div
          className="hd-routing__queue-summary"
        >
          <span>
            Evaluados
            <strong>
              {
                value.considered
              }
            </strong>
          </span>

          <span>
            Asignados
            <strong>
              {
                value.assigned
              }
            </strong>
          </span>

          <span>
            Omitidos
            <strong>
              {
                value.skipped
              }
            </strong>
          </span>

          <span>
            Fallidos
            <strong>
              {
                value.failed
              }
            </strong>
          </span>
        </div>
      </header>

      <div
        className="hd-routing__queue-items"
      >
        {value.items.map(
          item => (
            <article
              key={
                item.ticketId
              }
            >
              <div>
                <strong>
                  {
                    item.ticketNumber
                  }
                </strong>

                <small>
                  {
                    item.category
                  }
                  {' · '}
                  {
                    item.status
                  }
                </small>
              </div>

              <span
                className={
                  'hd-routing__status ' +
                  statusClass(
                    item.outcome,
                  )
                }
              >
                {
                  item.outcome
                }
              </span>

              <p>
                {
                  item.reason
                }
              </p>
            </article>
          ),
        )}
      </div>
    </section>
  )
}