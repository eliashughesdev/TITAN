import {
  useEffect,
  useMemo,
  useState,
} from 'react'

import axios
  from 'axios'

import {
  AlertTriangle,
  CheckCircle2,
  FlaskConical,
  Route,
  UserRound,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskRoutingSimulator.css'

type Site = {
  id: string
  code: string
  name: string
}

type Location = {
  id: string
  siteId: string
  name: string
}

type Team = {
  id: string
  name: string
}

type Catalog = {
  sites: Site[]
  locations: Location[]
  teams: Team[]
  categories: string[]
}

type Preview = {
  canAssign: boolean
  reason: string

  requesterLocation:
    string | null

  siteId:
    string | null

  siteLocationId:
    string | null

  teamId:
    string | null

  technicianId:
    string | null

  technicianName:
    string | null

  teamName:
    string | null

  coverageLocation:
    string | null

  technicianLocation:
    string | null

  openTickets:
    number | null

  capacity:
    number | null

  remainingCapacity:
    number | null

  category: string
  priority: string
}

function getError(
  error: unknown,
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
      `No se pudo ejecutar la simulación (${error.response?.status ?? 'sin conexión'}).`
    )
  }

  return error instanceof Error
    ? error.message
    : 'No se pudo ejecutar la simulación.'
}

