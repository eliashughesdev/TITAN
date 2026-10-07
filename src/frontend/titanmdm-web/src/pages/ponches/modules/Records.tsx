import {
  useMemo,
  useState,
} from 'react'

import {
  RefreshCw,
  Search,
} from 'lucide-react'

import {
  keepPreviousData,
  useQuery,
} from '@tanstack/react-query'

import {
  titanFetch,
} from '../lib/api'

import {
  Btn,
  PageHeader,
  Panel,
} from '../ui/kit'

type PunchRow = {
  id:
    | number
    | string

  codigo: string

  nombre?:
    | string
    | null

  departamento?:
    | string
    | null

  fecha?:
    | string
    | null

  entrada?:
    | string
    | null

  salida?:
    | string
    | null

  dispositivo_origen?:
    | string
    | null
}

type RecordsResponse = {
  items?: PunchRow[]
}

function formatTime(
  value?:
    | string
    | null,
) {
  if (!value) {
    return '—'
  }

  return String(
    value,
  ).slice(
    0,
    8,
  )
}

function formatDate(
  value?:
    | string
    | null,
) {
  if (!value) {
    return '—'
  }

  return String(
    value,
  ).slice(
    0,
    10,
  )
}

async function getRecords(
  search: string,
  fecha: string,
  dispositivo: string,
  limit: number,
  signal?: AbortSignal,
): Promise<PunchRow[]> {
  const params =
    new URLSearchParams({
      limit:
        String(
          limit,
        ),
    })

  if (
    search.trim()
  ) {
    params.set(
      'search',
      search.trim(),
    )
  }

  if (
    fecha
  ) {
    params.set(
      'fecha',
      fecha,
    )
  }

  if (
    dispositivo &&
    dispositivo !==
      'todos'
  ) {
    params.set(
      'dispositivo',
      dispositivo,
    )
  }

  const response =
    await titanFetch(
      `/api/ponches/records?${params.toString()}`,
      {
        signal,
      },
    )

  const data =
    await response
      .json()
      .catch(
        () =>
          ({}),
      ) as
      RecordsResponse & {
        message?: string
        detail?: string
      }

  if (
    !response.ok
  ) {
    throw new Error(
      data.detail ??
      data.message ??
      `Error ${response.status}`,
    )
  }

  return data.items ??
    []
}

