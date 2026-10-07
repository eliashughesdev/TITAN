import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from 'react'

import {
  HelpdeskRoutingSimulator,
} from './HelpdeskRoutingSimulator'

import {
  Link,
  useSearchParams,
} from 'react-router-dom'

import axios
  from 'axios'

import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  Clock3,
  Gauge,
  Headphones,
  MapPin,
  RefreshCw,
  ShieldCheck,
  Trash2,
  UserRound,
  UsersRound,
  XCircle,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskPages.css'
import './HelpdeskOperationsPage.css'

type OperationsTab =
  | 'sites'
  | 'coverage'
  | 'technicians'

type Site = {
  id: string
  code: string
  name: string
  city: string | null
  province: string | null
  isActive: boolean
}

type SiteLocation = {
  id: string
  siteId: string
  name: string
  description: string | null
  isActive: boolean
}

type Team = {
  id: string
  name: string
  description: string | null
  categories: string | null
  isActive: boolean
}

type Coverage = {
  id: string
  teamId: string
  siteId: string
  siteLocationId: string | null
  category: string | null
  priority: number
  isActive: boolean
}

type WeeklySlot = {
  day: number
  start: string
  end: string
}

type TechnicianGroup = {
  teamId: string
  teamName: string
  teamActive: boolean

  isAvailable: boolean
  acceptsAutomaticAssignments: boolean

  maxOpenTickets: number
  openTickets: number
  remainingCapacity: number
  isAtCapacity: boolean

  scheduleConfigured: boolean
  scheduleEnabled: boolean
  onDuty: boolean

  priority: number
  timeZoneId: string

  slots: WeeklySlot[]

  routingReady: boolean
  routingIssues: string[]
}

type Staff = {
  id: string
  name: string
  email: string

  roles: string[]

  siteId: string | null
  siteName: string | null

  siteLocationId: string | null
  siteLocationName: string | null

  canWorkTickets: boolean
  isEligible: boolean

  assistantEnabled: boolean

  groups: TechnicianGroup[]
  groupNames: string[]

  openTickets: number
  maxCapacity: number
  remainingCapacity: number
  isAtCapacity: boolean

  isAvailable: boolean
  acceptsAutomaticAssignments: boolean

  scheduleConfigured: boolean
  onDuty: boolean

  routingReady: boolean
  routingIssues: string[]
}

type Catalog = {
  sites: Site[]
  siteLocations: SiteLocation[]
  teams: Team[]
  coverages: Coverage[]
}

const emptyCatalog:
  Catalog = {
    sites: [],
    siteLocations: [],
    teams: [],
    coverages: [],
  }

const DAY_LABELS = [
  'Dom',
  'Lun',
  'Mar',
  'Mié',
  'Jue',
  'Vie',
  'Sáb',
]

function resolveTab(
  value:
    string | null,
): OperationsTab {
  if (
    value ===
      'coverage'
  ) {
    return 'coverage'
  }

  if (
    value ===
      'technicians'
  ) {
    return 'technicians'
  }

  return 'sites'
}

function errorMessage(
  error: unknown,
) {
  if (
    axios.isAxiosError(
      error,
    )
  ) {
    const data =
      error.response?.data as
        | {
            message?: string
            title?: string
          }
        | undefined

    return (
      data?.message
      ||
      data?.title
      ||
      `No se pudo completar la operación (${error.response?.status ?? 'sin conexión'}).`
    )
  }

  return error instanceof Error
    ? error.message
    : 'No se pudo completar la operación.'
}

function routingLabel(
  technician: Staff,
) {
  if (
    technician.routingReady
  ) {
    return 'LISTO PARA ROUTING'
  }

  if (
    technician.isAtCapacity
  ) {
    return 'CAPACIDAD COMPLETA'
  }

  if (
    technician.scheduleConfigured
    &&
    !technician.onDuty
  ) {
    return 'FUERA DE TURNO'
  }

  if (
    !technician.scheduleConfigured
  ) {
    return 'SIN TURNO'
  }

  if (
    !technician.isAvailable
  ) {
    return 'NO DISPONIBLE'
  }

  if (
    !technician.groupNames.length
  ) {
    return 'SIN GRUPO'
  }

  if (
    !technician.canWorkTickets
  ) {
    return 'SIN PERMISO'
  }

  return 'REVISAR CONFIGURACIÓN'
}

