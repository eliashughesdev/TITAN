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
  CheckCircle2,
  Clock3,
  Headphones,
  LockKeyhole,
  MessageSquare,
  Monitor,
  PauseCircle,
  PlayCircle,
  RefreshCw,
  RotateCcw,
  Send,
  UserRound,
  X,
} from 'lucide-react'

import axios
  from 'axios'

import {
  helpdeskApi,
  type HelpdeskTicketDetails,
} from '../../api/helpdeskApi'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
  legacyHelpdeskPermissions,
} from '../../auth/helpdeskAccess'

import './HelpdeskPages.css'
import './HelpdeskWorkflowActions.css'

import {
  HelpdeskTicketAttachmentsPanel,
} from './HelpdeskTicketAttachmentsPanel'

const STATUS:
  Record<string, string> = {
    new:
      'Nuevo',

    open:
      'Abierto',

    inprogress:
      'En proceso',

    pendinguser:
      'Esperando usuario',

    resolved:
      'Resuelto',

    closed:
      'Cerrado',
  }

const PRIORITY:
  Record<string, string> = {
    low:
      'Baja',

    medium:
      'Media',

    high:
      'Alta',

    critical:
      'Crítica',
  }

const dateFormatter =
  new Intl.DateTimeFormat(
    'es-DO',
    {
      dateStyle:
        'medium',

      timeStyle:
        'short',
    },
  )

function formatDate(
  value?: string | null,
) {
  if (!value) {
    return 'Sin fecha'
  }

  const date =
    new Date(
      value,
    )

  return Number.isNaN(
    date.getTime(),
  )
    ? 'Sin fecha'
    : dateFormatter.format(
        date,
      )
}

function errorMessage(
  error: unknown,
  fallback: string,
) {
  if (
    axios.isAxiosError<{
      message?: string
    }>(
      error,
    )
  ) {
    return (
      error.response
        ?.data
        ?.message
      ??
      fallback
    )
  }

  return fallback
}

