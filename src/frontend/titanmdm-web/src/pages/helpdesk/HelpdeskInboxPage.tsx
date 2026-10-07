import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  useNavigate,
  useSearchParams,
} from 'react-router-dom'

import axios
  from 'axios'

import {
  ArrowRight,
  ClipboardList,
  LayoutGrid,
  Plus,
  RefreshCw,
  Search,
  Users,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
  legacyHelpdeskPermissions,
} from '../../auth/helpdeskAccess'

import {
  HelpdeskCreateRequest,
} from './HelpdeskCreateRequest'

import {
  HelpdeskKanbanBoard,
  type KanbanTicket,
} from './HelpdeskKanbanBoard'

import './HelpdeskPages.css'
import './HelpdeskWorkPage.css'
import './HelpdeskInboxWorkflow.css'

type View =
  | 'mine'
  | 'unassigned'
  | 'all'
  | 'kanban'

interface Ticket
  extends KanbanTicket {
}

interface Result {
  items: Ticket[]
  total: number
  page: number
  pageSize: number
}

interface Workload {
  assignedToMe: number
  unassigned: number
  active: number

  agents: {
    userId: string
    name: string
    openTickets: number
    isAvailable: boolean
    capacity: number
  }[]
}

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

    urgent:
      'Urgente',
  }

function resolveView(
  value:
    string | null,
): View {
  return value ===
      'unassigned'
    ||
    value ===
      'all'
    ||
    value ===
      'kanban'
      ? value
      : 'mine'
}