function routingTone(
  technician: Staff,
) {
  if (
    technician.routingReady
  ) {
    return 'ready'
  }

  if (
    technician.isAtCapacity
  ) {
    return 'danger'
  }

  if (
    technician.scheduleConfigured
    &&
    !technician.onDuty
  ) {
    return 'warning'
  }

  return 'muted'
}

function formatSchedule(
  group: TechnicianGroup,
) {
  if (
    !group.slots.length
  ) {
    return 'Sin horario'
  }

  return group.slots
    .map(
      slot =>
        `${DAY_LABELS[slot.day] ?? slot.day} ${slot.start}-${slot.end}`,
    )
    .join(
      ' · ',
    )
}

export function HelpdeskOperationsPage() {
  const [
    searchParams,
    setSearchParams,
  ] =
    useSearchParams()

  const requestedTab =
    resolveTab(
      searchParams.get(
        'tab',
      ),
    )

  const [
    catalog,
    setCatalog,
  ] =
    useState<Catalog>(
      emptyCatalog,
    )

  const [
    staff,
    setStaff,
  ] =
    useState<Staff[]>(
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
    message,
    setMessage,
  ] =
    useState(
      '',
    )

  const [
    tab,
    setTab,
  ] =
    useState<OperationsTab>(
      requestedTab,
    )

  const [
    coverageTeamId,
    setCoverageTeamId,
  ] =
    useState(
      '',
    )

  const [
    coverageSiteId,
    setCoverageSiteId,
  ] =
    useState(
      '',
    )

  const [
    coverageLocationId,
    setCoverageLocationId,
  ] =
    useState(
      '',
    )

  const [
    coverageCategory,
    setCoverageCategory,
  ] =
    useState(
      '',
    )

  const [
    coveragePriority,
    setCoveragePriority,
  ] =
    useState(
      100,
    )

  const disabled =
    loading
    ||
    saving

  // ============================================================
  // URL <-> TAB
  // ============================================================

  useEffect(
    () => {
      if (
        requestedTab !==
        tab
      ) {
        setTab(
          requestedTab,
        )
      }
    },
    [
      requestedTab,
      tab,
    ],
  )

  function changeTab(
    next:
      OperationsTab,
  ) {
    setTab(
      next,
    )

    setError(
      '',
    )

    setMessage(
      '',
    )

    const params =
      new URLSearchParams(
        searchParams,
      )

    if (
      next ===
      'sites'
    ) {
      params.delete(
        'tab',
      )
    }
    else {
      params.set(
        'tab',
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
  // DERIVED DATA
  // ============================================================

  const activeSites =
    useMemo(
      () =>
        catalog.sites.filter(
          site =>
            site.isActive,
        ),
      [
        catalog.sites,
      ],
    )

  const activeTeams =
    useMemo(
      () =>
        catalog.teams.filter(
          team =>
            team.isActive,
        ),
      [
        catalog.teams,
      ],
    )

  const visibleLocations =
    useMemo(
      () =>
        catalog.siteLocations
          .filter(
            item =>
              item.isActive
              &&
              item.siteId ===
                coverageSiteId,
          ),
      [
        catalog.siteLocations,
        coverageSiteId,
      ],
    )

  const activeLocationsCount =
    useMemo(
      () =>
        catalog.siteLocations
          .filter(
            item =>
              item.isActive,
          )
          .length,
      [
        catalog.siteLocations,
      ],
    )

  const routingReadyCount =
    useMemo(
      () =>
        staff.filter(
          technician =>
            technician.routingReady,
        )
          .length,
      [
        staff,
      ],
    )

  const onDutyCount =
    useMemo(
      () =>
        staff.filter(
          technician =>
            technician.onDuty,
        )
          .length,
      [
        staff,
      ],
    )

  const capacityAlertCount =
    useMemo(
      () =>
        staff.filter(
          technician =>
            technician.isAtCapacity,
        )
          .length,
      [
        staff,
      ],
    )

  const openAssignedTickets =
    useMemo(
      () =>
        staff.reduce(
          (
            total,
            technician,
          ) =>
            total +
            technician.openTickets,
          0,
        ),
      [
        staff,
      ],
    )

  // ============================================================
  // LOAD
  // ============================================================

  const load =
    useCallback(
      async () => {
        setLoading(
          true,
        )

        try {
          const [
            catalogResult,
            staffResult,
          ] =
            await Promise.all(
              [
                apiClient
                  .get<Catalog>(
                    '/helpdesk/site-coverage/catalog',
                  ),

                apiClient
                  .get<Staff[]>(
                    '/helpdesk/staff/users',
                  ),
              ],
            )

          setCatalog(
            catalogResult.data,
          )

          setStaff(
            staffResult.data,
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

  const refresh =
    useCallback(
      async () => {
        setError(
          '',
        )

        try {
          await load()
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
      },
      [
        load,
      ],
    )

  useEffect(
    () => {
      void refresh()
    },
    [
      refresh,
    ],
  )

  // ============================================================
  // SAVE HELPER
  // ============================================================

  async function save(
    action:
      () => Promise<unknown>,

    success:
      string,

    reset?:
      () => void,
  ) {
    if (
      disabled
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setMessage(
      '',
    )

    try {
      await action()

      setMessage(
        success,
      )

      reset?.()

      await load()
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
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // COVERAGE CREATE
  // ============================================================

  function handleSubmit(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !coverageTeamId
      ||
      !coverageSiteId
    ) {
      setError(
        'Selecciona un grupo y una localidad.',
      )

      return
    }

    if (
      coveragePriority <
        1
      ||
      coveragePriority >
        1000
    ) {
      setError(
        'La prioridad debe estar entre 1 y 1000.',
      )

      return
    }

    void save(
      () =>
        apiClient.post(
          '/helpdesk/site-coverage',
          {
            teamId:
              coverageTeamId,

            siteId:
              coverageSiteId,

            siteLocationId:
              coverageLocationId
                ? coverageLocationId
                : null,

            category:
              coverageCategory
                .trim()
                .toLowerCase()
                ||
                null,

            priority:
              coveragePriority,
          },
        ),

      'Cobertura guardada correctamente.',

      () => {
        setCoverageCategory(
          '',
        )

        setCoverageLocationId(
          '',
        )
      },
    )
  }

  // ============================================================
  // COVERAGE REMOVE
  // ============================================================

  function removeCoverage(
    coverage:
      Coverage,
  ) {
    if (
      !window.confirm(
        '¿Eliminar esta cobertura para futuras asignaciones?',
      )
    ) {
      return
    }

    void save(
      () =>
        apiClient.delete(
          `/helpdesk/site-coverage/${coverage.id}`,
        ),

      'Cobertura eliminada.',
    )
  }

  // ============================================================
  // LABEL HELPERS
  // ============================================================

  function siteName(
    id:
      string,
  ) {
    return (
      catalog.sites.find(
        item =>
          item.id ===
          id,
      )
        ?.name
      ??
      'Localidad no disponible'
    )
  }

  function locationName(
    id:
      string | null,
  ) {
    if (!id) {
      return 'Toda la localidad'
    }

    return (
      catalog.siteLocations.find(
        item =>
          item.id ===
          id,
      )
        ?.name
      ??
      'Ubicación no disponible'
    )
  }

  function teamName(
    id:
      string,
  ) {
    return (
      catalog.teams.find(
        item =>
          item.id ===
          id,
      )
        ?.name
      ??
      'Grupo no disponible'
    )
  }

  // ============================================================
  // PAGE COPY
  // ============================================================

  const pageTitle =
    tab ===
      'technicians'
      ? 'Preparación de técnicos'
      : tab ===
          'coverage'
        ? 'Cobertura operativa'
        : 'Localidades de Helpdesk'

  const pageDescription =
    tab ===
      'technicians'
      ? 'Verifica permisos, grupos, capacidad, disponibilidad, turnos y preparación para autoasignación.'
      : tab ===
          'coverage'
        ? 'Define qué grupo atiende cada localidad, sublocalidad y categoría.'
        : 'La Mesa de Ayuda consume las localidades corporativas configuradas en TitanMDM.'

  // ============================================================
  // RENDER
  // ============================================================

  return (
    <main
      className="titan-page helpdesk-page hd-operation"
    >
      <header
        className="helpdesk-inbox__header"
      >
        <div>
          <span
            className="helpdesk-inbox__eyebrow"
          >
            <Headphones
              size={15}
            />

            PREPARACIÓN OPERATIVA
          </span>

          <h1>
            {pageTitle}
          </h1>

          <p>
            {pageDescription}
          </p>
        </div>

        <div
          className="hd-operation__header-actions"
        >
          <Link
            to="/helpdesk/especialidades?tab=schedules&workspace=helpdesk"
            className="helpdesk-ui-button helpdesk-ui-button--secondary"
          >
            <UsersRound
              size={16}
            />

            Grupos y turnos
          </Link>

          <button
            type="button"
            className="helpdesk-ui-button helpdesk-ui-button--secondary"
            disabled={
              disabled
            }
            onClick={
              () =>
                void refresh()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>
        </div>
      </header>

      {error && (
        <div
          role="alert"
          className="helpdesk-inbox__error"
        >
          {error}
        </div>
      )}

      {message && (
        <div
          role="status"
          className="hd-operation__success"
        >
          {message}
        </div>
      )}

      {/* ======================================================
          OPERATIONAL METRICS
         ====================================================== */}

      <section
        className="hd-operation__metrics hd-operation__metrics--six"
      >
        <article>
          <Building2
            size={19}
          />

          <span>
            Localidades
          </span>

          <strong>
            {
              activeSites.length
            }
          </strong>

          <small>
            {
              activeLocationsCount
            }
            {' '}
            sublocalidades
          </small>
        </article>

        <article>
          <UsersRound
            size={19}
          />

          <span>
            Técnicos
          </span>

          <strong>
            {
              staff.length
            }
          </strong>

          <small>
            Personal relacionado con Helpdesk
          </small>
        </article>

        <article
          className="is-success"
        >
          <ShieldCheck
            size={19}
          />

          <span>
            Routing listo
          </span>

          <strong>
            {
              routingReadyCount
            }
          </strong>

          <small>
            Elegibles en este momento
          </small>
        </article>

        <article>
          <Clock3
            size={19}
          />

          <span>
            En turno
          </span>

          <strong>
            {
              onDutyCount
            }
          </strong>

          <small>
            Según horario configurado
          </small>
        </article>

        <article
          className={
            capacityAlertCount
              ? 'is-danger'
              : ''
          }
        >
          <AlertTriangle
            size={19}
          />

          <span>
            Capacidad llena
          </span>

          <strong>
            {
              capacityAlertCount
            }
          </strong>

          <small>
            Requieren redistribución
          </small>
        </article>

        <article>
          <Gauge
            size={19}
          />

          <span>
            Tickets activos
          </span>

          <strong>
            {
              openAssignedTickets
            }
          </strong>

          <small>
            Asignados actualmente
          </small>
        </article>
      </section>

      {/* ======================================================
          TABS
         ====================================================== */}

      <nav
        className="hd-operation__tabs"
        aria-label="Organización operativa"
      >
        <button
          type="button"
          aria-pressed={
            tab ===
            'sites'
          }
          onClick={
            () =>
              changeTab(
                'sites',
              )
          }
        >
          <Building2
            size={16}
          />

          Localidades
        </button>

        <button
          type="button"
          aria-pressed={
            tab ===
            'coverage'
          }
          onClick={
            () =>
              changeTab(
                'coverage',
              )
          }
        >
          <MapPin
            size={16}
          />

          Cobertura
        </button>

        <button
          type="button"
          aria-pressed={
            tab ===
            'technicians'
          }
          onClick={
            () =>
              changeTab(
                'technicians',
              )
          }
        >
          <UserRound
            size={16}
          />

          Técnicos
        </button>
      </nav>

      {loading && (
        <p
          role="status"
          className="hd-operation__loading"
        >
          Cargando configuración operativa…
        </p>
      )}

      {/* ======================================================
          SITES
         ====================================================== */}

      {!loading &&
        tab ===
          'sites' && (
        <section
          className="hd-operation__card"
        >
          <div
            className="hd-operation__section-heading"
          >
            <div>
              <h2>
                Localidades corporativas
              </h2>

              <p>
                Fuente única:
                Configuración → Localidades.
                Helpdesk no mantiene un catálogo paralelo.
              </p>
            </div>
          </div>

          <div
            className="hd-operation__table"
          >
            <table>
              <thead>
                <tr>
                  <th>
                    Código
                  </th>

                  <th>
                    Localidad
                  </th>

                  <th>
                    Ciudad
                  </th>

                  <th>
                    Provincia
                  </th>

                  <th>
                    Estado
                  </th>
                </tr>
              </thead>

              <tbody>
                {!catalog.sites
                    .length ? (
                  <tr>
                    <td
                      colSpan={5}
                    >
                      No hay localidades configuradas.
                    </td>
                  </tr>
                ) : (
                  catalog.sites.map(
                    site => (
                      <tr
                        key={
                          site.id
                        }
                      >
                        <td>
                          <code>
                            {
                              site.code
                            }
                          </code>
                        </td>

                        <td>
                          <strong>
                            {
                              site.name
                            }
                          </strong>
                        </td>

                        <td>
                          {
                            site.city
                            ??
                            '—'
                          }
                        </td>

                        <td>
                          {
                            site.province
                            ??
                            '—'
                          }
                        </td>

                        <td>
                          <span
                            className={
                              site.isActive
                                ? 'hd-operation__status hd-operation__status--ready'
                                : 'hd-operation__status hd-operation__status--muted'
                            }
                          >
                            {
                              site.isActive
                                ? 'Activa'
                                : 'Inactiva'
                            }
                          </span>
                        </td>
                      </tr>
                    ),
                  )
                )}
              </tbody>
            </table>
          </div>

          <div
            className="hd-operation__section-heading hd-operation__section-heading--secondary"
          >
            <div>
              <h3>
                Sublocalidades
              </h3>

              <p>
                Edificios, áreas, plantas u otras divisiones
                utilizadas para routing más específico.
              </p>
            </div>
          </div>

          <div
            className="hd-operation__table"
          >
            <table>
              <thead>
                <tr>
                  <th>
                    Localidad
                  </th>

                  <th>
                    Sublocalidad
                  </th>

                  <th>
                    Descripción
                  </th>

                  <th>
                    Estado
                  </th>
                </tr>
              </thead>

              <tbody>
                {!catalog
                    .siteLocations
                    .length ? (
                  <tr>
                    <td
                      colSpan={4}
                    >
                      No hay sublocalidades configuradas.
                    </td>
                  </tr>
                ) : (
                  catalog
                    .siteLocations
                    .map(
                      location => (
                        <tr
                          key={
                            location.id
                          }
                        >
                          <td>
                            {siteName(
                              location.siteId,
                            )}
                          </td>

                          <td>
                            <strong>
                              {
                                location.name
                              }
                            </strong>
                          </td>

                          <td>
                            {
                              location.description
                              ??
                              '—'
                            }
                          </td>

                          <td>
                            <span
                              className={
                                location.isActive
                                  ? 'hd-operation__status hd-operation__status--ready'
                                  : 'hd-operation__status hd-operation__status--muted'
                              }
                            >
                              {
                                location.isActive
                                  ? 'Activa'
                                  : 'Inactiva'
                              }
                            </span>
                          </td>
                        </tr>
                      ),
                    )
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* ======================================================
          COVERAGE
         ====================================================== */}
{!loading &&
  tab ===
    'coverage' && (
  <>
    <HelpdeskRoutingSimulator />

    <section
      className="hd-operation__card"
    >
      <div
        className="hd-operation__section-heading"
      >
        <div>
          <h2>
            Cobertura de grupos
          </h2>

          <p>
            Indica qué grupo atiende una localidad,
            sublocalidad y categoría concreta.
          </p>
        </div>
      </div>

      <form
        onSubmit={
          handleSubmit
        }
      >
            <label>
              Grupo

              <select
                required
                disabled={
                  disabled
                }
                value={
                  coverageTeamId
                }
                onChange={
                  event =>
                    setCoverageTeamId(
                      event.target.value,
                    )
                }
              >
                <option value="">
                  Selecciona grupo
                </option>

                {activeTeams.map(
                  team => (
                    <option
                      key={
                        team.id
                      }
                      value={
                        team.id
                      }
                    >
                      {
                        team.name
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <label>
              Localidad

              <select
                required
                disabled={
                  disabled
                }
                value={
                  coverageSiteId
                }
                onChange={
                  event => {
                    setCoverageSiteId(
                      event.target.value,
                    )

                    setCoverageLocationId(
                      '',
                    )
                  }
                }
              >
                <option value="">
                  Selecciona localidad
                </option>

                {activeSites.map(
                  site => (
                    <option
                      key={
                        site.id
                      }
                      value={
                        site.id
                      }
                    >
                      {
                        site.name
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <label>
              Sublocalidad

              <select
                value={
                  coverageLocationId
                }
                disabled={
                  disabled
                  ||
                  !coverageSiteId
                }
                onChange={
                  event =>
                    setCoverageLocationId(
                      event.target.value,
                    )
                }
              >
                <option value="">
                  Toda la localidad
                </option>

                {visibleLocations.map(
                  location => (
                    <option
                      key={
                        location.id
                      }
                      value={
                        location.id
                      }
                    >
                      {
                        location.name
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <label>
              Categoría opcional

              <input
                value={
                  coverageCategory
                }
                disabled={
                  disabled
                }
                onChange={
                  event =>
                    setCoverageCategory(
                      event.target.value,
                    )
                }
                placeholder="Ej.: redes"
              />
            </label>

            <label>
              Prioridad

              <input
                type="number"
                min={1}
                max={1000}
                disabled={
                  disabled
                }
                value={
                  coveragePriority
                }
                onChange={
                  event =>
                    setCoveragePriority(
                      Number(
                        event.target.value,
                      ),
                    )
                }
              />
            </label>

            <button
              type="submit"
              className="helpdesk-ui-button helpdesk-ui-button--primary"
              disabled={
                disabled
              }
            >
              Guardar cobertura
            </button>
          </form>

          <div
            className="hd-operation__table"
          >
            <table>
              <thead>
                <tr>
                  <th>
                    Grupo
                  </th>

                  <th>
                    Localidad
                  </th>

                  <th>
                    Sublocalidad
                  </th>

                  <th>
                    Categoría
                  </th>

                  <th>
                    Prioridad
                  </th>

                  <th>
                    Estado
                  </th>

                  <th>
                    Acción
                  </th>
                </tr>
              </thead>

              <tbody>
                {!catalog.coverages
                    .length ? (
                  <tr>
                    <td
                      colSpan={7}
                    >
                      No hay coberturas configuradas.
                    </td>
                  </tr>
                ) : (
                  catalog.coverages.map(
                    coverage => (
                      <tr
                        key={
                          coverage.id
                        }
                      >
                        <td>
                          <strong>
                            {teamName(
                              coverage.teamId,
                            )}
                          </strong>
                        </td>

                        <td>
                          {siteName(
                            coverage.siteId,
                          )}
                        </td>

                        <td>
                          {locationName(
                            coverage.siteLocationId,
                          )}
                        </td>

                        <td>
                          {
                            coverage.category
                            ??
                            'Todas'
                          }
                        </td>

                        <td>
                          {
                            coverage.priority
                          }
                        </td>

                        <td>
                          <span
                            className={
                              coverage.isActive
                                ? 'hd-operation__status hd-operation__status--ready'
                                : 'hd-operation__status hd-operation__status--muted'
                            }
                          >
                            {
                              coverage.isActive
                                ? 'Activa'
                                : 'Inactiva'
                            }
                          </span>
                        </td>

                        <td>
                          <button
                            type="button"
                            className="helpdesk-ui-button helpdesk-ui-button--secondary"
                            disabled={
                              disabled
                            }
                            onClick={
                              () =>
                                removeCoverage(
                                  coverage,
                                )
                            }
                          >
                            <Trash2
                              size={14}
                            />

                            Eliminar
                          </button>
                        </td>
                      </tr>
                    ),
                  )
                )}
              </tbody>
            </table>
          </div>
        </section>
        </>
      )}

      {/* ======================================================
          TECHNICIANS
         ====================================================== */}

      {!loading &&
        tab ===
          'technicians' && (
        <section
          className="hd-operation__card"
        >
          <div
            className="hd-operation__section-heading"
          >
            <div>
              <h2>
                Diagnóstico de técnicos
              </h2>

              <p>
                Esta vista indica exactamente por qué un técnico
                puede o no recibir tickets automáticamente.
              </p>
            </div>

            <Link
              to="/helpdesk/especialidades?tab=schedules&workspace=helpdesk"
              className="helpdesk-ui-button helpdesk-ui-button--primary"
            >
              Configurar grupos y turnos
            </Link>
          </div>

          {!staff.length ? (
            <div
              className="hd-operation__empty"
            >
              <UserRound
                size={32}
              />

              <strong>
                No hay técnicos operativos
              </strong>

              <p>
                Asigna permisos de Helpdesk y agrégalos
                a un grupo de trabajo.
              </p>
            </div>
          ) : (
            <div
              className="hd-operation__technicians"
            >
              {staff.map(
                technician => (
                  <article
                    key={
                      technician.id
                    }
                    className={
                      `hd-operation__technician-card ` +
                      `hd-operation__technician-card--${routingTone(technician)}`
                    }
                  >
                    <header
                      className="hd-operation__technician-header"
                    >
                      <div
                        className="hd-operation__avatar"
                      >
                        {
                          technician.name
                            .slice(
                              0,
                              1,
                            )
                            .toUpperCase()
                        }
                      </div>

                      <div
                        className="hd-operation__technician-name"
                      >
                        <strong>
                          {
                            technician.name
                          }
                        </strong>

                        <span>
                          {
                            technician.email
                          }
                        </span>

                        <small>
                          {
                            technician.roles.length
                              ? technician.roles.join(
                                  ' · ',
                                )
                              : 'Sin rol visible'
                          }
                        </small>
                      </div>

                      <span
                        className={
                          `hd-operation__routing-badge ` +
                          `hd-operation__routing-badge--${routingTone(technician)}`
                        }
                      >
                        {
                          technician.routingReady
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
                          routingLabel(
                            technician,
                          )
                        }
                      </span>
                    </header>

                    <div
                      className="hd-operation__technician-grid"
                    >
                      <div>
                        <span>
                          Localidad
                        </span>

                        <strong>
                          {
                            technician.siteName
                            ??
                            'Sin localidad'
                          }
                        </strong>

                        <small>
                          {
                            technician.siteLocationName
                            ??
                            'Sin sublocalidad'
                          }
                        </small>
                      </div>

                      <div>
                        <span>
                          Grupos
                        </span>

                        <strong>
                          {
                            technician.groupNames.length
                              ? technician.groupNames.join(
                                  ', ',
                                )
                              : 'Sin grupo'
                          }
                        </strong>

                        <small>
                          {
                            technician.groups.length
                          }
                          {' '}
                          membresías
                        </small>
                      </div>

                      <div>
                        <span>
                          Carga
                        </span>

                        <strong>
                          {
                            technician.openTickets
                          }
                          {' / '}
                          {
                            technician.maxCapacity
                            ||
                            '—'
                          }
                        </strong>

                        <small>
                          {
                            technician.remainingCapacity
                          }
                          {' '}
                          plazas disponibles
                        </small>
                      </div>

                      <div>
                        <span>
                          Turno
                        </span>

                        <strong>
                          {
                            technician.onDuty
                              ? 'En turno'
                              : technician.scheduleConfigured
                                ? 'Fuera de turno'
                                : 'Sin horario'
                          }
                        </strong>

                        <small>
                          {
                            technician.scheduleConfigured
                              ? 'Horario configurado'
                              : 'Requiere configuración'
                          }
                        </small>
                      </div>

                      <div>
                        <span>
                          Disponibilidad
                        </span>

                        <strong>
                          {
                            technician.isAvailable
                              ? 'Disponible'
                              : 'No disponible'
                          }
                        </strong>

                        <small>
                          {
                            technician.acceptsAutomaticAssignments
                              ? 'Acepta autoasignación'
                              : 'Autoasignación apagada'
                          }
                        </small>
                      </div>

                      <div>
                        <span>
                          Titan Assistant
                        </span>

                        <strong>
                          {
                            technician.assistantEnabled
                              ? 'Habilitado'
                              : 'Deshabilitado'
                          }
                        </strong>
                      </div>
                    </div>

                    {technician.routingIssues.length >
                      0 && (
                      <div
                        className="hd-operation__issues"
                      >
                        <AlertTriangle
                          size={16}
                        />

                        <div>
                          <strong>
                            Bloqueos de routing
                          </strong>

                          <div
                            className="hd-operation__issue-tags"
                          >
                            {technician.routingIssues.map(
                              issue => (
                                <span
                                  key={
                                    issue
                                  }
                                >
                                  {
                                    issue
                                  }
                                </span>
                              ),
                            )}
                          </div>
                        </div>
                      </div>
                    )}

                    {technician.groups.length >
                      0 && (
                      <details
                        className="hd-operation__groups-detail"
                      >
                        <summary>
                          Ver diagnóstico por grupo
                        </summary>

                        <div
                          className="hd-operation__group-list"
                        >
                          {technician.groups.map(
                            group => (
                              <div
                                key={
                                  group.teamId
                                }
                                className={
                                  group.routingReady
                                    ? 'is-ready'
                                    : ''
                                }
                              >
                                <header>
                                  <strong>
                                    {
                                      group.teamName
                                    }
                                  </strong>

                                  <span>
                                    Prioridad
                                    {' '}
                                    {
                                      group.priority
                                    }
                                  </span>
                                </header>

                                <p>
                                  <strong>
                                    Capacidad:
                                  </strong>
                                  {' '}
                                  {
                                    group.openTickets
                                  }
                                  /
                                  {
                                    group.maxOpenTickets
                                  }
                                  {' · '}

                                  <strong>
                                    Disponible:
                                  </strong>
                                  {' '}
                                  {
                                    group.isAvailable
                                      ? 'Sí'
                                      : 'No'
                                  }
                                  {' · '}

                                  <strong>
                                    En turno:
                                  </strong>
                                  {' '}
                                  {
                                    group.onDuty
                                      ? 'Sí'
                                      : 'No'
                                  }
                                </p>

                                <small>
                                  {formatSchedule(
                                    group,
                                  )}
                                </small>

                                {group.routingIssues.length >
                                  0 && (
                                  <div
                                    className="hd-operation__issue-tags"
                                  >
                                    {group.routingIssues.map(
                                      issue => (
                                        <span
                                          key={
                                            issue
                                          }
                                        >
                                          {
                                            issue
                                          }
                                        </span>
                                      ),
                                    )}
                                  </div>
                                )}
                              </div>
                            ),
                          )}
                        </div>
                      </details>
                    )}
                  </article>
                ),
              )}
            </div>
          )}
        </section>
      )}
    </main>
  )
}