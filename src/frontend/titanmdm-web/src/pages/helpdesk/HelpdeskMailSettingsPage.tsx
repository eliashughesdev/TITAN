import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from 'react'

import axios
  from 'axios'

import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Filter,
  Inbox,
  Mail,
  MailCheck,
  RefreshCw,
  RotateCcw,
  Save,
  Send,
  ServerCog,
  ShieldCheck,
  ShieldX,
  UserCog,
  XCircle,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
} from '../../auth/helpdeskAccess'

import './HelpdeskMailSettingsPage.css'

// ============================================================
// TYPES
// ============================================================

interface MailStatistics {
  received: number
  accepted: number
  ignored: number
  failed: number

  sent: number
  pending: number
  retry: number
  sending: number
  deadLetter: number
}

interface MailSettings {
  mailbox:
    string | null

  acceptedRecipients:
    string | null

  actorUserId:
    string | null

  actorName:
    string | null

  inboundEnabled:
    boolean

  outboundEnabled:
    boolean

  ignoreAutomaticMessages:
    boolean

  ignoreBulkMessages:
    boolean

  ignoreBounceMessages:
    boolean

  ignoreNoReplyMessages:
    boolean

  blockedSenders:
    string | null

  blockedDomains:
    string | null

  allowedSenders:
    string | null

  allowedDomains:
    string | null

  ignoredSubjectPatterns:
    string | null

  inboundPollSeconds:
    number

  outboundPollSeconds:
    number

  batchSize:
    number

  maxAttempts:
    number

  lastInboundAttemptAtUtc:
    string | null

  lastInboundSuccessAtUtc:
    string | null

  lastInboundError:
    string | null

  lastOutboundAttemptAtUtc:
    string | null

  lastOutboundSuccessAtUtc:
    string | null

  lastOutboundError:
    string | null

  revision:
    number

  updatedAtUtc:
    string | null

  statistics:
    MailStatistics
}

interface MailActor {
  id: string
  name: string
  email:
    string | null
}

interface GraphDiagnostic {
  success: boolean

  message: string

  mailbox?: string

  acceptedRecipients?: string

  inbox?: string

  totalItemCount?: number

  unreadItemCount?: number

  inboundVerified:
    boolean

  outboundConfigured:
    boolean
}

interface MailActivityItem {
  id: string

  ticketId:
    string | null

  mailbox:
    string

  fromEmail:
    string

  subject:
    string | null

  decision:
    'accepted' |
    'ignored' |
    'failed' |
    string

  reasonCode:
    string

  reason:
    string

  receivedAtUtc:
    string | null

  processedAtUtc:
    string

  internetMessageId:
    string

  conversationId:
    string | null
}

// ============================================================
// HELPERS
// ============================================================

function errorMessage(
  exception: unknown,
  fallback: string,
) {
  if (
    axios.isAxiosError(
      exception,
    )
    &&
    typeof exception
      .response
      ?.data
      ?.message ===
      'string'
  ) {
    return exception
      .response
      .data
      .message
  }

  return fallback
}

function formatDate(
  value:
    string |
    null |
    undefined,
) {
  if (!value) {
    return 'Nunca'
  }

  const date =
    new Date(
      value,
    )

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return 'No disponible'
  }

  return new Intl
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
      date,
    )
}

function decisionLabel(
  value: string,
) {
  switch (
    value
      .trim()
      .toLowerCase()
  ) {
    case 'accepted':
      return 'Aceptado'

    case 'ignored':
      return 'Ignorado'

    case 'failed':
      return 'Fallido'

    default:
      return value
  }
}

function decisionClass(
  value: string,
) {
  switch (
    value
      .trim()
      .toLowerCase()
  ) {
    case 'accepted':
      return 'helpdesk-mail-status helpdesk-mail-status--accepted'

    case 'ignored':
      return 'helpdesk-mail-status helpdesk-mail-status--ignored'

    case 'failed':
      return 'helpdesk-mail-status helpdesk-mail-status--failed'

    default:
      return 'helpdesk-mail-status'
  }
}

// ============================================================
// PAGE
// ============================================================

