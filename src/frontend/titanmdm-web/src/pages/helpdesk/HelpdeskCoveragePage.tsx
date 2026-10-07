import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  Link,
} from 'react-router-dom'

import {
  AlertCircle,
  CheckCircle2,
  Download,
  MapPin,
  RefreshCw,
  Users,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskPages.css'
import './HelpdeskCoveragePage.css'

interface CoverageRow {
  zoneId: string
  zoneName: string
  zoneType: string
  category: string
  usersInZone: number
  ready: boolean
  availableAgents: number
  selectedTeam: string | null
  coveredByZone: string | null
  reason: string
}

interface Readiness {
  generatedAtUtc: string
  totalZones: number
  totalCategories: number
  readyCount: number
  uncoveredCount: number
  rows: CoverageRow[]
}

type CoverageFilter =
  | 'pending'
  | 'all'
  | 'ready'

const specialtiesUrl =
  '/helpdesk/especialidades?workspace=helpdesk'

function describePending(
  row: CoverageRow,
) {
  if (
    row.reason
  ) {
    return row.reason
  }

  return row.usersInZone ===
    0
    ? 'Todavía no hay usuarios vinculados a esta zona.'
    : 'Revisa grupo, agentes, turnos y capacidad.'
}

function csvCell(
  value:
    string
    |
    number,
) {
  return (
    `"${String(
      value,
    ).replaceAll(
      '"',
      '""',
    )}"`
  )
}

function exportPendingRows(
  rows: CoverageRow[],
) {
  const pending =
    rows.filter(
      row =>
        !row.ready,
    )

  if (
    pending.length ===
    0
  ) {
    return
  }

  const header = [
    'Zona',
    'Tipo de zona',
    'Categoría',
    'Usuarios en zona',
    'Agentes disponibles',
    'Motivo',
  ]

  const lines = [
    header
      .map(
        csvCell,
      )
      .join(
        ',',
      ),

    ...pending.map(
      row =>
        [
          row.zoneName,
          row.zoneType,
          row.category,
          row.usersInZone,
          row.availableAgents,
          describePending(
            row,
          ),
        ]
          .map(
            csvCell,
          )
          .join(
            ',',
          ),
    ),
  ]

  const blob =
    new Blob(
      [
        '\uFEFF',
        lines.join(
          '\r\n',
        ),
      ],
      {
        type:
          'text/csv;charset=utf-8',
      },
    )

  const url =
    URL.createObjectURL(
      blob,
    )

  const anchor =
    document.createElement(
      'a',
    )

  anchor.href =
    url

  anchor.download =
    'helpdesk-cobertura-pendiente.csv'

  document.body
    .appendChild(
      anchor,
    )

  anchor.click()

  anchor.remove()

  window.setTimeout(
    () =>
      URL.revokeObjectURL(
        url,
      ),
    1000,
  )
}

export function HelpdeskCoveragePage() {
  const [
    data,
    setData,
  ] =
    useState<Readiness | null>(
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
    error,
    setError,
  ] =
    useState(
      '',
    )

  const [
    filter,
    setFilter,
  ] =
    useState<CoverageFilter>(
      'pending',
    )

  const [
    search,
    setSearch,
  ] =
    useState(
      '',
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
          const response =
            await apiClient
              .get<Readiness>(
                '/helpdesk/coverage/readiness',
              )

          setData(
            response.data,
          )
        }
        catch {
          setError(
            'No se pudo consultar la cobertura de la Mesa de Ayuda.',
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
      void load()
    },
    [
      load,
    ],
  )

  const rows =
    data?.rows
    ??
    []

  const visible =
    useMemo(
      () => {
        const term =
          search
            .trim()
            .toLocaleLowerCase()

        return rows.filter(
          row => {
            if (
              filter ===
                'pending'
              &&
              row.ready
            ) {
              return false
            }

            if (
              filter ===
                'ready'
              &&
              !row.ready
            ) {
              return false
            }

            if (
              !term
            ) {
              return true
            }

            return [
              row.zoneName,
              row.zoneType,
              row.category,
              row.selectedTeam
              ??
              '',
              row.coveredByZone
              ??
              '',
              row.reason,
            ].some(
              value =>
                value
                  .toLocaleLowerCase()
                  .includes(
                    term,
                  ),
            )
          },
        )
      },
      [
        rows,
        filter,
        search,
      ],
    )

  const hasConfiguration =
    (
      data?.totalZones
      ??
      0
    ) >
      0
    &&
    (
      data?.totalCategories
      ??
      0
    ) >
      0

  const coverageComplete =
    hasConfiguration
    &&
    (
      data?.readyCount
      ??
      0
    ) >
      0
    &&
    data?.uncoveredCount ===
      0

  const zonesWithoutUsers =
    useMemo(
      () =>
        new Set(
          rows
            .filter(
              row =>
                row.usersInZone ===
                0,
            )
            .map(
              row =>
                row.zoneId,
            ),
        ).size,
      [
        rows,
      ],
    )

  return (
    <main
      className="titan-page helpdesk-page hdcov"
    >
      <header
        className="hdcov__hero"
      >
        <div>
          <span
            className="hdcov__eyebrow"
          >
            <MapPin
              size={14}
            />

            Configuración · Mesa de ayuda
          </span>

          <h1>
            Cobertura de asignación
          </h1>

          <p>
            Comprueba qué localidades,
            sublocalidades y categorías
            pueden recibir una asignación
            automática antes de habilitar
            el flujo de correo.
          </p>
        </div>

        <button
          type="button"
          className="helpdesk-ui-button helpdesk-ui-button--secondary"
          onClick={
            () =>
              void load()
          }
          disabled={
            loading
          }
        >
          <RefreshCw
            size={16}
          />

          {
            loading
              ? 'Actualizando…'
              : 'Actualizar'
          }
        </button>
      </header>

      <section
        className="hdcov__metrics"
      >
        <article
          className="hdcov__metric"
        >
          <span>
            Localidades / zonas
          </span>

          <strong>
            {
              data?.totalZones
              ??
              0
            }
          </strong>
        </article>

        <article
          className="hdcov__metric"
        >
          <span>
            Categorías
          </span>

          <strong>
            {
              data?.totalCategories
              ??
              0
            }
          </strong>
        </article>

        <article
          className="hdcov__metric"
        >
          <span>
            Combinaciones listas
          </span>

          <strong>
            {
              data?.readyCount
              ??
              0
            }
          </strong>
        </article>

        <article
          className="hdcov__metric"
        >
          <span>
            Pendientes
          </span>

          <strong>
            {
              data?.uncoveredCount
              ??
              0
            }
          </strong>
        </article>

        <article
          className="hdcov__metric"
        >
          <span>
            Zonas sin usuarios
          </span>

          <strong>
            {
              zonesWithoutUsers
            }
          </strong>
        </article>
      </section>

      {error && (
        <div
          className="helpdesk-inbox__error"
          role="alert"
        >
          <AlertCircle
            size={17}
          />

          {error}
        </div>
      )}

      {!loading &&
        !error && (
        <section
          className={
            'hdcov__readiness ' +
            (
              coverageComplete
                ? 'hdcov__readiness--ready'
                : 'hdcov__readiness--pending'
            )
          }
        >
          <div
            className="hdcov__readiness-header"
          >
            <span
              className="hdcov__readiness-icon"
            >
              {
                coverageComplete
                  ? (
                    <CheckCircle2
                      size={20}
                    />
                  )
                  : (
                    <AlertCircle
                      size={20}
                    />
                  )
              }
            </span>

            <div>
              <h2>
                {
                  coverageComplete
                    ? 'Cobertura configurada'
                    : 'Configuración pendiente'
                }
              </h2>

              <p>
                {
                  coverageComplete
                    ? 'Todas las combinaciones evaluadas disponen actualmente de al menos un técnico elegible.'
                    : 'Completa localidades, categorías, grupos, técnicos, capacidad y turnos antes de activar la recepción automática.'
                }
              </p>
            </div>
          </div>

          <div
            className="hdcov__readiness-actions"
          >
            <Link
              to={
                specialtiesUrl
              }
              className="helpdesk-ui-button helpdesk-ui-button--primary"
            >
              Configurar grupos y técnicos
            </Link>
          </div>
        </section>
      )}

      <section
        className="hdcov__card"
      >
        <header
          className="hdcov__card-header"
        >
          <div>
            <h2>
              Estado por localidad y categoría
            </h2>

            <p>
              Cada fila representa una
              combinación que TitanMDM
              debe ser capaz de enrutar.
            </p>
          </div>
        </header>

        <div
          className="hdcov__toolbar"
        >
          <input
            aria-label="Buscar en cobertura"
            placeholder="Buscar localidad, categoría o grupo"
            value={
              search
            }
            onChange={
              event =>
                setSearch(
                  event.target.value,
                )
            }
          />

          <select
            aria-label="Filtrar cobertura"
            value={
              filter
            }
                  onChange={(event) =>
                setFilter(
                  event.target.value as CoverageFilter,
                )
              }
          >
            <option value="pending">
              Solo pendientes
            </option>

            <option value="all">
              Todas
            </option>

            <option value="ready">
              Solo listas
            </option>
          </select>

          <button
            type="button"
            className="helpdesk-ui-button helpdesk-ui-button--secondary"
            onClick={
              () =>
                exportPendingRows(
                  rows,
                )
            }
            disabled={
              loading
              ||
              !data
              ||
              data.uncoveredCount ===
                0
            }
          >
            <Download
              size={16}
            />

            Exportar pendientes
          </button>
        </div>

        {loading ? (
          <div
            className="hdcov__empty"
            role="status"
          >
            Cargando cobertura…
          </div>
        ) : rows.length ===
          0 ? (
          <div
            className="hdcov__empty"
          >
            Aún no hay localidades y
            categorías suficientes para
            evaluar cobertura.
          </div>
        ) : visible.length ===
          0 ? (
          <div
            className="hdcov__empty"
          >
            No hay resultados para los
            filtros actuales.
          </div>
        ) : (
          <div
            className="hdcov__table-wrap"
          >
            <table
              className="hdcov__table"
            >
              <thead>
                <tr>
                  <th>
                    Localidad
                  </th>

                  <th>
                    Categoría
                  </th>

                  <th>
                    Usuarios
                  </th>

                  <th>
                    Estado
                  </th>

                  <th>
                    Grupo / motivo
                  </th>

                  <th>
                    Acción
                  </th>
                </tr>
              </thead>

              <tbody>
                {visible.map(
                  row => (
                    <tr
                      key={
                        `${row.zoneId}:${row.category}`
                      }
                    >
                      <td>
                        <div
                          className="hdcov__zone"
                        >
                          <strong>
                            {
                              row.zoneName
                            }
                          </strong>

                          <small>
                            {
                              row.zoneType
                            }
                          </small>
                        </div>
                      </td>

                      <td>
                        {
                          row.category
                        }
                      </td>

                      <td>
                        <span
                          className="hdcov__users"
                        >
                          <Users
                            size={14}
                          />

                          {
                            row.usersInZone
                          }
                        </span>
                      </td>

                      <td>
                        {row.ready ? (
                          <span
                            className="hdcov__status hdcov__status--ready"
                          >
                            <CheckCircle2
                              size={14}
                            />

                            {
                              row.availableAgents
                            }
                            {' '}
                            disponible(s)
                          </span>
                        ) : (
                          <span
                            className="hdcov__status hdcov__status--pending"
                          >
                            <AlertCircle
                              size={14}
                            />

                            Pendiente
                          </span>
                        )}
                      </td>

                      <td>
                        <div
                          className="hdcov__reason"
                        >
                          {
                            row.ready
                              ? (
                                <>
                                  {
                                    row.selectedTeam
                                    ??
                                    'Grupo disponible'
                                  }

                                  {' · '}

                                  {
                                    row.coveredByZone
                                    ??
                                    row.zoneName
                                  }
                                </>
                              )
                              : describePending(
                                  row,
                                )
                          }
                        </div>
                      </td>

                      <td>
                        <Link
                          to={
                            specialtiesUrl
                          }
                          className="hdcov__configure"
                        >
                          Configurar
                        </Link>
                      </td>
                    </tr>
                  ),
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  )
}