export function HelpdeskTicketPage() {
  const {
    ticketId,
  } =
    useParams()

  const navigate =
    useNavigate()

  const {
    user,
    hasPermission,
  } =
    useAuth()

  const [
    ticket,
    setTicket,
  ] =
    useState<HelpdeskTicketDetails | null>(
      null,
    )

  const [
    comment,
    setComment,
  ] =
    useState(
      '',
    )

  const [
    internal,
    setInternal,
  ] =
    useState(
      false,
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
    useState<string | null>(
      null,
    )

  const [
    notice,
    setNotice,
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

  // ============================================================
  // PERMISSIONS
  // ============================================================

  const canComment =
    hasPermission(
      helpdeskPermissions
        .ticketComment,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketComment,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canInternalNote =
    hasPermission(
      helpdeskPermissions
        .ticketInternalNote,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketComment,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canAssign =
    hasPermission(
      helpdeskPermissions
        .ticketAssign,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .ticketTake,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketAssign,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canTransition =
    hasPermission(
      helpdeskPermissions
        .ticketTransition,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketComment,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canResolve =
    hasPermission(
      helpdeskPermissions
        .ticketResolve,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketClose,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canClose =
    hasPermission(
      helpdeskPermissions
        .ticketClose,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketClose,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canReopen =
    hasPermission(
      helpdeskPermissions
        .ticketReopen,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketClose,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  // ============================================================
  // LOAD
  // ============================================================

  const load =
    useCallback(
      async () => {
        if (!ticketId) {
          setError(
            'No se indicó un ticket válido.',
          )

          setLoading(
            false,
          )

          return
        }

        setLoading(
          true,
        )

        setError(
          null,
        )

        try {
          const result =
            await helpdeskApi
              .getTicket(
                ticketId,
              )

          setTicket(
            result,
          )
        }
        catch (
          exception
        ) {
          setError(
            errorMessage(
              exception,
              'No se pudo cargar el ticket.',
            ),
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [
        ticketId,
      ],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  // ============================================================
  // COMMENTS
  // ============================================================

  async function sendComment(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !ticketId
      ||
      !comment.trim()
      ||
      saving
    ) {
      return
    }

    if (
      internal
      &&
      !canInternalNote
    ) {
      setError(
        'Tu rol no permite registrar notas internas.',
      )

      return
    }

    if (
      !internal
      &&
      !canComment
    ) {
      setError(
        'Tu rol no permite responder al solicitante.',
      )

      return
    }

    setSaving(
      true,
    )

    setError(
      null,
    )

    setNotice(
      '',
    )

    try {
      const updated =
        await helpdeskApi
          .addComment(
            ticketId,
            comment.trim(),
            internal,
          )

      setTicket(
        updated,
      )

      setComment(
        '',
      )

      setInternal(
        false,
      )

      setNotice(
        internal
          ? 'Nota interna registrada.'
          : 'Respuesta publicada correctamente.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo publicar el comentario.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // STATUS
  // ============================================================

  async function changeStatus(
    status: string,
  ) {
    if (
      !ticketId
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      null,
    )

    setNotice(
      '',
    )

    try {
      const updated =
        await helpdeskApi
          .transition(
            ticketId,
            status,
          )

      setTicket(
        updated,
      )

      setNotice(
        `Estado actualizado a «${STATUS[status] ?? status}».`,
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo actualizar el estado del ticket.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // REOPEN
  // ============================================================

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
        .length <
        5
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      null,
    )

    setNotice(
      '',
    )

    try {
      const updated =
        await helpdeskApi
          .reopen(
            ticketId,
            reopenReason.trim(),
          )

      setTicket(
        updated,
      )

      setReopenReason(
        '',
      )

      setShowReopen(
        false,
      )

      setNotice(
        'Ticket reabierto correctamente.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo reabrir el ticket.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // TAKE
  // ============================================================

  async function takeTicket() {
    if (
      !ticketId
      ||
      !user?.id
      ||
      !canAssign
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      null,
    )

    setNotice(
      '',
    )

    try {
      const updated =
        await helpdeskApi
          .assign(
            ticketId,
            user.id,
          )

      setTicket(
        updated,
      )

      setNotice(
        'Ticket asignado a tu usuario.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo tomar el ticket.',
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // EMPTY / LOADING
  // ============================================================

  if (
    loading
    &&
    !ticket
  ) {
    return (
      <main
        className="titan-page helpdesk-page helpdesk-detail"
      >
        <div
          className="helpdesk-detail__loading"
        >
          Cargando ticket…
        </div>
      </main>
    )
  }

  if (!ticket) {
    return (
      <main
        className="titan-page helpdesk-page helpdesk-detail"
      >
        <div
          className="helpdesk-detail__loading"
        >
          <p>
            {
              error
              ??
              'No se encontró el ticket.'
            }
          </p>

          <button
            type="button"
            className="helpdesk-ui-button helpdesk-ui-button--secondary"
            onClick={
              () =>
                navigate(
                  '/helpdesk?workspace=helpdesk',
                )
            }
          >
            <ArrowLeft
              size={16}
            />

            Volver
          </button>
        </div>
      </main>
    )
  }

  const normalizedStatus =
    ticket.status
      .trim()
      .toLowerCase()

  const terminal =
    normalizedStatus ===
      'resolved'
    ||
    normalizedStatus ===
      'closed'

  const slaPaused =
    normalizedStatus ===
      'pendinguser'

  const canTake =
    canAssign
    &&
    !terminal
    &&
    ticket.assigneeUserId !==
      user?.id

  const showOpen =
    canTransition
    &&
    !terminal
    &&
    normalizedStatus !==
      'open'

  const showInProgress =
    canTransition
    &&
    !terminal
    &&
    normalizedStatus !==
      'inprogress'

  const showPending =
    canTransition
    &&
    !terminal
    &&
    normalizedStatus !==
      'pendinguser'

  const showResolve =
    canResolve
    &&
    !terminal

  const showClose =
    canClose
    &&
    normalizedStatus ===
      'resolved'

  const showReopenAction =
  canReopen
  &&
  terminal

  return (
    <main
      className="titan-page helpdesk-page helpdesk-detail"
    >
      <div
        className="helpdesk-detail__back"
      >
        <button
          type="button"
          onClick={
            () =>
              navigate(
                '/helpdesk?workspace=helpdesk',
              )
          }
        >
          <ArrowLeft
            size={16}
          />

          Volver a tickets
        </button>
      </div>

      <header
        className="helpdesk-detail__header"
      >
        <div>
          <span
            className="helpdesk-inbox__eyebrow"
          >
            <Headphones
              size={15}
            />

            Ticket {
              ticket.number
            }
          </span>

          <h1>
            {
              ticket.subject
            }
          </h1>

          <div
            className="helpdesk-detail__header-meta"
          >
            <span
              className={
                `helpdesk-inbox__badge ` +
                `helpdesk-inbox__badge--${normalizedStatus}`
              }
            >
              {
                STATUS[
                  normalizedStatus
                ]
                ??
                ticket.status
              }
            </span>

            <span>
              Prioridad{' '}
              {
                PRIORITY[
                  ticket.priority
                ]
                ??
                ticket.priority
              }
            </span>

            <span>
              Creado{' '}
              {formatDate(
                ticket.createdAtUtc,
              )}
            </span>

            {slaPaused && (
              <span
                className="helpdesk-workflow__sla-paused"
              >
                <PauseCircle
                  size={14}
                />

                SLA pausado
              </span>
            )}

            {ticket.slaBreached && (
              <span
                className="helpdesk-detail__breached"
              >
                <AlertTriangle
                  size={14}
                />

                SLA vencido
              </span>
            )}
          </div>
        </div>

        <button
          type="button"
          className="helpdesk-ui-button helpdesk-ui-button--secondary"
          disabled={
            loading
          }
          onClick={
            () =>
              void load()
          }
        >
          <RefreshCw
            size={16}
          />

          Actualizar
        </button>
      </header>

      {notice && (
        <div
          className="helpdesk-workflow__success"
          role="status"
        >
          <CheckCircle2
            size={17}
          />

          {notice}
        </div>
      )}

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

      {slaPaused && (
        <div
          className="helpdesk-workflow__notice"
        >
          <PauseCircle
            size={18}
          />

          <div>
            <strong>
              Esperando respuesta del solicitante
            </strong>

            <span>
              El SLA está pausado mientras el ticket permanezca en este estado.
              Cuando el usuario responda, TitanMDM reanudará el SLA automáticamente.
            </span>
          </div>
        </div>
      )}

      <div
        className="helpdesk-detail__layout"
      >
        <div
          className="helpdesk-detail__main"
        >
          <section
            className="helpdesk-detail__card"
          >
            <div
              className="helpdesk-detail__section-title"
            >
              <div>
                <h2>
                  Descripción del caso
                </h2>

                <p>
                  Información registrada al crear el ticket.
                </p>
              </div>
            </div>

            <p
              className="helpdesk-detail__description"
            >
              {
                ticket.description
                ||
                'No se agregó una descripción.'
              }
            </p>

            <div
              className="helpdesk-detail__attributes"
            >
              <span>
                Tipo:{' '}
                <strong>
                  {
                    ticket.type
                  }
                </strong>
              </span>

              <span>
                Categoría:{' '}
                <strong>
                  {
                    ticket.category
                  }
                </strong>
              </span>

              <span>
                Origen:{' '}
                <strong>
                  {
                    ticket.source
                  }
                </strong>
              </span>
            </div>
          </section>

          <section
            className="helpdesk-detail__card"
          >
            <div
              className="helpdesk-detail__section-title"
            >
              <div>
                <h2>
                  Conversación
                </h2>

                <p>
                  Respuestas públicas y notas internas.
                </p>
              </div>

              <span
                className="helpdesk-detail__count"
              >
                {
                  ticket.comments.length
                }
              </span>
            </div>

            <div
              className="helpdesk-detail__conversation"
            >
              {ticket.comments.length ===
                0 ? (
                <div
                  className="helpdesk-detail__empty"
                >
                  <MessageSquare
                    size={25}
                  />

                  <strong>
                    Aún no hay respuestas
                  </strong>

                  <span>
                    La conversación aparecerá aquí.
                  </span>
                </div>
              ) : (
                ticket.comments.map(
                  item => (
                    <article
                      key={
                        item.id
                      }
                      className={
                        `helpdesk-detail__message` +
                        (
                          item.isInternal
                            ? ' helpdesk-detail__message--internal'
                            : ''
                        )
                      }
                    >
                      <div
                        className="helpdesk-detail__message-top"
                      >
                        <span
                          className="helpdesk-detail__avatar"
                        >
                          {
                            item.authorName
                              .charAt(
                                0,
                              )
                              .toUpperCase()
                          }
                        </span>

                        <div>
                          <strong>
                            {
                              item.authorName
                            }
                          </strong>

                          <span>
                            {formatDate(
                              item.createdAtUtc,
                            )}
                          </span>
                        </div>

                        {item.isInternal && (
                          <small>
                            <LockKeyhole
                              size={13}
                            />

                            Nota interna
                          </small>
                        )}
                      </div>

                      <p>
                        {
                          item.body
                        }
                      </p>
                    </article>
                  ),
                )
              )}
            </div>

            {(canComment ||
              canInternalNote)
              &&
              !terminal && (
              <form
                className="helpdesk-detail__composer"
                onSubmit={
                  event =>
                    void sendComment(
                      event,
                    )
                }
              >
                <label
                  htmlFor="helpdesk-reply"
                >
                  {
                    internal
                      ? 'Nota interna'
                      : 'Respuesta al solicitante'
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
                    comment
                  }
                  onChange={
                    event =>
                      setComment(
                        event.target.value,
                      )
                  }
                  placeholder={
                    internal
                      ? 'Escribe una nota visible solo para TIC…'
                      : 'Escribe una respuesta para el solicitante…'
                  }
                />

                <div
                  className="helpdesk-detail__composer-footer"
                >
                  {canInternalNote && (
                    <label
                      className="helpdesk-detail__internal"
                    >
                      <input
                        type="checkbox"
                        checked={
                          internal
                        }
                        onChange={
                          event =>
                            setInternal(
                              event.target.checked,
                            )
                        }
                      />

                      <LockKeyhole
                        size={15}
                      />

                      Nota interna
                    </label>
                  )}

                  <button
                    type="submit"
                    className="helpdesk-ui-button helpdesk-ui-button--primary"
                    disabled={
                      saving
                      ||
                      !comment.trim()
                    }
                  >
                    <Send
                      size={15}
                    />

                    {
                      saving
                        ? 'Publicando…'
                        : 'Publicar'
                    }
                  </button>
                </div>
              </form>
            )}
          </section>

                  <HelpdeskTicketAttachmentsPanel
          ticketId={
            ticket.id
          }
/>

          <section
            className="helpdesk-detail__card"
          >
            <div
              className="helpdesk-detail__section-title"
            >
              <div>
                <h2>
                  Actividad
                </h2>

                <p>
                  Historial auditado del ticket.
                </p>
              </div>

              <Clock3
                size={18}
              />
            </div>

            {ticket.timeline.length ===
              0 ? (
              <p
                className="helpdesk-detail__muted"
              >
                Todavía no hay eventos.
              </p>
            ) : (
              <ol
                className="helpdesk-detail__timeline"
              >
                {ticket.timeline.map(
                  item => (
                    <li
                      key={
                        item.id
                      }
                    >
                      <span
                        className="helpdesk-detail__timeline-dot"
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
        </div>

        <aside
          className="helpdesk-detail__sidebar"
        >
          <section
            className="helpdesk-detail__card"
          >
            <h2>
              Workflow
            </h2>

            {showOpen && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void changeStatus(
                      'open',
                    )
                }
              >
                Abrir
              </button>
            )}

            {showInProgress && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void changeStatus(
                      'inprogress',
                    )
                }
              >
                <PlayCircle
                  size={15}
                />

                En proceso
              </button>
            )}

            {showPending && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void changeStatus(
                      'pendinguser',
                    )
                }
              >
                <PauseCircle
                  size={15}
                />

                Esperando usuario
              </button>
            )}

            {showResolve && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--primary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void changeStatus(
                      'resolved',
                    )
                }
              >
                <CheckCircle2
                  size={15}
                />

                Resolver
              </button>
            )}

            {showClose && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--primary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void changeStatus(
                      'closed',
                    )
                }
              >
                Cerrar
              </button>
            )}

           {showReopenAction && (
  <button
    type="button"
    className="helpdesk-workflow__reopen-button helpdesk-ui-button helpdesk-detail__full"
    disabled={
      saving
    }
    onClick={
      () =>
        setShowReopen(
          true,
        )
    }
  >
    <RotateCcw
      size={15}
    />

    Reabrir
  </button>
)}
          </section>

          <section
            className="helpdesk-detail__card"
          >
            <h2>
              Responsables
            </h2>

            <div
              className="helpdesk-detail__info-row"
            >
              <UserRound
                size={17}
              />

              <div>
                <span>
                  Solicitante
                </span>

                <strong>
                  {
                    ticket.requesterName
                  }
                </strong>

                {ticket.entraUserPrincipalName && (
                  <small>
                    {
                      ticket.entraUserPrincipalName
                    }
                  </small>
                )}
              </div>
            </div>

            <div
              className="helpdesk-detail__info-row"
            >
              <Headphones
                size={17}
              />

              <div>
                <span>
                  Técnico asignado
                </span>

                <strong>
                  {
                    ticket.assigneeName
                    ??
                    'Sin asignar'
                  }
                </strong>
              </div>
            </div>

            {canTake && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary helpdesk-detail__full"
                disabled={
                  saving
                }
                onClick={
                  () =>
                    void takeTicket()
                }
              >
                Tomar ticket
              </button>
            )}
          </section>

          <section
            className="helpdesk-detail__card"
          >
            <h2>
              SLA
            </h2>

            <div
              className="helpdesk-detail__info-row"
            >
              <Clock3
                size={17}
              />

              <div>
                <span>
                  Primera respuesta
                </span>

                <strong>
                  {slaPaused
                    ? 'Pausado'
                    : formatDate(
                        ticket.firstResponseDueAtUtc,
                      )}
                </strong>
              </div>
            </div>

            <div
              className="helpdesk-detail__info-row"
            >
              <Clock3
                size={17}
              />

              <div>
                <span>
                  Resolución
                </span>

                <strong>
                  {slaPaused
                    ? 'Pausado'
                    : formatDate(
                        ticket.resolveDueAtUtc,
                      )}
                </strong>
              </div>
            </div>
          </section>

          <section
            className="helpdesk-detail__card"
          >
            <h2>
              Dispositivo
            </h2>

            <div
              className="helpdesk-detail__info-row"
            >
              <Monitor
                size={18}
              />

              <div>
                <span>
                  Equipo relacionado
                </span>

                <strong>
                  {
                    ticket.deviceName
                    ??
                    'Sin dispositivo vinculado'
                  }
                </strong>

                {ticket.devicePlatform && (
                  <small>
                    {
                      ticket.devicePlatform
                    }
                  </small>
                )}
              </div>
            </div>

            {ticket.deviceId && (
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary helpdesk-detail__full"
                onClick={
                  () =>
                    navigate(
                      `/devices/${ticket.deviceId}`,
                    )
                }
              >
                Ver dispositivo
              </button>
            )}
          </section>
        </aside>
      </div>

      {showReopen && (
        <div
          className="helpdesk-workflow-modal"
          role="presentation"
        >
          <form
            className="helpdesk-workflow-modal__dialog"
            onSubmit={
              event =>
                void reopenTicket(
                  event,
                )
            }
          >
            <header
              className="helpdesk-workflow-modal__header"
            >
              <div
                className="helpdesk-workflow-modal__icon"
              >
                <RotateCcw
                  size={20}
                />
              </div>

              <div>
                <h2>
                  Reabrir ticket
                </h2>

                <p>
                  El motivo quedará registrado en auditoría.
                </p>
              </div>

              <button
                type="button"
                className="helpdesk-workflow-modal__close"
                aria-label="Cerrar"
                onClick={
                  () =>
                    setShowReopen(
                      false,
                    )
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div
              className="helpdesk-workflow-modal__body"
            >
              <label
                htmlFor="helpdesk-reopen"
              >
                Motivo de reapertura
              </label>

              <textarea
                id="helpdesk-reopen"
                autoFocus
                required
                minLength={
                  5
                }
                maxLength={
                  1000
                }
                value={
                  reopenReason
                }
                onChange={
                  event =>
                    setReopenReason(
                      event.target.value,
                    )
                }
                placeholder="Describe por qué el caso debe volver a trabajarse…"
              />

              <small>
                Mínimo 5 caracteres.
              </small>
            </div>

            <footer
              className="helpdesk-workflow-modal__footer"
            >
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary"
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
                className="helpdesk-ui-button helpdesk-ui-button--primary"
                disabled={
                  saving
                  ||
                  reopenReason
                    .trim()
                    .length <
                    5
                }
              >
                <RotateCcw
                  size={15}
                />

                {
                  saving
                    ? 'Reabriendo…'
                    : 'Confirmar'
                }
              </button>
            </footer>
          </form>
        </div>
      )}
    </main>
  )
}