export default function Records() {
  const [
    searchInput,
    setSearchInput,
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
    fecha,
    setFecha,
  ] =
    useState(
      '',
    )

  const [
    dispositivo,
    setDispositivo,
  ] =
    useState(
      'todos',
    )

  const [
    limit,
    setLimit,
  ] =
    useState(
      100,
    )

  const query =
    useQuery({
      queryKey: [
        'ponches',
        'records',
        search,
        fecha,
        dispositivo,
        limit,
      ],

      queryFn:
        ({
          signal,
        }) =>
          getRecords(
            search,
            fecha,
            dispositivo,
            limit,
            signal,
          ),

      staleTime:
        15_000,

      gcTime:
        5 * 60_000,

      placeholderData:
        keepPreviousData,

      refetchOnWindowFocus:
        false,
    })

  const rows =
    query.data ??
    []

  const conSalida =
    useMemo(
      () =>
        rows.filter(
          row =>
            Boolean(
              row.salida,
            ),
        ).length,
      [
        rows,
      ],
    )

  const abiertos =
    rows.length -
    conSalida

  return (
    <div className="space-y-5 page-enter">
      <PageHeader
        kicker="Asistencia"
        title="Ponches"
        subtitle="Consulta operacional de asistencia"
        actions={
          <Btn
            tone="primary"
            onClick={() =>
              void query.refetch()
            }
            disabled={
              query.isFetching
            }
          >
            <RefreshCw
              size={16}
              className={
                query.isFetching
                  ? 'animate-spin'
                  : ''
              }
            />

            Actualizar
          </Btn>
        }
      />

      <div className="grid gap-3 sm:grid-cols-3">
        <Metric
          label="Registros"
          value={
            rows.length
          }
        />

        <Metric
          label="Con salida"
          value={
            conSalida
          }
        />

        <Metric
          label="Sin salida"
          value={
            abiertos
          }
        />
      </div>

      {query.error && (
        <p className="rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-700">
          {query.error instanceof Error
            ? query.error.message
            : 'No se pudieron cargar los ponches.'}
        </p>
      )}

      <Panel>
        <form
          onSubmit={
            event => {
              event.preventDefault()

              setSearch(
                searchInput.trim(),
              )
            }
          }
          className="mb-4 grid grid-cols-1 items-end gap-3 md:grid-cols-5"
        >
          <label className="text-[11px] font-semibold uppercase text-zinc-500 md:col-span-2">
            Código / nombre

            <span className="relative mt-1 block">
              <Search
                size={14}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400"
              />

              <input
                value={
                  searchInput
                }
                onChange={
                  event =>
                    setSearchInput(
                      event.target.value,
                    )
                }
                className="w-full rounded-xl border px-3 py-2.5 pl-8 text-sm"
                placeholder="Código o colaborador"
              />
            </span>
          </label>

          <label className="text-[11px] font-semibold uppercase text-zinc-500">
            Fecha

            <input
              type="date"
              value={
                fecha
              }
              onChange={
                event =>
                  setFecha(
                    event.target.value,
                  )
              }
              className="mt-1 w-full rounded-xl border px-3 py-2.5 text-sm"
            />
          </label>

          <label className="text-[11px] font-semibold uppercase text-zinc-500">
            Dispositivo

            <input
              value={
                dispositivo ===
                'todos'
                  ? ''
                  : dispositivo
              }
              onChange={
                event =>
                  setDispositivo(
                    event.target.value ||
                    'todos',
                  )
              }
              placeholder="Todos"
              className="mt-1 w-full rounded-xl border px-3 py-2.5 text-sm"
            />
          </label>

          <div className="flex gap-2">
            <select
              value={
                limit
              }
              onChange={
                event =>
                  setLimit(
                    Number(
                      event.target.value,
                    ),
                  )
              }
              className="rounded-xl border bg-white px-2 py-2.5 text-sm"
            >
              <option value={50}>
                50
              </option>

              <option value={100}>
                100
              </option>

              <option value={200}>
                200
              </option>

              <option value={500}>
                500
              </option>
            </select>

            <Btn
              type="submit"
              tone="primary"
            >
              Filtrar
            </Btn>
          </div>
        </form>

        <div className="max-h-[560px] overflow-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-[11px] uppercase text-zinc-500">
                <th className="py-2">
                  Código
                </th>

                <th>
                  Nombre
                </th>

                <th>
                  Departamento
                </th>

                <th>
                  Fecha
                </th>

                <th>
                  Entrada
                </th>

                <th>
                  Salida
                </th>

                <th>
                  Reloj
                </th>
              </tr>
            </thead>

            <tbody>
              {query.isLoading && (
                <tr>
                  <td
                    colSpan={7}
                    className="py-8 text-center text-zinc-400"
                  >
                    Cargando…
                  </td>
                </tr>
              )}

              {!query.isLoading &&
                rows.length ===
                  0 && (
                  <tr>
                    <td
                      colSpan={7}
                      className="py-8 text-center text-zinc-400"
                    >
                      Sin resultados
                    </td>
                  </tr>
                )}

              {rows.map(
                row => (
                  <tr
                    key={
                      row.id
                    }
                    className="border-t border-zinc-100 hover:bg-zinc-50"
                  >
                    <td className="py-2 font-mono text-xs">
                      {
                        row.codigo
                      }
                    </td>

                    <td className="font-medium">
                      {row.nombre ||
                        '—'}
                    </td>

                    <td className="text-zinc-500">
                      {row.departamento ||
                        '—'}
                    </td>

                    <td>
                      {formatDate(
                        row.fecha,
                      )}
                    </td>

                    <td>
                      <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700">
                        {formatTime(
                          row.entrada,
                        )}
                      </span>
                    </td>

                    <td>
                      <span
                        className={
                          `rounded-full px-2 py-0.5 text-xs font-semibold ${
                            row.salida
                              ? 'bg-sky-50 text-sky-700'
                              : 'bg-amber-50 text-amber-700'
                          }`
                        }
                      >
                        {formatTime(
                          row.salida,
                        )}
                      </span>
                    </td>

                    <td className="max-w-[180px] truncate text-xs text-zinc-500">
                      {row.dispositivo_origen ||
                        '—'}
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      </Panel>
    </div>
  )
}

function Metric({
  label,
  value,
}: {
  label: string
  value: number
}) {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <p className="text-xs uppercase tracking-wide text-zinc-500">
        {label}
      </p>

      <p className="mt-1 text-3xl font-black text-zinc-900">
        {value}
      </p>
    </div>
  )
}