export function HelpdeskMailSettingsPage() {
  const {
    hasPermission,
  } =
    useAuth()

  const canManage =
    hasPermission(
      helpdeskPermissions
        .mailManage,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )
    ||
    hasPermission(
      'helpdesk.manage',
    )
    ||
    hasPermission(
      'settings.manage',
    )

  const [
    settings,
    setSettings,
  ] =
    useState<
      MailSettings |
      null
    >(
      null,
    )

  const [
    actors,
    setActors,
  ] =
    useState<
      MailActor[]
    >(
      [],
    )

  const [
    activity,
    setActivity,
  ] =
    useState<
      MailActivityItem[]
    >(
      [],
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    activityLoading,
    setActivityLoading,
  ] =
    useState(
      false,
    )

  const [
    saving,
    setSaving,
  ] =
    useState(
      false,
    )

  const [
    testing,
    setTesting,
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
    success,
    setSuccess,
  ] =
    useState(
      '',
    )

  const [
    diagnostic,
    setDiagnostic,
  ] =
    useState<
      GraphDiagnostic |
      null
    >(
      null,
    )

  // ============================================================
  // LOAD
  // ============================================================

  const loadActivity =
    useCallback(
      async () => {
        setActivityLoading(
          true,
        )

        try {
          const response =
            await apiClient
              .get<
                MailActivityItem[]
              >(
                '/helpdesk/mail/activity',
                {
                  params: {
                    take:
                      50,
                  },
                },
              )

          setActivity(
            response.data,
          )
        }
        catch {
          /*
           * La actividad es secundaria.
           * No bloqueamos toda la pantalla
           * si este endpoint falla.
           */
        }
        finally {
          setActivityLoading(
            false,
          )
        }
      },
      [],
    )

  const load =
    useCallback(
      async () => {
        setLoading(
          true,
        )

        setError(
          '',
        )

        try {
          const [
            settingsResponse,
          ] =
            await Promise.all(
              [
                apiClient
                  .get<MailSettings>(
                    '/helpdesk/mail',
                  ),
              ],
            )

          setSettings(
            settingsResponse.data,
          )

          if (canManage) {
            const actorResponse =
              await apiClient
                .get<
                  MailActor[]
                >(
                  '/helpdesk/mail/actors',
                )

            setActors(
              actorResponse.data,
            )
          }

          await loadActivity()
        }
        catch (
          exception
        ) {
          setError(
            errorMessage(
              exception,
              'No se pudo cargar la configuración de correo.',
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
        canManage,
        loadActivity,
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
  // SELECTED ACTOR
  // ============================================================

  const selectedActor =
    useMemo(
      () =>
        actors.find(
          actor =>
            actor.id ===
            settings
              ?.actorUserId,
        )
        ??
        null,
      [
        actors,
        settings
          ?.actorUserId,
      ],
    )

  // ============================================================
  // VALIDATION
  // ============================================================

  function validate() {
    if (!settings) {
      return 'No existe configuración para guardar.'
    }

    const mailbox =
      settings.mailbox
        ?.trim()
        ??
        ''

    if (
      (
        settings.inboundEnabled
        ||
        settings.outboundEnabled
      )
      &&
      !mailbox
    ) {
      return 'Debes configurar el buzón antes de habilitar el servicio.'
    }

    if (
      settings.inboundEnabled
      &&
      !settings.actorUserId
    ) {
      return 'Selecciona un usuario técnico para procesar correo entrante.'
    }

    if (
      settings.inboundEnabled
      &&
      !(
        settings.acceptedRecipients
          ?.trim()
        ||
        mailbox
      )
    ) {
      return 'Debes indicar al menos un destinatario aceptado.'
    }

    if (
      settings.inboundPollSeconds <
        30
      ||
      settings.inboundPollSeconds >
        3600
    ) {
      return 'El intervalo de entrada debe estar entre 30 y 3600 segundos.'
    }

    if (
      settings.outboundPollSeconds <
        10
      ||
      settings.outboundPollSeconds >
        3600
    ) {
      return 'El intervalo de salida debe estar entre 10 y 3600 segundos.'
    }

    if (
      settings.batchSize <
        1
      ||
      settings.batchSize >
        100
    ) {
      return 'El tamaño de lote debe estar entre 1 y 100.'
    }

    if (
      settings.maxAttempts <
        1
      ||
      settings.maxAttempts >
        20
    ) {
      return 'Los reintentos deben estar entre 1 y 20.'
    }

    return ''
  }

  // ============================================================
  // SAVE
  // ============================================================

  async function save(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !settings
      ||
      !canManage
      ||
      saving
    ) {
      return
    }

    const validation =
      validate()

    if (validation) {
      setError(
        validation,
      )

      setSuccess(
        '',
      )

      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    try {
      const response =
        await apiClient
          .put<MailSettings>(
            '/helpdesk/mail',
            {
              mailbox:
                settings.mailbox,

              acceptedRecipients:
                settings
                  .acceptedRecipients,

              actorUserId:
                settings.actorUserId,

              inboundEnabled:
                settings
                  .inboundEnabled,

              outboundEnabled:
                settings
                  .outboundEnabled,

              ignoreAutomaticMessages:
                settings
                  .ignoreAutomaticMessages,

              ignoreBulkMessages:
                settings
                  .ignoreBulkMessages,

              ignoreBounceMessages:
                settings
                  .ignoreBounceMessages,

              ignoreNoReplyMessages:
                settings
                  .ignoreNoReplyMessages,

              blockedSenders:
                settings
                  .blockedSenders,

              blockedDomains:
                settings
                  .blockedDomains,

              allowedSenders:
                settings
                  .allowedSenders,

              allowedDomains:
                settings
                  .allowedDomains,

              ignoredSubjectPatterns:
                settings
                  .ignoredSubjectPatterns,

              inboundPollSeconds:
                settings
                  .inboundPollSeconds,

              outboundPollSeconds:
                settings
                  .outboundPollSeconds,

              batchSize:
                settings
                  .batchSize,

              maxAttempts:
                settings
                  .maxAttempts,

              revision:
                settings
                  .revision,
            },
          )

      setSettings(
        response.data,
      )

      setDiagnostic(
        null,
      )

      setSuccess(
        'Configuración de correo guardada correctamente.',
      )

      await loadActivity()
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo guardar la configuración.',
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
  // GRAPH TEST
  // ============================================================

  async function testMailbox() {
    if (
      !canManage
      ||
      testing
    ) {
      return
    }

    setTesting(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    setDiagnostic(
      null,
    )

    try {
      const response =
        await apiClient
          .post<GraphDiagnostic>(
            '/helpdesk/mail/test',
          )

      setDiagnostic(
        response.data,
      )

      if (
        response.data
          .success
      ) {
        setSuccess(
          response.data
            .message,
        )
      }
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
          'No se pudo validar Microsoft Graph.',
        ),
      )
    }
    finally {
      setTesting(
        false,
      )
    }
  }

  // ============================================================
  // STATES
  // ============================================================

  if (loading) {
    return (
      <main
        className="titan-page helpdesk-mail-page"
      >
        <div
          className="helpdesk-mail-loading"
        >
          <RefreshCw
            size={20}
          />

          Cargando configuración
          de correo…
        </div>
      </main>
    )
  }

  if (!settings) {
    return (
      <main
        className="titan-page helpdesk-mail-page"
      >
        <div
          className="helpdesk-mail-message helpdesk-mail-message--error"
        >
          <AlertTriangle
            size={18}
          />

          {
            error
            ||
            'No se pudo cargar la configuración.'
          }
        </div>
      </main>
    )
  }

  const statistics =
    settings.statistics

  // ============================================================
  // RENDER
  // ============================================================

  return (
    <main
      className="titan-page helpdesk-mail-page"
    >
      <header
        className="helpdesk-mail-hero"
      >
        <div>
          <span
            className="helpdesk-mail-eyebrow"
          >
            <Mail
              size={14}
            />

            HELPDESK · MICROSOFT 365
          </span>

          <h1>
            Correo de Mesa de Ayuda
          </h1>

          <p>
            Controla qué mensajes pueden
            convertirse en tickets,
            audita correos ignorados y
            administra Microsoft Graph
            desde TitanMDM.
          </p>
        </div>

        <button
          type="button"
          className="helpdesk-mail-refresh"
          disabled={
            loading
            ||
            saving
            ||
            testing
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

      {error && (
        <div
          className="helpdesk-mail-message helpdesk-mail-message--error"
          role="alert"
        >
          <AlertTriangle
            size={18}
          />

          {error}
        </div>
      )}

      {success && (
        <div
          className="helpdesk-mail-message helpdesk-mail-message--success"
          role="status"
        >
          <CheckCircle2
            size={18}
          />

          {success}
        </div>
      )}

      {/* ====================================================== */}
      {/* METRICS                                                */}
      {/* ====================================================== */}

      <section
        className="helpdesk-mail-metrics helpdesk-mail-metrics--enterprise"
      >
        <article>
          <Inbox
            size={18}
          />

          <span>
            Recibidos
          </span>

          <strong>
            {statistics.received}
          </strong>
        </article>

        <article>
          <CheckCircle2
            size={18}
          />

          <span>
            Aceptados
          </span>

          <strong>
            {statistics.accepted}
          </strong>
        </article>

        <article>
          <ShieldX
            size={18}
          />

          <span>
            Ignorados
          </span>

          <strong>
            {statistics.ignored}
          </strong>
        </article>

        <article
          className={
            statistics.failed >
              0
              ? 'helpdesk-mail-metric--danger'
              : ''
          }
        >
          <AlertTriangle
            size={18}
          />

          <span>
            Fallidos
          </span>

          <strong>
            {statistics.failed}
          </strong>
        </article>

        <article>
          <Send
            size={18}
          />

          <span>
            Enviados
          </span>

          <strong>
            {statistics.sent}
          </strong>
        </article>

        <article>
          <Clock3
            size={18}
          />

          <span>
            Pendientes
          </span>

          <strong>
            {statistics.pending}
          </strong>
        </article>

        <article>
          <RotateCcw
            size={18}
          />

          <span>
            Reintento
          </span>

          <strong>
            {statistics.retry}
          </strong>
        </article>

        <article
          className={
            statistics.deadLetter >
              0
              ? 'helpdesk-mail-metric--danger'
              : ''
          }
        >
          <XCircle
            size={18}
          />

          <span>
            Dead letter
          </span>

          <strong>
            {statistics.deadLetter}
          </strong>
        </article>
      </section>

      <form
        onSubmit={
          event =>
            void save(
              event,
            )
        }
      >
        {/* ==================================================== */}
        {/* MAILBOX                                              */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <Mail
                size={20}
              />
            </span>

            <div>
              <h2>
                Buzón corporativo
              </h2>

              <p>
                Buzón físico consultado
                mediante Microsoft Graph
                y actor técnico utilizado
                por TitanMDM.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-grid"
          >
            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Dirección del buzón
              </span>

              <input
                type="email"
                maxLength={320}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings.mailbox
                  ??
                  ''
                }
                placeholder="mesadeayuda@empresa.com"
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        mailbox:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />

              <small>
                Buzón físico que Microsoft
                Graph consulta.
              </small>
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Usuario técnico del buzón
              </span>

              {canManage ? (
                <select
                  disabled={
                    saving
                  }
                  value={
                    settings.actorUserId
                    ??
                    ''
                  }
                  onChange={
                    event =>
                      setSettings(
                        {
                          ...settings,

                          actorUserId:
                            event
                              .target
                              .value
                            ||
                            null,
                        },
                      )
                  }
                >
                  <option value="">
                    Seleccionar usuario
                  </option>

                  {actors.map(
                    actor => (
                      <option
                        key={
                          actor.id
                        }
                        value={
                          actor.id
                        }
                      >
                        {actor.name}
                        {
                          actor.email
                            ? ` · ${actor.email}`
                            : ''
                        }
                      </option>
                    ),
                  )}
                </select>
              ) : (
                <div
                  className="helpdesk-mail-readonly"
                >
                  {
                    settings.actorName
                    ||
                    'No configurado'
                  }
                </div>
              )}

              <small>
                Actor interno usado por
                los eventos automáticos
                creados desde correo.
              </small>
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--full"
            >
              <span>
                Destinatarios autorizados
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .acceptedRecipients
                  ??
                  ''
                }
                placeholder={
                  'mesadeayuda@empresa.com\nsoporte@empresa.com'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        acceptedRecipients:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />

              <small>
                Solo los mensajes cuyo
                To/Cc contenga una de
                estas direcciones pueden
                convertirse en ticket.
                Puedes separar valores
                por coma, punto y coma o
                una línea nueva.
              </small>
            </label>
          </div>
        </section>

        {/* ==================================================== */}
        {/* ENTRY / EXIT                                         */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <MailCheck
                size={20}
              />
            </span>

            <div>
              <h2>
                Entrada y salida
              </h2>

              <p>
                Activa de forma
                independiente los workers
                de recepción y envío.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-switches"
          >
            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .inboundEnabled
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        inboundEnabled:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <span
                className="helpdesk-mail-switch"
              />

              <div>
                <strong>
                  Correo entrante
                </strong>

                <small>
                  Crear tickets y
                  registrar respuestas.
                </small>
              </div>
            </label>

            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .outboundEnabled
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        outboundEnabled:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <span
                className="helpdesk-mail-switch"
              />

              <div>
                <strong>
                  Correo saliente
                </strong>

                <small>
                  Enviar respuestas
                  públicas del equipo TIC.
                </small>
              </div>
            </label>
          </div>

          <div
            className="helpdesk-mail-grid"
          >
            <label
              className="helpdesk-mail-field"
            >
              <span>
                Polling entrada
              </span>

              <div
                className="helpdesk-mail-input-suffix"
              >
                <input
                  type="number"
                  min={30}
                  max={3600}
                  disabled={
                    !canManage
                    ||
                    saving
                  }
                  value={
                    settings
                      .inboundPollSeconds
                  }
                  onChange={
                    event =>
                      setSettings(
                        {
                          ...settings,

                          inboundPollSeconds:
                            Number(
                              event
                                .target
                                .value,
                            ),
                        },
                      )
                  }
                />

                <span>
                  s
                </span>
              </div>
            </label>

            <label
              className="helpdesk-mail-field"
            >
              <span>
                Polling salida
              </span>

              <div
                className="helpdesk-mail-input-suffix"
              >
                <input
                  type="number"
                  min={10}
                  max={3600}
                  disabled={
                    !canManage
                    ||
                    saving
                  }
                  value={
                    settings
                      .outboundPollSeconds
                  }
                  onChange={
                    event =>
                      setSettings(
                        {
                          ...settings,

                          outboundPollSeconds:
                            Number(
                              event
                                .target
                                .value,
                            ),
                        },
                      )
                  }
                />

                <span>
                  s
                </span>
              </div>
            </label>

            <label
              className="helpdesk-mail-field"
            >
              <span>
                Tamaño de lote
              </span>

              <input
                type="number"
                min={1}
                max={100}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings.batchSize
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        batchSize:
                          Number(
                            event
                              .target
                              .value,
                          ),
                      },
                    )
                }
              />
            </label>

            <label
              className="helpdesk-mail-field"
            >
              <span>
                Intentos máximos
              </span>

              <input
                type="number"
                min={1}
                max={20}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings.maxAttempts
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        maxAttempts:
                          Number(
                            event
                              .target
                              .value,
                          ),
                      },
                    )
                }
              />
            </label>
          </div>
        </section>

        {/* ==================================================== */}
        {/* ENTERPRISE FILTERS                                   */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <Filter
                size={20}
              />
            </span>

            <div>
              <h2>
                Política de correo entrante
              </h2>

              <p>
                Evita newsletters,
                respuestas automáticas,
                rebotes y remitentes no
                autorizados.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-policy-switches"
          >
            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .ignoreAutomaticMessages
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        ignoreAutomaticMessages:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <ShieldCheck
                size={18}
              />

              <div>
                <strong>
                  Respuestas automáticas
                </strong>

                <small>
                  Ignora mensajes con
                  Auto-Submitted.
                </small>
              </div>
            </label>

            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .ignoreBulkMessages
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        ignoreBulkMessages:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <ShieldCheck
                size={18}
              />

              <div>
                <strong>
                  Correo masivo
                </strong>

                <small>
                  Ignora bulk, list y
                  junk.
                </small>
              </div>
            </label>

            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .ignoreBounceMessages
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        ignoreBounceMessages:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <ShieldCheck
                size={18}
              />

              <div>
                <strong>
                  Rebotes / NDR
                </strong>

                <small>
                  Ignora delivery failure
                  y correo no entregado.
                </small>
              </div>
            </label>

            <label>
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  settings
                    .ignoreNoReplyMessages
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        ignoreNoReplyMessages:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <ShieldCheck
                size={18}
              />

              <div>
                <strong>
                  No-reply
                </strong>

                <small>
                  Ignora noreply,
                  postmaster y similares.
                </small>
              </div>
            </label>
          </div>

          <div
            className="helpdesk-mail-grid"
          >
            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Remitentes bloqueados
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .blockedSenders
                  ??
                  ''
                }
                placeholder={
                  'spam@dominio.com\nmarketing@dominio.com'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        blockedSenders:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Dominios bloqueados
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .blockedDomains
                  ??
                  ''
                }
                placeholder={
                  'publicidad.com\nspam.example'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        blockedDomains:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Remitentes permitidos
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .allowedSenders
                  ??
                  ''
                }
                placeholder={
                  'usuario@empresa.com'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        allowedSenders:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />

              <small>
                Si configuras una
                allow-list, los demás
                remitentes quedarán
                excluidos salvo dominios
                permitidos.
              </small>
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--wide"
            >
              <span>
                Dominios permitidos
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .allowedDomains
                  ??
                  ''
                }
                placeholder={
                  'cesariglesias.com.do'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        allowedDomains:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />
            </label>

            <label
              className="helpdesk-mail-field helpdesk-mail-field--full"
            >
              <span>
                Patrones ignorados en el
                asunto
              </span>

              <textarea
                rows={3}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  settings
                    .ignoredSubjectPatterns
                  ??
                  ''
                }
                placeholder={
                  'newsletter\npromoción\nnotificación automática'
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        ignoredSubjectPatterns:
                          event
                            .target
                            .value,
                      },
                    )
                }
              />

              <small>
                Coincidencia parcial,
                sin distinguir
                mayúsculas/minúsculas.
              </small>
            </label>
          </div>
        </section>

        {/* ==================================================== */}
        {/* RUNTIME                                              */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <ServerCog
                size={20}
              />
            </span>

            <div>
              <h2>
                Estado operativo
              </h2>

              <p>
                Últimos ciclos ejecutados
                por los workers.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-runtime"
          >
            <article>
              <div
                className="helpdesk-mail-runtime__title"
              >
                <Inbox
                  size={16}
                />

                <strong>
                  Entrada
                </strong>
              </div>

              <dl>
                <div>
                  <dt>
                    Último intento
                  </dt>

                  <dd>
                    {formatDate(
                      settings
                        .lastInboundAttemptAtUtc,
                    )}
                  </dd>
                </div>

                <div>
                  <dt>
                    Último éxito
                  </dt>

                  <dd>
                    {formatDate(
                      settings
                        .lastInboundSuccessAtUtc,
                    )}
                  </dd>
                </div>
              </dl>

              {
                settings
                  .lastInboundError
                  ? (
                    <div
                      className="helpdesk-mail-runtime__error"
                    >
                      <AlertTriangle
                        size={14}
                      />

                      {
                        settings
                          .lastInboundError
                      }
                    </div>
                  )
                  : (
                    <div
                      className="helpdesk-mail-runtime__ok"
                    >
                      <CheckCircle2
                        size={14}
                      />

                      Sin error registrado.
                    </div>
                  )
              }
            </article>

            <article>
              <div
                className="helpdesk-mail-runtime__title"
              >
                <Send
                  size={16}
                />

                <strong>
                  Salida
                </strong>
              </div>

              <dl>
                <div>
                  <dt>
                    Último intento
                  </dt>

                  <dd>
                    {formatDate(
                      settings
                        .lastOutboundAttemptAtUtc,
                    )}
                  </dd>
                </div>

                <div>
                  <dt>
                    Último éxito
                  </dt>

                  <dd>
                    {formatDate(
                      settings
                        .lastOutboundSuccessAtUtc,
                    )}
                  </dd>
                </div>
              </dl>

              {
                settings
                  .lastOutboundError
                  ? (
                    <div
                      className="helpdesk-mail-runtime__error"
                    >
                      <AlertTriangle
                        size={14}
                      />

                      {
                        settings
                          .lastOutboundError
                      }
                    </div>
                  )
                  : (
                    <div
                      className="helpdesk-mail-runtime__ok"
                    >
                      <CheckCircle2
                        size={14}
                      />

                      Sin error registrado.
                    </div>
                  )
              }
            </article>
          </div>
        </section>

        {/* ==================================================== */}
        {/* GRAPH                                                */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <ShieldCheck
                size={20}
              />
            </span>

            <div>
              <h2>
                Diagnóstico Microsoft Graph
              </h2>

              <p>
                Valida credenciales,
                permisos y acceso real
                al Inbox.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-diagnostic"
          >
            <button
              type="button"
              disabled={
                !canManage
                ||
                testing
                ||
                saving
              }
              onClick={
                () =>
                  void testMailbox()
              }
            >
              {
                testing
                  ? (
                    <RefreshCw
                      size={16}
                    />
                  )
                  : (
                    <ShieldCheck
                      size={16}
                    />
                  )
              }

              {
                testing
                  ? 'Probando…'
                  : 'Probar conexión'
              }
            </button>

            <span>
              Verifica Microsoft Entra,
              Microsoft Graph y el buzón.
            </span>
          </div>

          {diagnostic && (
            <div
              className={
                diagnostic.success
                  ? 'helpdesk-mail-test helpdesk-mail-test--success'
                  : 'helpdesk-mail-test helpdesk-mail-test--error'
              }
            >
              {
                diagnostic.success
                  ? (
                    <CheckCircle2
                      size={19}
                    />
                  )
                  : (
                    <AlertTriangle
                      size={19}
                    />
                  )
              }

              <div>
                <strong>
                  {
                    diagnostic
                      .message
                  }
                </strong>

                {diagnostic.mailbox && (
                  <span>
                    Buzón:
                    {' '}
                    {
                      diagnostic
                        .mailbox
                    }
                  </span>
                )}

                {
                  diagnostic
                    .acceptedRecipients
                    && (
                      <span>
                        Destinatarios:
                        {' '}
                        {
                          diagnostic
                            .acceptedRecipients
                        }
                      </span>
                    )
                }

                {diagnostic.inbox && (
                  <span>
                    Carpeta:
                    {' '}
                    {
                      diagnostic
                        .inbox
                    }
                  </span>
                )}

                <span>
                  Mensajes:
                  {' '}
                  {
                    diagnostic
                      .totalItemCount
                    ??
                    0
                  }
                  {' · '}
                  No leídos:
                  {' '}
                  {
                    diagnostic
                      .unreadItemCount
                    ??
                    0
                  }
                </span>
              </div>
            </div>
          )}
        </section>

        {/* ==================================================== */}
        {/* ACTIVITY                                             */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header helpdesk-mail-card__header--actions"
          >
            <span>
              <ShieldX
                size={20}
              />
            </span>

            <div>
              <h2>
                Actividad de correo
              </h2>

              <p>
                Auditoría de mensajes
                aceptados, ignorados y
                fallidos.
              </p>
            </div>

            <button
              type="button"
              className="helpdesk-mail-inline-action"
              disabled={
                activityLoading
              }
              onClick={
                () =>
                  void loadActivity()
              }
            >
              <RefreshCw
                size={14}
              />

              Actualizar
            </button>
          </header>

          {
            activityLoading
            &&
            activity.length === 0
              ? (
                <div
                  className="helpdesk-mail-empty"
                >
                  Cargando actividad…
                </div>
              )
              : activity.length === 0
                ? (
                  <div
                    className="helpdesk-mail-empty"
                  >
                    <MailCheck
                      size={24}
                    />

                    <strong>
                      Aún no hay mensajes procesados
                    </strong>

                    <span>
                      Los próximos correos
                      aceptados o rechazados
                      aparecerán aquí.
                    </span>
                  </div>
                )
                : (
                  <div
                    className="helpdesk-mail-table-wrap"
                  >
                    <table
                      className="helpdesk-mail-table"
                    >
                      <thead>
                        <tr>
                          <th>
                            Estado
                          </th>

                          <th>
                            Remitente
                          </th>

                          <th>
                            Asunto
                          </th>

                          <th>
                            Motivo
                          </th>

                          <th>
                            Ticket
                          </th>

                          <th>
                            Procesado
                          </th>
                        </tr>
                      </thead>

                      <tbody>
                        {
                          activity.map(
                            item => (
                              <tr
                                key={
                                  item.id
                                }
                              >
                                <td>
                                  <span
                                    className={
                                      decisionClass(
                                        item.decision,
                                      )
                                    }
                                  >
                                    {
                                      decisionLabel(
                                        item.decision,
                                      )
                                    }
                                  </span>
                                </td>

                                <td>
                                  <strong
                                    className="helpdesk-mail-table__sender"
                                  >
                                    {
                                      item
                                        .fromEmail
                                    }
                                  </strong>
                                </td>

                                <td>
                                  {
                                    item.subject
                                    ||
                                    '(Sin asunto)'
                                  }
                                </td>

                                <td>
                                  <span
                                    className="helpdesk-mail-reason"
                                    title={
                                      item.reason
                                    }
                                  >
                                    {
                                      item.reason
                                    }
                                  </span>

                                  <small
                                    className="helpdesk-mail-reason-code"
                                  >
                                    {
                                      item
                                        .reasonCode
                                    }
                                  </small>
                                </td>

                                <td>
                                  {
                                    item.ticketId
                                      ? (
                                        <span
                                          className="helpdesk-mail-ticket-link"
                                        >
                                          Vinculado
                                        </span>
                                      )
                                      : '—'
                                  }
                                </td>

                                <td>
                                  {
                                    formatDate(
                                      item
                                        .processedAtUtc,
                                    )
                                  }
                                </td>
                              </tr>
                            ),
                          )
                        }
                      </tbody>
                    </table>
                  </div>
                )
          }
        </section>

        {/* ==================================================== */}
        {/* CONFIG SUMMARY                                       */}
        {/* ==================================================== */}

        <section
          className="helpdesk-mail-card"
        >
          <header
            className="helpdesk-mail-card__header"
          >
            <span>
              <UserCog
                size={20}
              />
            </span>

            <div>
              <h2>
                Configuración actual
              </h2>

              <p>
                Control de revisión,
                trazabilidad y estado.
              </p>
            </div>
          </header>

          <div
            className="helpdesk-mail-summary"
          >
            <article>
              <span>
                Revisión
              </span>

              <strong>
                {
                  settings.revision
                }
              </strong>
            </article>

            <article>
              <span>
                Actualizada
              </span>

              <strong>
                {formatDate(
                  settings
                    .updatedAtUtc,
                )}
              </strong>
            </article>

            <article>
              <span>
                Actor
              </span>

              <strong>
                {
                  selectedActor
                    ?.name
                  ??
                  settings.actorName
                  ??
                  'Sin configurar'
                }
              </strong>
            </article>

            <article>
              <span>
                Servicio
              </span>

              <strong>
                {
                  settings.inboundEnabled
                  ||
                  settings.outboundEnabled
                    ? 'Activo'
                    : 'Detenido'
                }
              </strong>
            </article>
          </div>
        </section>

        {/* ==================================================== */}
        {/* SAVE BAR                                             */}
        {/* ==================================================== */}

        {canManage && (
          <div
            className="helpdesk-mail-savebar"
          >
            <div>
              <Save
                size={18}
              />

              <span>
                Los workers aplicarán
                esta configuración desde
                SQL Server.
              </span>
            </div>

            <button
              type="submit"
              disabled={
                saving
                ||
                testing
              }
            >
              <Save
                size={16}
              />

              {
                saving
                  ? 'Guardando…'
                  : 'Guardar configuración'
              }
            </button>
          </div>
        )}
      </form>
    </main>
  )
}