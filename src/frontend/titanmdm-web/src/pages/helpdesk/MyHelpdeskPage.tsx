import {
  useCallback,
  useEffect,
  useState,
  type FormEvent,
} from 'react'

import {
  useNavigate,
  useParams,
} from 'react-router-dom'

import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Clock3,
  Headphones,
  MessageSquare,
  PauseCircle,
  Plus,
  RotateCcw,
  Send,
  Ticket,
  X,
} from 'lucide-react'

import axios
  from 'axios'

import apiClient
  from '../../api/apiClient'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
} from '../../auth/helpdeskAccess'

import './HelpdeskPages.css'
import './MyHelpdeskWorkflow.css'

interface MyTicket {
  id: string
  number: string
  subject: string
  description: string
  status: string
  priority: string
  category: string
  createdAtUtc: string
  updatedAtUtc: string
}

interface MyTicketDetails
  extends MyTicket {
  firstResponseDueAtUtc?:
    string | null

  resolveDueAtUtc?:
    string | null

  resolvedAtUtc?:
    string | null

  canReopen: boolean

  reopenWindowDays: number

  comments: {
    id: string
    authorUserId: string
    authorName: string
    body: string
    createdAtUtc: string
  }[]

  activity: {
    id: string
    eventType: string
    summary: string
    createdAtUtc: string
  }[]
}

const statusNames:
  Record<string, string> = {
    new:
      'Recibido',

    open:
      'En atención',

    inprogress:
      'En proceso',

    pendinguser:
      'Esperando tu respuesta',

    resolved:
      'Resuelto',

    closed:
      'Cerrado',
  }

function formatDate(
  value?: string | null,
) {
  if (
    !value
  ) {
    return '—'
  }

  const parsed =
    new Date(
      /(?:Z|[+-]\d{2}:?\d{2})$/i
        .test(value)
        ? value
        : value + 'Z',
    )

  return Number.isNaN(
    parsed.getTime(),
  )
    ? '—'
    : new Intl
        .DateTimeFormat(
          'es-DO',
          {
            dateStyle:
              'medium',

            timeStyle:
              'short',
          },
        )
        .format(
          parsed,
        )
}

function errorMessage(
  error: unknown,
  fallback: string,
) {
  return (
    axios.isAxiosError(
      error,
    )
    &&
    typeof error
      .response
      ?.data
      ?.message ===
      'string'
  )
    ? error
        .response
        ?.data
        ?.message
    : fallback
}