export function HelpdeskRoutingSimulator() {
  const [
    catalog,
    setCatalog,
  ] =
    useState<Catalog>({
      sites: [],
      locations: [],
      teams: [],
      categories: [],
    })

  const [
    siteId,
    setSiteId,
  ] =
    useState(
      '',
    )

  const [
    locationId,
    setLocationId,
  ] =
    useState(
      '',
    )

  const [
    teamId,
    setTeamId,
  ] =
    useState(
      '',
    )

  const [
    category,
    setCategory,
  ] =
    useState(
      'general',
    )

  const [
    priority,
    setPriority,
  ] =
    useState(
      'medium',
    )

  const [
    preview,
    setPreview,
  ] =
    useState<Preview | null>(
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
    running,
    setRunning,
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

  const visibleLocations =
    useMemo(
      () =>
        catalog.locations
          .filter(
            location =>
              location.siteId ===
                siteId,
          ),
      [
        catalog.locations,
        siteId,
      ],
    )

  useEffect(
    () => {
      async function load() {
        try {
          const {
            data,
          } =
            await apiClient
              .get<Catalog>(
                '/helpdesk/routing/catalog',
              )

          setCatalog(
            data,
          )

          if (
            data.categories
              .length
          ) {
            setCategory(
              data.categories[0],
            )
          }
        }
        catch (
          exception
        ) {
          setError(
            getError(
              exception,
            ),
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      }

      void load()
    },
    [],
  )

  async function simulate() {
    if (
      !siteId
      ||
      !category.trim()
    ) {
      setError(
        'Selecciona una localidad y una categoría.',
      )

      return
    }

    setRunning(
      true,
    )

    setError(
      '',
    )

    setPreview(
      null,
    )

    try {
      const {
        data,
      } =
        await apiClient
          .post<Preview>(
            '/helpdesk/routing/preview',
            {
              requesterUserId:
                null,

              siteId,

              siteLocationId:
                locationId
                  ||
                  null,

              requestedTeamId:
                teamId
                  ||
                  null,

              category:
                category
                  .trim()
                  .toLowerCase(),

              priority,
            },
          )

      setPreview(
        data,
      )
    }
    catch (
      exception
    ) {
      setError(
        getError(
          exception,
        ),
      )
    }
    finally {
      setRunning(
        false,
      )
    }
  }

  return (
    <section
      className="hdrs"
    >
      <header
        className="hdrs__header"
      >
        <div
          className="hdrs__icon"
        >
          <FlaskConical
            size={20}
          />
        </div>

        <div>
          <h2>
            Simulador de routing
          </h2>

          <p>
            Ejecuta el mismo motor de
            asignación automática sin crear
            ni modificar tickets.
          </p>
        </div>
      </header>

      {error && (
        <div
          className="hdrs__error"
          role="alert"
        >
          <AlertTriangle
            size={16}
          />

          {error}
        </div>
      )}

      <div
        className="hdrs__form"
      >
        <label>
          Localidad

          <select
            value={
              siteId
            }
            disabled={
              loading
              ||
              running
            }
            onChange={
              event => {
                setSiteId(
                  event.target.value,
                )

                setLocationId(
                  '',
                )

                setPreview(
                  null,
                )
              }
            }
          >
            <option value="">
              Selecciona localidad
            </option>

            {catalog.sites.map(
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
              locationId
            }
            disabled={
              !siteId
              ||
              loading
              ||
              running
            }
            onChange={
              event => {
                setLocationId(
                  event.target.value,
                )

                setPreview(
                  null,
                )
              }
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
          Categoría

          <select
            value={
              category
            }
            disabled={
              loading
              ||
              running
            }
            onChange={
              event => {
                setCategory(
                  event.target.value,
                )

                setPreview(
                  null,
                )
              }
            }
          >
            {catalog.categories
              .length ===
              0 && (
              <option value="general">
                general
              </option>
            )}

            {catalog.categories.map(
              item => (
                <option
                  key={
                    item
                  }
                  value={
                    item
                  }
                >
                  {
                    item
                  }
                </option>
              ),
            )}
          </select>
        </label>

        <label>
          Grupo opcional

          <select
            value={
              teamId
            }
            disabled={
              loading
              ||
              running
            }
            onChange={
              event => {
                setTeamId(
                  event.target.value,
                )

                setPreview(
                  null,
                )
              }
            }
          >
            <option value="">
              Automático
            </option>

            {catalog.teams.map(
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
          Prioridad ticket

          <select
            value={
              priority
            }
            disabled={
              running
            }
            onChange={
              event =>
                setPriority(
                  event.target.value,
                )
            }
          >
            <option value="low">
              Baja
            </option>

            <option value="medium">
              Media
            </option>

            <option value="high">
              Alta
            </option>

            <option value="critical">
              Crítica
            </option>
          </select>
        </label>

        <button
          type="button"
          className="hdrs__run"
          disabled={
            running
            ||
            loading
            ||
            !siteId
          }
          onClick={
            () =>
              void simulate()
          }
        >
          <Route
            size={16}
          />

          {
            running
              ? 'Evaluando…'
              : 'Simular routing'
          }
        </button>
      </div>

      <p
        className="hdrs__note"
      >
        La prioridad del ticket afecta
        principalmente el SLA. El motor de
        routing actual decide por cobertura,
        categoría, grupo, permisos,
        disponibilidad, turno y capacidad.
      </p>

      {preview && (
        <div
          className={
            preview.canAssign
              ? 'hdrs__result hdrs__result--ok'
              : 'hdrs__result hdrs__result--fail'
          }
        >
          <header>
            {
              preview.canAssign
                ? (
                  <CheckCircle2
                    size={21}
                  />
                )
                : (
                  <AlertTriangle
                    size={21}
                  />
                )
            }

            <div>
              <strong>
                {
                  preview.canAssign
                    ? 'Asignación disponible'
                    : 'No se puede autoasignar'
                }
              </strong>

              <span>
                {
                  preview.reason
                }
              </span>
            </div>
          </header>

          {preview.canAssign && (
            <div
              className="hdrs__result-grid"
            >
              <div>
                <span>
                  Grupo
                </span>

                <strong>
                  {
                    preview.teamName
                    ??
                    '—'
                  }
                </strong>
              </div>

              <div>
                <span>
                  Técnico
                </span>

                <strong>
                  <UserRound
                    size={13}
                  />

                  {
                    preview.technicianName
                    ??
                    '—'
                  }
                </strong>
              </div>

              <div>
                <span>
                  Cobertura
                </span>

                <strong>
                  {
                    preview.coverageLocation
                    ??
                    '—'
                  }
                </strong>
              </div>

              <div>
                <span>
                  Ubicación técnico
                </span>

                <strong>
                  {
                    preview.technicianLocation
                    ??
                    '—'
                  }
                </strong>
              </div>

              <div>
                <span>
                  Carga actual
                </span>

                <strong>
                  {
                    preview.openTickets
                    ??
                    0
                  }
                  {' / '}
                  {
                    preview.capacity
                    ??
                    0
                  }
                </strong>
              </div>

              <div>
                <span>
                  Capacidad libre
                </span>

                <strong>
                  {
                    preview.remainingCapacity
                    ??
                    0
                  }
                </strong>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  )
}