function formatDate(
  value:
    string,
) {
  const parsed =
    new Date(
      /(?:Z|[+-]\d{2}:?\d{2})$/i
        .test(
          value,
        )
        ? value
        : value +
          'Z',
    )

  if (
    Number.isNaN(
      parsed.getTime(),
    )
  ) {
    return '—'
  }

  return new Intl
    .DateTimeFormat(
      'es-DO',
      {
        dateStyle:
          'short',

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
) {
  if (
    axios.isAxiosError<{
      message?: string
      title?: string
    }>(
      error,
    )
  ) {
    return (
      error.response
        ?.data
        ?.message
      ??
      error.response
        ?.data
        ?.title
      ??
      `No se pudo completar la operación (${error.response?.status ?? 'sin conexión'}).`
    )
  }

  return error instanceof Error
    ? error.message
    : 'No se pudo completar la operación.'
}

export function HelpdeskInboxPage() {
  const navigate =
    useNavigate()

  const [
    searchParams,
    setSearchParams,
  ] =
    useSearchParams()

  const {
    hasPermission,
  } =
    useAuth()

  const requestedView =
    resolveView(
      searchParams.get(
        'view',
      ),
    )

  const [
    view,
    setView,
  ] =
    useState<View>(
      requestedView,
    )

  const [
    tickets,
    setTickets,
  ] =
    useState<Ticket[]>(
      [],
    )

  const [
    workload,
    setWorkload,
  ] =
    useState<Workload | null>(
      null,
    )

  const [
    total,
    setTotal,
  ] =
    useState(
      0,
    )

  const [
    page,
    setPage,
  ] =
    useState(
      1,
    )

  const [
    input,
    setInput,
  ] =
    useState(
      '',
    )

  const [
    search,
    setSearch,
  ] =
    useState(
      '',
    )

  const [
    status,
    setStatus,
  ] =
    useState(
      '',
    )

  const [
    priority,
    setPriority,
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
    workloadError,
    setWorkloadError,
  ] =
    useState(
      false,
    )

  const [
    showCreate,
    setShowCreate,
  ] =
    useState(
      false,
    )

  const [
    movingTicketId,
    setMovingTicketId,
  ] =
    useState<string | null>(
      null,
    )

  // ============================================================
  // PERMISSIONS
  // ============================================================

  const canCreate =
    hasPermission(
      helpdeskPermissions
        .requestCreate,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketCreate,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canViewMine =
    hasPermission(
      helpdeskPermissions
        .inboxMyWork,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .agentAccess,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canViewUnassigned =
    hasPermission(
      helpdeskPermissions
        .inboxUnassigned,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canViewAll =
    hasPermission(
      helpdeskPermissions
        .inboxAll,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )

  const canViewKanban =
    hasPermission(
      helpdeskPermissions
        .kanbanView,
    )
    ||
    hasPermission(
      helpdeskPermissions
        .agentAccess,
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
      helpdeskPermissions
        .adminAccess,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .ticketComment,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions
        .manage,
    )

  // ============================================================
  // URL STATE
  // ============================================================

  useEffect(
    () => {
      if (
        requestedView !==
        view
      ) {
        setView(
          requestedView,
        )

        setPage(
          1,
        )
      }
    },
    [
      requestedView,
      view,
    ],
  )

  const pageSize =
    view ===
      'kanban'
      ? 100
      : 25

  const pages =
    Math.max(
      1,
      Math.ceil(
        total /
        pageSize,
      ),
    )

  function openTicket(
    id:
      string,
  ) {
    navigate(
      `/helpdesk/tickets/${id}?workspace=helpdesk`,
    )
  }

  function changeView(
    next:
      View,
  ) {
    setView(
      next,
    )

    setPage(
      1,
    )

    setError(
      '',
    )

    setNotice(
      '',
    )

    const params =
      new URLSearchParams(
        searchParams,
      )

    if (
      next ===
      'mine'
    ) {
      params.delete(
        'view',
      )
    }
    else {
      params.set(
        'view',
        next,
      )
    }

    params.set(
      'workspace',
      'helpdesk',
    )

    setSearchParams(
      params,
    )
  }

  // ============================================================
  // LOAD TICKETS
  // ============================================================

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
          const {
            data,
          } =
            await apiClient
              .get<Result>(
                '/helpdesk/workload/tickets',
                {
                  params: {
                    view,

                    search:
                      search
                      ||
                      undefined,

                    status:
                      view ===
                        'kanban'
                        ? undefined
                        : status
                          ||
                          undefined,

                    priority:
                      priority
                      ||
                      undefined,

                    page,

                    pageSize,
                  },
                },
              )

          setTickets(
            data.items,
          )

          setTotal(
            data.total,
          )
        }
        catch (
          exception
        ) {
          setTickets(
            [],
          )

          setTotal(
            0,
          )

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
      [
        view,
        search,
        status,
        priority,
        page,
        pageSize,
      ],
    )

  // ============================================================
  // LOAD WORKLOAD
  // ============================================================

  const loadWorkload =
    useCallback(
      async () => {
        try {
          const {
            data,
          } =
            await apiClient
              .get<Workload>(
                '/helpdesk/workload',
              )

          setWorkload(
            data,
          )

          setWorkloadError(
            false,
          )
        }
        catch {
          setWorkload(
            null,
          )

          setWorkloadError(
            true,
          )
        }
      },
      [],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  useEffect(
    () => {
      void loadWorkload()
    },
    [
      loadWorkload,
    ],
  )

  function refresh() {
    setNotice(
      '',
    )

    void load()
    void loadWorkload()
  }

  function applySearch() {
    setSearch(
      input.trim(),
    )

    setPage(
      1,
    )
  }

  // ============================================================
  // KANBAN TRANSITION
  // ============================================================

  async function transitionFromKanban(
    ticket:
      KanbanTicket,

    targetStatus:
      | 'new'
      | 'open'
      | 'inprogress'
      | 'pendinguser',
  ) {
    if (
      !canTransition
      ||
      ticket.status ===
        targetStatus
    ) {
      return
    }

    setMovingTicketId(
      ticket.id,
    )

    setError(
      '',
    )

    setNotice(
      '',
    )

    try {
      await apiClient.post(
        `/helpdesk/tickets/${ticket.id}/transition`,
        {
          status:
            targetStatus,
        },
      )

      setTickets(
        current =>
          current.map(
            item =>
              item.id ===
                ticket.id
                ? {
                    ...item,

                    status:
                      targetStatus,

                    updatedAtUtc:
                      new Date()
                        .toISOString(),
                  }
                : item,
          ),
      )

      const label =
        STATUS[
          targetStatus
        ]
        ??
        targetStatus

      setNotice(
        `${ticket.number} movido a «${label}».`,
      )

      await loadWorkload()
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )

      await load()
    }
    finally {
      setMovingTicketId(
        null,
      )
    }
  }

  // ============================================================
  // TABS
  // ============================================================

  const tabs =
    useMemo(
      () =>
        [
          canViewMine
            ? {
                key:
                  'mine' as const,

                label:
                  'Mi trabajo',

                count:
                  workload
                    ?.assignedToMe,
              }
            : null,

          canViewUnassigned
            ? {
                key:
                  'unassigned' as const,

                label:
                  'Sin asignar',

                count:
                  workload
                    ?.unassigned,
              }
            : null,

          canViewAll
            ? {
                key:
                  'all' as const,

                label:
                  'Todos',

                count:
                  workload
                    ?.active,
              }
            : null,

          canViewKanban
            ? {
                key:
                  'kanban' as const,

                label:
                  'Kanban',

                count:
                  workload
                    ?.active,
              }
            : null,
        ]
          .filter(
            Boolean,
          ) as {
            key: View
            label: string
            count:
              number |
              undefined
          }[],
      [
        canViewMine,
        canViewUnassigned,
        canViewAll,
        canViewKanban,
        workload,
      ],
    )

  // ============================================================
  // RENDER
  // ============================================================

  return (
    <main
      className={
        'titan-page ' +
        'helpdesk-page ' +
        'helpdesk-inbox ' +
        'helpdesk-work'
      }
    >
      <header
        className="helpdesk-inbox__header"
      >
        <div>
          <span
            className="helpdesk-inbox__eyebrow"
          >
            {
              view ===
                'kanban'
                ? (
                  <LayoutGrid
                    size={15}
                  />
                )
                : (
                  <ClipboardList
                    size={15}
                  />
                )
            }

            OPERACIÓN TIC
          </span>

          <h1>
            {
              view ===
                'kanban'
                ? 'Tablero Kanban'
                : 'Bandeja de Mesa de Ayuda'
            }
          </h1>

          <p>
            {
              view ===
                'kanban'
                ? 'Administra el trabajo activo moviendo los tickets según su estado operativo.'
                : 'Atiende solicitudes, prioriza casos, controla el backlog y trabaja con tu equipo.'
            }
          </p>
        </div>

        <div
          className="helpdesk-inbox__header-actions"
        >
          <button
            type="button"
            className={
              'helpdesk-ui-button ' +
              'helpdesk-ui-button--secondary'
            }
            disabled={
              loading
            }
            onClick={
              refresh
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          {canCreate && (
            <button
              type="button"
              className={
                'helpdesk-ui-button ' +
                'helpdesk-ui-button--primary'
              }
              onClick={
                () =>
                  setShowCreate(
                    true,
                  )
              }
            >
              <Plus
                size={16}
              />

              Nuevo ticket
            </button>
          )}
        </div>
      </header>

      {notice && (
        <div
          className="hd-workflow-notice"
          role="status"
        >
          {notice}
        </div>
      )}

      {error && (
        <div
          className="helpdesk-inbox__error"
          role="alert"
        >
          {error}

          <button
            type="button"
            onClick={
              refresh
            }
          >
            Reintentar
          </button>
        </div>
      )}

      {workloadError && (
        <div
          className="helpdesk-inbox__error"
          role="alert"
        >
          La carga del equipo
          no está disponible.
        </div>
      )}

      <nav
        className="helpdesk-work__tabs"
        aria-label="Bandejas de tickets"
      >
        {tabs.map(
          item => (
            <button
              key={
                item.key
              }
              type="button"
              className={
                view ===
                  item.key
                  ? 'is-active'
                  : ''
              }
              aria-pressed={
                view ===
                item.key
              }
              onClick={
                () =>
                  changeView(
                    item.key,
                  )
              }
            >
              {
                item.label
              }

              {item.count !==
                undefined && (
                <strong>
                  {
                    item.count
                  }
                </strong>
              )}
            </button>
          ),
        )}
      </nav>

      <div
        className={
          view ===
            'kanban'
            ? 'helpdesk-work__layout helpdesk-work__layout--kanban'
            : 'helpdesk-work__layout'
        }
      >
        <section
          className="helpdesk-inbox__panel"
          aria-label="Solicitudes"
        >
          <div
            className="helpdesk-inbox__filters"
          >
            <label
              className="helpdesk-inbox__search"
            >
              <Search
                size={17}
              />

              <span
                className="sr-only"
              >
                Buscar
              </span>

              <input
                value={
                  input
                }
                onChange={
                  event =>
                    setInput(
                      event.target.value,
                    )
                }
                onKeyDown={
                  event => {
                    if (
                      event.key ===
                      'Enter'
                    ) {
                      applySearch()
                    }
                  }
                }
                placeholder="Número, asunto o categoría"
              />
            </label>

            {view !==
              'kanban' && (
              <label>
                <span
                  className="sr-only"
                >
                  Estado
                </span>

                <select
                  value={
                    status
                  }
                  onChange={
                    event => {
                      setStatus(
                        event.target.value,
                      )

                      setPage(
                        1,
                      )
                    }
                  }
                >
                  <option value="">
                    Todos los estados
                  </option>

                  {Object.entries(
                    STATUS,
                  ).map(
                    (
                      [
                        key,
                        label,
                      ],
                    ) => (
                      <option
                        key={
                          key
                        }
                        value={
                          key
                        }
                      >
                        {
                          label
                        }
                      </option>
                    ),
                  )}
                </select>
              </label>
            )}

            <label>
              <span
                className="sr-only"
              >
                Prioridad
              </span>

              <select
                value={
                  priority
                }
                onChange={
                  event => {
                    setPriority(
                      event.target.value,
                    )

                    setPage(
                      1,
                    )
                  }
                }
              >
                <option value="">
                  Todas las prioridades
                </option>

                {Object.entries(
                  PRIORITY,
                ).map(
                  (
                    [
                      key,
                      label,
                    ],
                  ) => (
                    <option
                      key={
                        key
                      }
                      value={
                        key
                      }
                    >
                      {
                        label
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <button
              type="button"
              className={
                'helpdesk-ui-button ' +
                'helpdesk-ui-button--secondary'
              }
              onClick={
                applySearch
              }
            >
              Buscar
            </button>

            <button
              type="button"
              className="helpdesk-inbox__clear"
              onClick={
                () => {
                  setInput(
                    '',
                  )

                  setSearch(
                    '',
                  )

                  setStatus(
                    '',
                  )

                  setPriority(
                    '',
                  )

                  setPage(
                    1,
                  )
                }
              }
            >
              Limpiar
            </button>
          </div>

          {view ===
            'kanban' ? (
            <>
              {loading ? (
                <div
                  className="helpdesk-inbox__empty"
                >
                  Cargando tablero…
                </div>
              ) : (
                <HelpdeskKanbanBoard
                  tickets={
                    tickets
                  }
                  canTransition={
                    canTransition
                  }
                  movingTicketId={
                    movingTicketId
                  }
                  onOpen={
                    openTicket
                  }
                  onTransition={
                    transitionFromKanban
                  }
                  formatDate={
                    formatDate
                  }
                />
              )}

              <footer
                className="helpdesk-inbox__footer"
              >
                <span>
                  {total} tickets activos
                </span>
              </footer>
            </>
          ) : (
            <>
              <div
                className="helpdesk-inbox__panel-heading"
              >
                <div>
                  <h2>
                    {
                      view ===
                        'mine'
                        ? 'Asignadas a mí'
                        : view ===
                            'unassigned'
                          ? 'Pendientes de asignación'
                          : 'Todas las solicitudes'
                    }
                  </h2>

                  <p>
                    Abre un caso para
                    atenderlo y consultar
                    su historial.
                  </p>
                </div>

                <span
                  className="helpdesk-inbox__total"
                >
                  {
                    total
                  }
                  {' '}
                  resultados
                </span>
              </div>

              <div
                className="helpdesk-inbox__table-wrap"
              >
                <table
                  className="helpdesk-inbox__table"
                >
                  <thead>
                    <tr>
                      <th>
                        Solicitud
                      </th>

                      <th>
                        Estado
                      </th>

                      <th>
                        Prioridad
                      </th>

                      <th>
                        Solicitante
                      </th>

                      <th>
                        Asignado
                      </th>

                      <th>
                        Actualizado
                      </th>

                      <th>
                        <span
                          className="sr-only"
                        >
                          Abrir
                        </span>
                      </th>
                    </tr>
                  </thead>

                  <tbody>
                    {loading ? (
                      <tr>
                        <td
                          colSpan={
                            7
                          }
                          className="helpdesk-inbox__empty"
                        >
                          Cargando…
                        </td>
                      </tr>
                    ) : !tickets.length ? (
                      <tr>
                        <td
                          colSpan={
                            7
                          }
                          className="helpdesk-inbox__empty"
                        >
                          <ClipboardList
                            size={27}
                          />

                          <strong>
                            Sin solicitudes
                            en esta vista
                          </strong>

                          <span>
                            Prueba otros filtros.
                          </span>
                        </td>
                      </tr>
                    ) : (
                      tickets.map(
                        item => (
                          <tr
                            key={
                              item.id
                            }
                          >
                            <td>
                              <button
                                type="button"
                                className="helpdesk-inbox__ticket-link"
                                onClick={
                                  () =>
                                    openTicket(
                                      item.id,
                                    )
                                }
                              >
                                <span>
                                  {
                                    item.number
                                  }
                                </span>

                                <strong>
                                  {
                                    item.subject
                                  }
                                </strong>

                                {item.slaBreached && (
                                  <small>
                                    SLA vencido
                                  </small>
                                )}
                              </button>
                            </td>

                            <td>
                              <span
                                className={
                                  `helpdesk-inbox__badge ` +
                                  `helpdesk-inbox__badge--${item.status}`
                                }
                              >
                                {
                                  STATUS[
                                    item.status
                                  ]
                                  ??
                                  item.status
                                }
                              </span>
                            </td>

                            <td>
                              {
                                PRIORITY[
                                  item.priority
                                ]
                                ??
                                item.priority
                              }
                            </td>

                            <td>
                              {
                                item.requesterName
                              }
                            </td>

                            <td>
                              {
                                item.assigneeName
                                ??
                                'Sin asignar'
                              }
                            </td>

                            <td>
                              {
                                formatDate(
                                  item.updatedAtUtc,
                                )
                              }
                            </td>

                            <td>
                              <button
                                type="button"
                                className="helpdesk-inbox__open"
                                aria-label={
                                  `Abrir ${item.number}`
                                }
                                onClick={
                                  () =>
                                    openTicket(
                                      item.id,
                                    )
                                }
                              >
                                <ArrowRight
                                  size={17}
                                />
                              </button>
                            </td>
                          </tr>
                        ),
                      )
                    )}
                  </tbody>
                </table>
              </div>

              <footer
                className="helpdesk-inbox__footer"
              >
                <span>
                  Página {page} de {pages}
                </span>

                <button
                  type="button"
                  disabled={
                    loading
                    ||
                    page <=
                      1
                  }
                  onClick={
                    () =>
                      setPage(
                        value =>
                          value -
                          1,
                      )
                  }
                >
                  Anterior
                </button>

                <button
                  type="button"
                  disabled={
                    loading
                    ||
                    page >=
                      pages
                  }
                  onClick={
                    () =>
                      setPage(
                        value =>
                          value +
                          1,
                      )
                  }
                >
                  Siguiente
                </button>
              </footer>
            </>
          )}
        </section>

        {view !==
          'kanban' && (
          <aside
            className="helpdesk-work__agents"
            aria-label="Carga del equipo"
          >
            <div>
              <Users
                size={19}
              />

              <div>
                <h2>
                  Equipo TIC
                </h2>

                <p>
                  Carga activa y disponibilidad
                </p>
              </div>
            </div>

            {!workload ? (
              <p>
                {
                  workloadError
                    ? 'Carga no disponible.'
                    : 'Cargando equipo…'
                }
              </p>
            ) : !workload
                .agents
                .length ? (
              <p>
                Todavía no hay agentes configurados.
              </p>
            ) : (
              workload.agents.map(
                agent => (
                  <article
                    key={
                      agent.userId
                    }
                  >
                    <div>
                      <strong>
                        {
                          agent.name
                        }
                      </strong>

                      <span
                        className={
                          agent.isAvailable
                            ? 'is-available'
                            : ''
                        }
                      >
                        {
                          agent.isAvailable
                            ? 'Disponible'
                            : 'No disponible'
                        }
                      </span>
                    </div>

                    <p>
                      {
                        agent.openTickets
                      }
                      {' '}
                      activos · capacidad
                      {' '}
                      {
                        agent.capacity
                      }
                    </p>
                  </article>
                ),
              )
            )}
          </aside>
        )}
      </div>

      {showCreate &&
        canCreate && (
        <HelpdeskCreateRequest
          console
          onCancel={
            () =>
              setShowCreate(
                false,
              )
          }
          onCreated={
            id => {
              setShowCreate(
                false,
              )

              openTicket(
                id,
              )
            }
          }
        />
      )}
    </main>
  )
}