export function MyHelpdeskPage() {
  const {
    ticketId,
  } =
    useParams()

  const navigate =
    useNavigate()

  const {
    hasPermission,
  } =
    useAuth()

  const [
    tickets,
    setTickets,
  ] =
    useState<MyTicket[]>(
      [],
    )

  const [
    ticket,
    setTicket,
  ] =
    useState<
      MyTicketDetails |
      null
    >(
      null,
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    saving,
    setSaving,
  ] =
    useState(
      false,
    )

  const [
    error,
    setError,
  ] =
    useState(
      '',
    )

  const [
    notice,
    setNotice,
  ] =
    useState(
      '',
    )

  const [
    reply,
    setReply,
  ] =
    useState(
      '',
    )

  const [
    showReopen,
    setShowReopen,
  ] =
    useState(
      false,
    )

  const [
    reopenReason,
    setReopenReason,
  ] =
    useState(
      '',
    )

  const canCreate =
    hasPermission(
      helpdeskPermissions
        .requestCreate,
    )
    ||
    hasPermission(
      'tickets.create',
    )

  const canReply =
    hasPermission(
      helpdeskPermissions
        .requestOwnComment,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .portalAccess,
    )

  const canRequestReopen =
    hasPermission(
      helpdeskPermissions
        .requestOwnReopen,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .portalAccess,
    )

  const load =
    useCallback(
      async (
        signal:
          AbortSignal,
      ) => {
        setLoading(
          true,
        )

        setError(
          '',
        )

        setNotice(
          '',
        )

        setTicket(
          null,
        )

        try {
          if (
            ticketId
          ) {
            /*
             * Usuario común:
             * siempre utiliza su endpoint
             * personal.
             */
            const result =
              await apiClient
                .get<MyTicketDetails>(
                  `/my/helpdesk/tickets/${ticketId}`,
                  {
                    signal,
                  },
                )

            if (
              !signal.aborted
            ) {
              setTicket(
                result.data,
              )
            }
          }
          else {
            const result =
              await apiClient
                .get<MyTicket[]>(
                  '/my/helpdesk/tickets',
                  {
                    signal,
                  },
                )

            if (
              !signal.aborted
            ) {
              setTickets(
                result.data,
              )
            }
          }
        }
        catch (
          exception
        ) {
          if (
            !signal.aborted
          ) {
            setError(
              errorMessage(
                exception,
                ticketId
                  ? 'No pudimos cargar esta solicitud.'
                  : 'No pudimos cargar tus solicitudes.',
              ),
            )
          }
        }
        finally {
          if (
            !signal.aborted
          ) {
            setLoading(
              false,
            )
          }
        }
      },
      [
        ticketId,
      ],
    )

  useEffect(
    () => {
      const controller =
        new AbortController()

      setReply(
        '',
      )

      setReopenReason(
        '',
      )

      setShowReopen(
        false,
      )

      void load(
        controller.signal,
      )

      return () =>
        controller.abort()
    },
    [
      load,
    ],
  )

  async function sendReply(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !ticketId
      ||
      !reply.trim()
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setNotice(
      '',
    )

    try {
      const result =
        await apiClient
          .post<MyTicketDetails>(
            `/my/helpdesk/tickets/${ticketId}/reply`,
            {
              body:
                reply.trim(),
            },
          )

      setTicket(
        result.data,
      )

      setReply(
        '',
      )

      setNotice(
        ticket?.status ===
          'pendinguser'
          ? 'Tu respuesta fue enviada. La solicitud volvió a estar en proceso.'
          : 'Tu respuesta fue enviada correctamente.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No pudimos publicar tu respuesta.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  async function reopenTicket(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !ticketId
      ||
      reopenReason
        .trim()
        .length < 5
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setNotice(
      '',
    )

    try {
      const result =
        await apiClient
          .post<MyTicketDetails>(
            `/my/helpdesk/tickets/${ticketId}/reopen`,
            {
              reason:
                reopenReason.trim(),
            },
          )

      setTicket(
        result.data,
      )

      setShowReopen(
        false,
      )

      setReopenReason(
        '',
      )

      setNotice(
        'La solicitud fue reabierta correctamente y volvió a la bandeja del equipo TIC.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No pudimos reabrir la solicitud.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  const badge =
    (
      value: string,
    ) => (
      <span
        className={
          `helpdesk-inbox__badge ` +
          `helpdesk-inbox__badge--${value}`
        }
      >
        {
          statusNames[
            value
          ]
          ??
          value
        }
      </span>
    )

  /*
   * ============================================================
   * DETAIL
   * ============================================================
   */

  if (
    ticketId
  ) {
    const normalizedStatus =
      ticket?.status
        ?.trim()
        .toLowerCase()
      ??
      ''

    const terminal =
      normalizedStatus ===
        'resolved'
      ||
      normalizedStatus ===
        'closed'

    const waitingUser =
      normalizedStatus ===
        'pendinguser'

    const reopenExpired =
      terminal
      &&
      ticket
      &&
      !ticket.canReopen

    return (
      <main
        className={
          'titan-page ' +
          'helpdesk-page ' +
          'my-helpdesk'
        }
      >
        <button
          type="button"
          className="my-helpdesk__back"
          onClick={
            () =>
              navigate(
                '/my-support?workspace=helpdesk',
              )
          }
        >
          <ArrowLeft
            size={16}
          />

          Mis solicitudes
        </button>

        {error && (
          <div
            className="helpdesk-inbox__error"
            role="alert"
          >
            <AlertTriangle
              size={17}
            />

            {error}
          </div>
        )}

        {notice && (
          <div
            className="my-helpdesk-workflow__success"
            role="status"
          >
            <CheckCircle2
              size={18}
            />

            {notice}
          </div>
        )}

        {loading && (
          <section
            className="my-helpdesk__card"
          >
            Cargando solicitud…
          </section>
        )}

        {!loading &&
          !ticket &&
          !error && (
          <section
            className="my-helpdesk__card"
          >
            La solicitud no
            está disponible.
          </section>
        )}

        {!loading &&
          ticket && (
          <>
            <header
              className="my-helpdesk__hero"
            >
              <span
                className="helpdesk-inbox__eyebrow"
              >
                <Headphones
                  size={15}
                />

                Solicitud{' '}
                {
                  ticket.number
                }
              </span>

              <h1>
                {
                  ticket.subject
                }
              </h1>

              <div
                className="my-helpdesk__meta"
              >
                {badge(
                  normalizedStatus,
                )}

                <span>
                  Creada{' '}
                  {formatDate(
                    ticket.createdAtUtc,
                  )}
                </span>

                <span>
                  Actualizada{' '}
                  {formatDate(
                    ticket.updatedAtUtc,
                  )}
                </span>
              </div>
            </header>

            {waitingUser && (
              <section
                className="my-helpdesk-workflow__attention"
              >
                <PauseCircle
                  size={22}
                />

                <div>
                  <strong>
                    El equipo TIC necesita
                    una respuesta tuya
                  </strong>

                  <span>
                    El SLA de resolución
                    está pausado mientras
                    espera tu respuesta.
                    Al responder, el ticket
                    volverá automáticamente
                    a proceso.
                  </span>
                </div>
              </section>
            )}

            {terminal && (
              <section
                className={
                  ticket.canReopen
                    ? 'my-helpdesk-workflow__resolved'
                    : 'my-helpdesk-workflow__closed'
                }
              >
                <CheckCircle2
                  size={22}
                />

                <div>
                  <strong>
                    {
                      normalizedStatus ===
                        'closed'
                        ? 'Esta solicitud está cerrada'
                        : 'Esta solicitud fue resuelta'
                    }
                  </strong>

                  {ticket.canReopen ? (
                    <span>
                      Si el problema continúa,
                      puedes reabrirla dentro
                      del período permitido
                      de {
                        ticket.reopenWindowDays
                      } días.
                    </span>
                  ) : (
                    <span>
                      El período disponible
                      para reabrir esta
                      solicitud ya terminó.
                      Si necesitas ayuda
                      nuevamente, crea una
                      nueva solicitud.
                    </span>
                  )}
                </div>

                {canRequestReopen &&
                  ticket.canReopen && (
                  <button
                    type="button"
                    className={
                      'helpdesk-ui-button ' +
                      'helpdesk-ui-button--secondary'
                    }
                    onClick={
                      () => {
                        setError(
                          '',
                        )

                        setNotice(
                          '',
                        )

                        setReopenReason(
                          '',
                        )

                        setShowReopen(
                          true,
                        )
                      }
                    }
                  >
                    <RotateCcw
                      size={16}
                    />

                    Reabrir solicitud
                  </button>
                )}
              </section>
            )}

            <section
              className="my-helpdesk__card"
            >
              <h2>
                Tu solicitud
              </h2>

              <p
                className="my-helpdesk__body"
              >
                {
                  ticket.description
                }
              </p>

              <div
                className="my-helpdesk-workflow__request-meta"
              >
                <span>
                  <strong>
                    Prioridad
                  </strong>

                  {
                    ticket.priority
                  }
                </span>

                <span>
                  <strong>
                    Categoría
                  </strong>

                  {
                    ticket.category
                  }
                </span>

                {ticket.resolvedAtUtc && (
                  <span>
                    <strong>
                      Resolución
                    </strong>

                    {formatDate(
                      ticket.resolvedAtUtc,
                    )}
                  </span>
                )}
              </div>
            </section>

            <section
              className="my-helpdesk__card"
            >
              <h2>
                Conversación
              </h2>

              {!ticket
                .comments
                .length ? (
                <div
                  className="my-helpdesk__empty"
                >
                  <MessageSquare
                    size={26}
                  />

                  <strong>
                    Aún no hay respuestas
                  </strong>

                  <span>
                    El equipo TIC
                    responderá aquí.
                  </span>
                </div>
              ) : (
                <div
                  className="my-helpdesk__messages"
                >
                  {ticket.comments.map(
                    item => (
                      <article
                        key={
                          item.id
                        }
                      >
                        <div>
                          <strong>
                            {
                              item.authorName
                            }
                          </strong>

                          <time>
                            {formatDate(
                              item.createdAtUtc,
                            )}
                          </time>
                        </div>

                        <p
                          className="my-helpdesk__body"
                        >
                          {
                            item.body
                          }
                        </p>
                      </article>
                    ),
                  )}
                </div>
              )}

              {canReply &&
                !terminal && (
                <form
                  className="my-helpdesk__form"
                  onSubmit={
                    event =>
                      void sendReply(
                        event,
                      )
                  }
                >
                  <label
                    htmlFor="helpdesk-reply"
                  >
                    {
                      waitingUser
                        ? 'Responder para continuar la atención'
                        : 'Responder al equipo TIC'
                    }
                  </label>

                  <textarea
                    id="helpdesk-reply"
                    required
                    maxLength={
                      4000
                    }
                    rows={
                      4
                    }
                    value={
                      reply
                    }
                    onChange={
                      event =>
                        setReply(
                          event
                            .target
                            .value,
                        )
                    }
                    placeholder={
                      waitingUser
                        ? 'Escribe la información solicitada por el equipo TIC…'
                        : 'Escribe información adicional…'
                    }
                  />

                  <button
                    type="submit"
                    className={
                      'helpdesk-ui-button ' +
                      'helpdesk-ui-button--primary'
                    }
                    disabled={
                      saving
                      ||
                      !reply.trim()
                    }
                  >
                    <Send
                      size={16}
                    />

                    {
                      saving
                        ? 'Enviando…'
                        : waitingUser
                          ? 'Responder y continuar'
                          : 'Enviar respuesta'
                    }
                  </button>
                </form>
              )}
            </section>

            <section
              className="my-helpdesk__card"
            >
              <div
                className="my-helpdesk-workflow__section-heading"
              >
                <div>
                  <h2>
                    Actividad
                  </h2>

                  <p>
                    Seguimiento de los cambios
                    principales de tu solicitud.
                  </p>
                </div>

                <Clock3
                  size={19}
                />
              </div>

              {!ticket
                .activity
                .length ? (
                <div
                  className="my-helpdesk__empty"
                >
                  <Clock3
                    size={25}
                  />

                  <strong>
                    Sin actividad todavía
                  </strong>
                </div>
              ) : (
                <ol
                  className="my-helpdesk-workflow__timeline"
                >
                  {ticket.activity.map(
                    item => (
                      <li
                        key={
                          item.id
                        }
                      >
                        <span
                          className="my-helpdesk-workflow__timeline-dot"
                        />

                        <div>
                          <strong>
                            {
                              item.summary
                            }
                          </strong>

                          <time>
                            {formatDate(
                              item.createdAtUtc,
                            )}
                          </time>
                        </div>
                      </li>
                    ),
                  )}
                </ol>
              )}
            </section>

            {reopenExpired && (
              <section
                className="my-helpdesk-workflow__expired"
              >
                <AlertTriangle
                  size={19}
                />

                <span>
                  Esta solicitud ya no puede
                  reabrirse desde el portal.
                  Puedes crear una nueva si
                  necesitas asistencia adicional.
                </span>
              </section>
            )}
          </>
        )}

        {showReopen &&
          ticket && (
          <div
            className="my-helpdesk-workflow-modal"
            role="presentation"
            onMouseDown={
              event => {
                if (
                  event.target ===
                  event.currentTarget
                ) {
                  setShowReopen(
                    false,
                  )
                }
              }
            }
          >
            <form
              className="my-helpdesk-workflow-modal__dialog"
              onSubmit={
                event =>
                  void reopenTicket(
                    event,
                  )
              }
            >
              <header
                className="my-helpdesk-workflow-modal__header"
              >
                <div
                  className="my-helpdesk-workflow-modal__icon"
                >
                  <RotateCcw
                    size={21}
                  />
                </div>

                <div>
                  <h2>
                    Reabrir solicitud
                  </h2>

                  <p>
                    El equipo TIC volverá
                    a recibir este caso.
                  </p>
                </div>

                <button
                  type="button"
                  className="my-helpdesk-workflow-modal__close"
                  aria-label="Cerrar"
                  onClick={
                    () =>
                      setShowReopen(
                        false,
                      )
                  }
                >
                  <X
                    size={19}
                  />
                </button>
              </header>

              <div
                className="my-helpdesk-workflow-modal__body"
              >
                <div
                  className="my-helpdesk-workflow-modal__ticket"
                >
                  <strong>
                    {
                      ticket.number
                    }
                  </strong>

                  <span>
                    {
                      ticket.subject
                    }
                  </span>
                </div>

                <label
                  htmlFor="my-helpdesk-reopen-reason"
                >
                  ¿Por qué necesitas
                  reabrirla?
                </label>

                <textarea
                  id="my-helpdesk-reopen-reason"
                  autoFocus
                  required
                  minLength={
                    5
                  }
                  maxLength={
                    1000
                  }
                  rows={
                    5
                  }
                  value={
                    reopenReason
                  }
                  onChange={
                    event =>
                      setReopenReason(
                        event
                          .target
                          .value,
                      )
                  }
                  placeholder={
                    'Ejemplo: el inconveniente volvió a presentarse después de aplicar la solución…'
                  }
                />

                <div
                  className="my-helpdesk-workflow-modal__hint"
                >
                  <Clock3
                    size={15}
                  />

                  <span>
                    La reapertura quedará
                    registrada en el historial.
                    La ventana actual es de{' '}
                    <strong>
                      {
                        ticket.reopenWindowDays
                      } días
                    </strong>.
                  </span>
                </div>
              </div>

              <footer
                className="my-helpdesk-workflow-modal__footer"
              >
                <button
                  type="button"
                  className={
                    'helpdesk-ui-button ' +
                    'helpdesk-ui-button--secondary'
                  }
                  disabled={
                    saving
                  }
                  onClick={
                    () =>
                      setShowReopen(
                        false,
                      )
                  }
                >
                  Cancelar
                </button>

                <button
                  type="submit"
                  className={
                    'helpdesk-ui-button ' +
                    'helpdesk-ui-button--primary'
                  }
                  disabled={
                    saving
                    ||
                    reopenReason
                      .trim()
                      .length < 5
                  }
                >
                  <RotateCcw
                    size={16}
                  />

                  {
                    saving
                      ? 'Reabriendo…'
                      : 'Confirmar reapertura'
                  }
                </button>
              </footer>
            </form>
          </div>
        )}
      </main>
    )
  }

  /*
   * ============================================================
   * REQUESTER HOME
   * ============================================================
   */

  return (
    <main
      className={
        'titan-page ' +
        'helpdesk-page ' +
        'my-helpdesk'
      }
    >
      <header
        className="my-helpdesk__hero"
      >
        <div
          className="my-helpdesk__heading"
        >
          <div>
            <span
              className="helpdesk-inbox__eyebrow"
            >
              <Headphones
                size={15}
              />

              PORTAL DE SOPORTE
            </span>

            <h1>
              Mesa de Ayuda
            </h1>

            <p>
              Crea solicitudes,
              consulta su estado y
              conversa con el equipo TIC.
            </p>
          </div>

          {canCreate && (
            <button
              type="button"
              className={
                'helpdesk-ui-button ' +
                'helpdesk-ui-button--primary'
              }
              onClick={
                () =>
                  navigate(
                    '/my-support/new?workspace=helpdesk',
                  )
              }
            >
              <Plus
                size={16}
              />

              Crear solicitud
            </button>
          )}
        </div>
      </header>

      {error && (
        <div
          className="helpdesk-inbox__error"
          role="alert"
        >
          <AlertTriangle
            size={17}
          />

          {error}
        </div>
      )}

      <section
        className="my-helpdesk__card"
      >
        <div
          className="my-helpdesk__heading"
        >
          <div>
            <h2>
              Mis solicitudes
            </h2>

            <p>
              Solo puedes ver
              tus propios tickets.
            </p>
          </div>

          <span
            className="helpdesk-inbox__total"
          >
            {
              tickets.length
            } solicitudes
          </span>
        </div>

        {loading ? (
          <div
            className="my-helpdesk__empty"
          >
            Cargando…
          </div>
        ) : !tickets.length ? (
          <div
            className="my-helpdesk__empty"
          >
            <Ticket
              size={28}
            />

            <strong>
              No tienes solicitudes
            </strong>

            <span>
              Cuando necesites
              soporte podrás crear
              una solicitud aquí.
            </span>
          </div>
        ) : (
          <div
            className="my-helpdesk__list"
          >
            {tickets.map(
              item => (
                <button
                  type="button"
                  key={
                    item.id
                  }
                  onClick={
                    () =>
                      navigate(
                        `/my-support/${item.id}?workspace=helpdesk`,
                      )
                  }
                >
                  <span>
                    <small>
                      {
                        item.number
                      }
                    </small>

                    <strong>
                      {
                        item.subject
                      }
                    </strong>

                    <small>
                      Actualizado{' '}
                      {formatDate(
                        item.updatedAtUtc,
                      )}
                    </small>
                  </span>

                  <span
                    className="my-helpdesk__meta"
                  >
                    {badge(
                      item.status,
                    )}

                    <ArrowRight
                      size={17}
                    />
                  </span>
                </button>
              ),
            )}
          </div>
        )}
      </section>
    </main>
  )
}