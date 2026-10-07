import {
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from 'react'

import {
  Link,
  useLocation,
} from 'react-router-dom'

import axios
  from 'axios'

import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import {
  AlertTriangle,
  Bot,
  CheckCircle2,
  Clock3,
  PauseCircle,
  RefreshCw,
  Save,
  ShieldCheck,
  TimerReset,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
} from '../../auth/helpdeskAccess'

import {
  HelpdeskRequestTemplates,
} from './HelpdeskRequestTemplates'

import './HelpdeskCenterPage.css'

interface Settings {
  classificationEnabled: boolean

  escalationDelayMinutes: number

  reopenDays: number

  lowFirstResponseMinutes: number
  lowResolutionMinutes: number

  mediumFirstResponseMinutes: number
  mediumResolutionMinutes: number

  highFirstResponseMinutes: number
  highResolutionMinutes: number

  criticalFirstResponseMinutes: number
  criticalResolutionMinutes: number

  pauseSlaWhenWaitingUser: boolean

  revision: number

  updatedAtUtc?: string | null

  modelConfigured: boolean
}

interface Count {
  label: string
  count: number
}

interface Analytics {
  total: number
  resolved: number
  active: number
  paused: number
  unassigned: number
  firstOverdue: number
  resolutionOverdue: number

  averageFirstResponseHours:
    number | null

  averageResolutionHours:
    number | null

  escalations24h: number

  byStatus: Count[]

  byCategory: Count[]

  byZone: Count[]

  byAgent: Count[]

  daily: {
    date: string
    count: number
  }[]

  alerts: {
    id: string
    ticketId: string
    number: string
    eventType: string
    summary: string
    createdAtUtc: string
  }[]
}

interface Readiness {
  zones: number
  groups: number
  availableMemberships: number
  activeSchedules: number
  activeTemplates: number
  unlocatedUsers: number
  assistantUsers: number
}

interface SlaRow {
  key:
    | 'low'
    | 'medium'
    | 'high'
    | 'critical'

  label: string

  description: string

  firstResponse:
    keyof Settings

  resolution:
    keyof Settings
}

function message(
  exception: unknown,
) {
  return (
    axios.isAxiosError(
      exception,
    )
    &&
    typeof exception
      .response
      ?.data
      ?.message ===
      'string'
  )
    ? exception
        .response
        ?.data
        ?.message
    : 'No se pudo completar la operación.'
}

function formatMinutes(
  value: number,
) {
  if (
    !Number.isFinite(
      value,
    )
    ||
    value <= 0
  ) {
    return '—'
  }

  if (
    value < 60
  ) {
    return `${value} min`
  }

  if (
    value % 1440 ===
    0
  ) {
    const days =
      value / 1440

    return days === 1
      ? '1 día'
      : `${days} días`
  }

  if (
    value % 60 ===
    0
  ) {
    const hours =
      value / 60

    return hours === 1
      ? '1 hora'
      : `${hours} horas`
  }

  const hours =
    Math.floor(
      value / 60,
    )

  const minutes =
    value % 60

  return `${hours} h ${minutes} min`
}

function formatDate(
  value?: string | null,
) {
  if (
    !value
  ) {
    return 'Sin cambios registrados'
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
    return 'Sin cambios registrados'
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

export function HelpdeskCenterPage() {
  const location =
    useLocation()

  const section =
    location.pathname
      .split('/')
      .filter(Boolean)
      .at(-1)
    ??
    'configuracion'

  const {
    hasPermission,
  } =
    useAuth()

  const manager =
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .slaManage,
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
    useState<Settings | null>(
      null,
    )

  const [
    data,
    setData,
  ] =
    useState<Analytics | null>(
      null,
    )

  const [
    readiness,
    setReadiness,
  ] =
    useState<Readiness | null>(
      null,
    )

  const [
    from,
    setFrom,
  ] =
    useState(
      '',
    )

  const [
    to,
    setTo,
  ] =
    useState(
      '',
    )

  const [
    filters,
    setFilters,
  ] =
    useState(
      {
        from:
          '',

        to:
          '',
      },
    )

  const [
    version,
    setVersion,
  ] =
    useState(
      0,
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
    success,
    setSuccess,
  ] =
    useState(
      '',
    )

  const slaRows:
    SlaRow[] =
      useMemo(
        () => [
          {
            key:
              'low',

            label:
              'Baja',

            description:
              'Solicitudes sin impacto inmediato en la operación.',

            firstResponse:
              'lowFirstResponseMinutes',

            resolution:
              'lowResolutionMinutes',
          },

          {
            key:
              'medium',

            label:
              'Media',

            description:
              'Incidentes operativos normales y solicitudes estándar.',

            firstResponse:
              'mediumFirstResponseMinutes',

            resolution:
              'mediumResolutionMinutes',
          },

          {
            key:
              'high',

            label:
              'Alta',

            description:
              'Incidentes con impacto importante que requieren atención prioritaria.',

            firstResponse:
              'highFirstResponseMinutes',

            resolution:
              'highResolutionMinutes',
          },

          {
            key:
              'critical',

            label:
              'Crítica / Urgente',

            description:
              'Interrupciones críticas o eventos que comprometen la operación.',

            firstResponse:
              'criticalFirstResponseMinutes',

            resolution:
              'criticalResolutionMinutes',
          },
        ],
        [],
      )

  useEffect(
    () => {
      const controller =
        new AbortController()

      setLoading(
        true,
      )

      setError(
        '',
      )

      setSuccess(
        '',
      )

      setData(
        null,
      )

      setReadiness(
        null,
      )

      setSettings(
        null,
      )

      const endpoint =
        section ===
          'configuracion'
          ? 'settings'
          : section ===
              'preparacion'
            ? 'readiness'
            : 'analytics'

      void apiClient
        .get(
          `/helpdesk/closure/${endpoint}`,
          {
            signal:
              controller.signal,

            params: {
              ...(
                filters.from
                  ? {
                      from:
                        filters.from,
                    }
                  : {}
              ),

              ...(
                filters.to
                  ? {
                      to:
                        filters.to,
                    }
                  : {}
              ),
            },
          },
        )
        .then(
          result => {
            if (
              controller.signal
                .aborted
            ) {
              return
            }

            if (
              endpoint ===
              'settings'
            ) {
              setSettings(
                result.data,
              )
            }
            else if (
              endpoint ===
              'readiness'
            ) {
              setReadiness(
                result.data,
              )
            }
            else {
              setData(
                result.data,
              )
            }
          },
        )
        .catch(
          exception => {
            if (
              !controller.signal
                .aborted
            ) {
              setError(
                message(
                  exception,
                ),
              )
            }
          },
        )
        .finally(
          () => {
            if (
              !controller.signal
                .aborted
            ) {
              setLoading(
                false,
              )
            }
          },
        )

      return () =>
        controller.abort()
    },
    [
      section,
      filters,
      version,
    ],
  )

  function updateNumber(
    key: keyof Settings,
    value: number,
  ) {
    if (
      !settings
    ) {
      return
    }

    setSettings(
      {
        ...settings,

        [key]:
          value,
      },
    )
  }

  function validateSettings() {
    if (
      !settings
    ) {
      return 'No existe configuración para validar.'
    }

    if (
      settings
        .escalationDelayMinutes <
        15
      ||
      settings
        .escalationDelayMinutes >
        1440
    ) {
      return (
        'El escalamiento debe estar ' +
        'entre 15 y 1440 minutos.'
      )
    }

    if (
      settings.reopenDays <
        1
      ||
      settings.reopenDays >
        30
    ) {
      return (
        'La reapertura debe estar ' +
        'entre 1 y 30 días.'
      )
    }

    for (
      const row
      of slaRows
    ) {
      const first =
        Number(
          settings[
            row.firstResponse
          ],
        )

      const resolution =
        Number(
          settings[
            row.resolution
          ],
        )

      if (
        first < 1
        ||
        first > 43200
        ||
        resolution < 1
        ||
        resolution > 43200
      ) {
        return (
          `Los tiempos SLA de prioridad ${row.label} ` +
          'deben estar entre 1 y 43200 minutos.'
        )
      }

      if (
        resolution <=
        first
      ) {
        return (
          `El tiempo de resolución para prioridad ${row.label} ` +
          'debe ser mayor que el tiempo de primera respuesta.'
        )
      }
    }

    return ''
  }

  async function save(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !settings
      ||
      saving
    ) {
      return
    }

    const validation =
      validateSettings()

    if (
      validation
    ) {
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
      const result =
        await apiClient
          .put<Settings>(
            '/helpdesk/closure/settings',
            settings,
          )

      setSettings(
        result.data,
      )

      setSuccess(
        'Configuración SLA guardada correctamente.',
      )
    }
    catch (
      exception
    ) {
      setError(
        message(
          exception,
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  const statusNames:
    Record<string, string> = {
      new:
        'Recibido',

      open:
        'Abierto',

      inprogress:
        'En proceso',

      pendinguser:
        'Espera del usuario',

      resolved:
        'Resuelto',

      closed:
        'Cerrado',
    }

  const statusData =
    data
      ?.byStatus
      .map(
        item => ({
          ...item,

          label:
            statusNames[
              item.label
            ]
            ??
            item.label,
        }),
      )
    ??
    []

  const metrics =
    data
      ? [
          [
            'Tickets',
            data.total,
          ],

          [
            'Activos',
            data.active,
          ],

          [
            'Resueltos o cerrados',
            data.resolved,
          ],

          [
            'En espera del usuario',
            data.paused,
          ],

          [
            'Sin técnico',
            data.unassigned,
          ],

          [
            'Primera respuesta vencida',
            data.firstOverdue,
          ],

          [
            'Resolución vencida',
            data.resolutionOverdue,
          ],

          [
            'Primera respuesta media (h)',
            data
              .averageFirstResponseHours
              ?.toFixed(
                2,
              )
            ??
            '—',
          ],

          [
            'Resolución media (h)',
            data
              .averageResolutionHours
              ?.toFixed(
                2,
              )
            ??
            '—',
          ],

          [
            'Escalamientos internos · últimas 24 h',
            data
              .escalations24h,
          ],
        ]
      : []

  return (
    <main
      className="titan-page hdc"
    >
      <header
        className="hdc-header"
      >
        <div>
          <span
            className="hdc-eyebrow"
          >
            HELPDESK ENTERPRISE
          </span>

          <h1>
            Centro de operación de Helpdesk
          </h1>

          <p>
            Configuración, SLA,
            preparación y métricas
            operativas de la Mesa de Ayuda.
          </p>
        </div>

        <button
          type="button"
          className="hdc-refresh"
          disabled={
            loading
            ||
            saving
          }
          onClick={
            () =>
              setVersion(
                value =>
                  value + 1,
              )
          }
        >
          <RefreshCw
            size={16}
          />

          Actualizar
        </button>
      </header>

      <nav
        className="hdc-nav"
        aria-label="Centro de Helpdesk"
      >
        <Link
          to="/helpdesk/centro/alertas?workspace=helpdesk"
        >
          Automatización
        </Link>

        <Link
          to="/helpdesk/centro/kpis?workspace=helpdesk"
        >
          KPI
        </Link>
        
        {manager && (
          <>
            <Link
              to="/helpdesk/centro/configuracion?workspace=helpdesk"
            >
              Configuración
            </Link>

            <Link
              to="/helpdesk/centro/preparacion?workspace=helpdesk"
            >
              Preparación
            </Link>
          </>
        )}

        <Link
          to="/helpdesk/seguimiento?workspace=helpdesk"
        >
          Seguimiento SLA
        </Link>
      </nav>

      {error && (
        <div
          className="hdc-message hdc-message--error"
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
          className="hdc-message hdc-message--success"
          role="status"
        >
          <CheckCircle2
            size={18}
          />

          {success}
        </div>
      )}

      {loading && (
        <div
          className="hdc-loading"
          role="status"
        >
          Cargando…
        </div>
      )}

      {section ===
        'configuracion'
        &&
        settings
        &&
        manager && (
        <form
          className="hdc-settings-form"
          onSubmit={
            event =>
              void save(
                event,
              )
          }
        >
          <section
            className="hdc-card"
          >
            <div
              className="hdc-card__header"
            >
              <div
                className="hdc-card__icon"
              >
                <Clock3
                  size={20}
                />
              </div>

              <div>
                <h2>
                  Acuerdos de nivel
                  de servicio
                </h2>

                <p>
                  Define los tiempos
                  máximos de primera
                  respuesta y resolución
                  según la prioridad.
                </p>
              </div>
            </div>

            <div
              className="hdc-sla-table"
            >
              <div
                className="hdc-sla-table__head"
              >
                <span>
                  Prioridad
                </span>

                <span>
                  Primera respuesta
                </span>

                <span>
                  Resolución
                </span>

                <span>
                  Resumen
                </span>
              </div>

              {slaRows.map(
                row => {
                  const first =
                    Number(
                      settings[
                        row.firstResponse
                      ],
                    )

                  const resolution =
                    Number(
                      settings[
                        row.resolution
                      ],
                    )

                  return (
                    <div
                      key={
                        row.key
                      }
                      className={
                        `hdc-sla-row ` +
                        `hdc-sla-row--${row.key}`
                      }
                    >
                      <div
                        className="hdc-sla-row__priority"
                      >
                        <strong>
                          {
                            row.label
                          }
                        </strong>

                        <small>
                          {
                            row.description
                          }
                        </small>
                      </div>

                      <label>
                        <span>
                          Minutos
                        </span>

                        <input
                          type="number"
                          min={1}
                          max={43200}
                          required
                          disabled={
                            saving
                          }
                          value={
                            first
                          }
                          onChange={
                            event =>
                              updateNumber(
                                row.firstResponse,
                                Number(
                                  event.target.value,
                                ),
                              )
                          }
                        />

                        <small>
                          {
                            formatMinutes(
                              first,
                            )
                          }
                        </small>
                      </label>

                      <label>
                        <span>
                          Minutos
                        </span>

                        <input
                          type="number"
                          min={1}
                          max={43200}
                          required
                          disabled={
                            saving
                          }
                          value={
                            resolution
                          }
                          onChange={
                            event =>
                              updateNumber(
                                row.resolution,
                                Number(
                                  event.target.value,
                                ),
                              )
                          }
                        />

                        <small>
                          {
                            formatMinutes(
                              resolution,
                            )
                          }
                        </small>
                      </label>

                      <div
                        className="hdc-sla-row__summary"
                      >
                        <Clock3
                          size={15}
                        />

                        <span>
                          Respuesta:
                          {' '}
                          <strong>
                            {
                              formatMinutes(
                                first,
                              )
                            }
                          </strong>

                          <br />

                          Resolución:
                          {' '}
                          <strong>
                            {
                              formatMinutes(
                                resolution,
                              )
                            }
                          </strong>
                        </span>
                      </div>
                    </div>
                  )
                },
              )}
            </div>

            <div
              className="hdc-info-box"
            >
              <ShieldCheck
                size={19}
              />

              <div>
                <strong>
                  Política aplicada
                  a tickets nuevos
                </strong>

                <span>
                  Los deadlines se
                  calculan al crear
                  cada ticket. Los
                  tickets existentes
                  conservan sus fechas
                  actuales para mantener
                  trazabilidad histórica.
                </span>
              </div>
            </div>
          </section>

          <section
            className="hdc-card"
          >
            <div
              className="hdc-card__header"
            >
              <div
                className="hdc-card__icon"
              >
                <PauseCircle
                  size={20}
                />
              </div>

              <div>
                <h2>
                  Pausa y ciclo
                  de vida del SLA
                </h2>

                <p>
                  Controla el comportamiento
                  cuando el caso depende
                  del solicitante.
                </p>
              </div>
            </div>

            <label
              className="hdc-switch"
            >
              <input
                type="checkbox"
                checked={
                  settings
                    .pauseSlaWhenWaitingUser
                }
                disabled={
                  saving
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        pauseSlaWhenWaitingUser:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <span
                className="hdc-switch__control"
                aria-hidden="true"
              />

              <span>
                <strong>
                  Pausar SLA cuando
                  el ticket espera
                  respuesta del usuario
                </strong>

                <small>
                  Estado
                  {' '}
                  <code>
                    pendinguser
                  </code>
                  {' '}
                  suspende el contador
                  hasta recibir una
                  respuesta.
                </small>
              </span>
            </label>

            <div
              className="hdc-settings-grid"
            >
              <label>
                <span>
                  Días disponibles
                  para reapertura
                </span>

                <input
                  type="number"
                  min={1}
                  max={30}
                  required
                  disabled={
                    saving
                  }
                  value={
                    settings
                      .reopenDays
                  }
                  onChange={
                    event =>
                      setSettings(
                        {
                          ...settings,

                          reopenDays:
                            Number(
                              event
                                .target
                                .value,
                            ),
                        },
                      )
                  }
                />

                <small>
                  El solicitante podrá
                  reabrir un ticket
                  resuelto/cerrado durante
                  este período.
                </small>
              </label>

              <label>
                <span>
                  Escalamiento después
                  del vencimiento
                </span>

                <div
                  className="hdc-input-suffix"
                >
                  <input
                    type="number"
                    min={15}
                    max={1440}
                    required
                    disabled={
                      saving
                    }
                    value={
                      settings
                        .escalationDelayMinutes
                    }
                    onChange={
                      event =>
                        setSettings(
                          {
                            ...settings,

                            escalationDelayMinutes:
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
                    min
                  </span>
                </div>

                <small>
                  {
                    formatMinutes(
                      settings
                        .escalationDelayMinutes,
                    )
                  }
                  {' '}
                  después de superar
                  el SLA.
                </small>
              </label>
            </div>
          </section>

          <section
            className="hdc-card"
          >
            <div
              className="hdc-card__header"
            >
              <div
                className="hdc-card__icon"
              >
                <Bot
                  size={20}
                />
              </div>

              <div>
                <h2>
                  Automatización
                  asistida
                </h2>

                <p>
                  Configuración actual
                  de clasificación
                  automática.
                </p>
              </div>
            </div>

            <label
              className="hdc-switch"
            >
              <input
                type="checkbox"
                checked={
                  settings
                    .classificationEnabled
                }
                disabled={
                  saving
                  ||
                  (
                    !settings
                      .modelConfigured
                    &&
                    !settings
                      .classificationEnabled
                  )
                }
                onChange={
                  event =>
                    setSettings(
                      {
                        ...settings,

                        classificationEnabled:
                          event
                            .target
                            .checked,
                      },
                    )
                }
              />

              <span
                className="hdc-switch__control"
                aria-hidden="true"
              />

              <span>
                <strong>
                  Clasificar automáticamente
                  solicitudes generales
                </strong>

                <small>
                  Requiere el modelo Titan
                  configurado en el servidor.
                </small>
              </span>
            </label>

            {!settings
              .modelConfigured && (
              <div
                className="hdc-warning-box"
              >
                <AlertTriangle
                  size={18}
                />

                <div>
                  <strong>
                    Modelo no configurado
                  </strong>

                  <span>
                    La clasificación
                    automática permanecerá
                    disponible para activarla
                    después de configurar
                    Ollama/Titan.
                  </span>
                </div>
              </div>
            )}
          </section>

          <section
            className="hdc-card"
          >
            <div
              className="hdc-card__header"
            >
              <div
                className="hdc-card__icon"
              >
                <TimerReset
                  size={20}
                />
              </div>

              <div>
                <h2>
                  Estado de configuración
                </h2>

                <p>
                  Información de concurrencia
                  y versión de esta política.
                </p>
              </div>
            </div>

            <div
              className="hdc-settings-summary"
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
                  Última actualización
                </span>

                <strong>
                  {
                    formatDate(
                      settings
                        .updatedAtUtc,
                    )
                  }
                </strong>
              </article>

              <article>
                <span>
                  Espera de usuario
                </span>

                <strong>
                  {
                    settings
                      .pauseSlaWhenWaitingUser
                      ? 'Pausa SLA'
                      : 'No pausa'
                  }
                </strong>
              </article>

              <article>
                <span>
                  Reapertura
                </span>

                <strong>
                  {
                    settings
                      .reopenDays
                  } días
                </strong>
              </article>
            </div>
          </section>

          <div
            className="hdc-save-bar"
          >
            <div>
              <Save
                size={18}
              />

              <span>
                Los cambios se aplicarán
                a la configuración de
                Helpdesk de esta organización.
              </span>
            </div>

            <button
              type="submit"
              disabled={
                saving
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

          <section
            className="hdc-card"
          >
            <h2>
              Administración de la mesa
            </h2>

            <div
              className="hdc-nav"
            >
              <Link
                to="/helpdesk/operations?workspace=helpdesk"
              >
                Localidades,
                técnicos y usuarios
              </Link>

              <Link
                to="/helpdesk/especialidades?workspace=helpdesk"
              >
                Grupos, categorías,
                cobertura y turnos
              </Link>

              <Link
                to="/helpdesk/cobertura?workspace=helpdesk"
              >
                Validación de cobertura
              </Link>

              <Link
                to="/roles?workspace=administration"
              >
                Roles y permisos
              </Link>
            </div>
          </section>

          <section
            className="hdc-card"
          >
            <h2>
              Catálogo de plantillas
            </h2>

            <HelpdeskRequestTemplates
              onApply={
                () =>
                  setSuccess(
                    'Vista previa aplicada. Para crear el ticket utiliza Mis solicitudes.',
                  )
              }
            />
          </section>
        </form>
      )}

      {section ===
        'preparacion'
        &&
        readiness && (
        <section
          className="hdc-card"
        >
          <h2>
            Indicadores
            de configuración
          </h2>

          <div
            className="hdc-metrics"
          >
            {
              Object
                .entries(
                  readiness,
                )
                .map(
                  (
                    [
                      key,
                      value,
                    ],
                  ) => (
                    <article
                      key={
                        key
                      }
                    >
                      <span>
                        {
                          {
                            zones:
                              'Zonas activas',

                            groups:
                              'Grupos activos',

                            availableMemberships:
                              'Membresías disponibles',

                            activeSchedules:
                              'Turnos habilitados',

                            activeTemplates:
                              'Plantillas activas',

                            unlocatedUsers:
                              'Usuarios sin ubicación',

                            assistantUsers:
                              'Usuarios con Titan habilitado',
                          }[
                            key
                          ]
                          ??
                          key
                        }
                      </span>

                      <strong>
                        {
                          value
                        }
                      </strong>
                    </article>
                  ),
                )
            }
          </div>

          <p>
            Son indicadores,
            no una certificación
            funcional. Completa
            la ubicación de usuarios
            y valida cobertura antes
            de activar correo.
          </p>
        </section>
      )}

      {
        [
          'kpis',
          'graficos',
          'alertas',
        ]
          .includes(
            section,
          ) && (
          <>
            <form
              className="hdc-filters"
              onSubmit={
                event => {
                  event.preventDefault()

                  if (
                    from
                    &&
                    to
                    &&
                    from >
                      to
                  ) {
                    setError(
                      'El inicio debe ser anterior al fin.',
                    )

                    return
                  }

                  setFilters(
                    {
                      from,
                      to,
                    },
                  )
                }
              }
            >
              <label>
                Desde

                <input
                  type="date"
                  value={
                    from
                  }
                  onChange={
                    event =>
                      setFrom(
                        event
                          .target
                          .value,
                      )
                  }
                />
              </label>

              <label>
                Hasta

                <input
                  type="date"
                  value={
                    to
                  }
                  onChange={
                    event =>
                      setTo(
                        event
                          .target
                          .value,
                      )
                  }
                />
              </label>

              <button
                disabled={
                  loading
                }
              >
                Aplicar filtros
              </button>

              <button
                type="button"
                disabled={
                  loading
                }
                onClick={
                  () => {
                    setFrom(
                      '',
                    )

                    setTo(
                      '',
                    )

                    setFilters(
                      {
                        from:
                          '',

                        to:
                          '',
                      },
                    )
                  }
                }
              >
                Todo el histórico
              </button>
            </form>

            <p>
              Sin fechas se utiliza
              todo el histórico.
              Los filtros se aplican
              a la fecha de creación
              del ticket.
            </p>
          </>
        )
      }

      {section ===
        'alertas'
        &&
        data && (
        <section
          className="hdc-card"
        >
          <h2>
            Automatización ·
            últimas 24 horas
          </h2>

          <p>
            Escalamientos y
            clasificaciones son
            registros internos;
            no son correos enviados.
          </p>

          {!data
            .alerts
            .length && (
            <p>
              No hay registros
              para este alcance.
            </p>
          )}

          {
            data
              .alerts
              .map(
                item => (
                  <article
                    key={
                      item.id
                    }
                  >
                    <Link
                      to={
                        `/helpdesk/tickets/${item.ticketId}` +
                        '?workspace=helpdesk'
                      }
                    >
                      {
                        item.number
                      }
                    </Link>

                    <p>
                      {
                        item.summary
                      }
                    </p>

                    <small>
                      {
                        item.createdAtUtc
                      } UTC
                    </small>

                    <hr />
                  </article>
                ),
              )
          }
        </section>
      )}

      {section ===
        'kpis'
        &&
        data && (
        <div
          className="hdc-metrics"
        >
          {
            metrics.map(
              (
                [
                  label,
                  value,
                ],
              ) => (
                <article
                  key={
                    String(
                      label,
                    )
                  }
                >
                  <span>
                    {
                      label
                    }
                  </span>

                  <strong>
                    {
                      value
                    }
                  </strong>
                </article>
              ),
            )
          }
        </div>
      )}

      {section ===
        'graficos'
        &&
        data && (
        <div
          className="hdc-charts"
        >
          {
            [
              [
                'Por localidad actual del solicitante',
                data.byZone,
              ],

              [
                'Por técnico asignado',
                data.byAgent,
              ],
            ]
              .map(
                (
                  [
                    title,
                    rows,
                  ],
                ) => (
                  <section
                    className="hdc-card"
                    key={
                      String(
                        title,
                      )
                    }
                  >
                    <h2>
                      {
                        String(
                          title,
                        )
                      }
                    </h2>

                    <div
                      className="hdc-chart"
                    >
                      <ResponsiveContainer
                        width="100%"
                        height="100%"
                      >
                        <BarChart
                          data={
                            rows as Count[]
                          }
                          layout="vertical"
                        >
                          <CartesianGrid
                            strokeDasharray="3 3"
                          />

                          <XAxis
                            type="number"
                            allowDecimals={
                              false
                            }
                          />

                          <YAxis
                            type="category"
                            dataKey="label"
                            width={110}
                          />

                          <Tooltip />

                          <Bar
                            dataKey="count"
                            name="Tickets"
                            fill="#16a34a"
                          />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </section>
                ),
              )
          }

          <section
            className="hdc-card"
          >
            <h2>
              Tickets por estado
            </h2>

            {!data.total ? (
              <p>
                No hay tickets
                en este período.
              </p>
            ) : (
              <div
                className="hdc-chart"
              >
                <ResponsiveContainer
                  width="100%"
                  height="100%"
                >
                  <PieChart>
                    <Pie
                      data={
                        statusData
                      }
                      dataKey="count"
                      nameKey="label"
                      fill="#7656d6"
                      label
                    />

                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            )}
          </section>

          <section
            className="hdc-card"
          >
            <h2>
              Tickets por categoría
            </h2>

            <div
              className="hdc-chart"
            >
              <ResponsiveContainer
                width="100%"
                height="100%"
              >
                <BarChart
                  data={
                    data
                      .byCategory
                  }
                >
                  <CartesianGrid
                    strokeDasharray="3 3"
                  />

                  <XAxis
                    dataKey="label"
                  />

                  <YAxis
                    allowDecimals={
                      false
                    }
                  />

                  <Tooltip />

                  <Bar
                    dataKey="count"
                    name="Tickets"
                    fill="#7656d6"
                  />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </section>

          <section
            className="hdc-card"
          >
            <h2>
              Tickets por día · UTC
            </h2>

            <div
              className="hdc-chart"
            >
              <ResponsiveContainer
                width="100%"
                height="100%"
              >
                <LineChart
                  data={
                    data.daily.map(
                      item => ({
                        ...item,

                        date:
                          item.date
                            .slice(
                              0,
                              10,
                            ),
                      }),
                    )
                  }
                >
                  <CartesianGrid
                    strokeDasharray="3 3"
                  />

                  <XAxis
                    dataKey="date"
                  />

                  <YAxis
                    allowDecimals={
                      false
                    }
                  />

                  <Tooltip />

                  <Line
                    dataKey="count"
                    name="Tickets"
                    stroke="#2563eb"
                    dot={
                      false
                    }
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </section>
        </div>
      )}
    </main>
  )
}