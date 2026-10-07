import {
  useMemo,
  useState,
} from 'react'

import {
  AlertTriangle,
  RefreshCw,
  Search,
  Server,
  Wifi,
  WifiOff,
} from 'lucide-react'

import {
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

type Device = {
  name: string

  ip?:
    | string
    | null

  port?:
    | number
    | null

  location?:
    | string
    | null

  online?: boolean

  latency_ms?:
    | number
    | null

  punches_today?:
    | number
    | null

  punches_total?:
    | number
    | null

  last_fecha?:
    | string
    | null

  last_entrada?:
    | string
    | null

  configured?: boolean
}

type DeviceHealthResponse = {
  total?: number
  online?: number
  offline?: number
  items?: Device[]
}

async function loadDevices(
  signal?: AbortSignal,
): Promise<DeviceHealthResponse> {
  const response =
    await titanFetch(
      '/api/ponches/device-health',
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
      DeviceHealthResponse & {
        detail?: string
        message?: string
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

  return data
}

export default function Devices() {
  const [
    search,
    setSearch,
  ] =
    useState(
      '',
    )

  const query =
    useQuery({
      queryKey: [
        'ponches',
        'device-health',
      ],

      queryFn:
        ({
          signal,
        }) =>
          loadDevices(
            signal,
          ),

      staleTime:
        10_000,

      gcTime:
        5 * 60_000,

      refetchInterval:
        30_000,

      refetchOnWindowFocus:
        false,
    })

  const items =
    query.data?.items ??
    []

  const total =
    query.data?.total ??
    items.length

  const online =
    query.data?.online ??
    items.filter(
      item =>
        item.online,
    ).length

  const offline =
    query.data?.offline ??
    Math.max(
      0,
      total -
        online,
    )

  const availability =
    total > 0
      ? Math.round(
          (
            online /
            total
          ) *
            100,
        )
      : 0

  const attention =
    useMemo(
      () =>
        items.filter(
          item =>
            !item.online ||
            (
              item.latency_ms ??
              0
            ) > 500,
        ),
      [
        items,
      ],
    )

  const filtered =
    useMemo(
      () => {
        const normalized =
          search
            .trim()
            .toLowerCase()

        if (!normalized) {
          return items
        }

        return items.filter(
          item =>
            [
              item.name,
              item.ip,
              item.location,
            ]
              .filter(
                Boolean,
              )
              .join(
                ' ',
              )
              .toLowerCase()
              .includes(
                normalized,
              ),
        )
      },
      [
        items,
        search,
      ],
    )

  return (
    <div className="space-y-5 page-enter">
      <PageHeader
        kicker="Infraestructura biométrica"
        title="Dispositivos"
        subtitle="Estado operativo, latencia y actividad de los relojes biométricos."
        actions={
          <Btn
            tone="primary"
            disabled={
              query.isFetching
            }
            onClick={() =>
              void query.refetch()
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

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric
          title="Relojes"
          value={
            total
          }
          detail="Inventario"
        />

        <Metric
          title="Online"
          value={
            online
          }
          detail={`${availability}% disponible`}
        />

        <Metric
          title="Offline"
          value={
            offline
          }
          detail="Requieren revisión"
        />

        <Metric
          title="Atención"
          value={
            attention.length
          }
          detail="Offline o alta latencia"
        />
      </div>

      {query.error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
          {query.error instanceof Error
            ? query.error.message
            : 'No se pudo obtener el estado de los dispositivos.'}
        </div>
      )}

      <Panel>
        <div className="mb-4 flex flex-col gap-4 lg:flex-row lg:items-center">
          <div className="relative flex-1">
            <Search
              size={16}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400"
            />

            <input
              value={
                search
              }
              onChange={
                event =>
                  setSearch(
                    event.target.value,
                  )
              }
              placeholder="Buscar por nombre, IP o ubicación"
              className="w-full rounded-xl border border-zinc-200 py-2.5 pl-9 pr-3 text-sm"
            />
          </div>

          <div className="flex items-center gap-3 rounded-xl bg-zinc-50 px-4 py-2">
            <div
              className="h-2.5 w-32 overflow-hidden rounded-full bg-zinc-200"
            >
              <div
                className="h-full rounded-full bg-emerald-500 transition-all"
                style={{
                  width:
                    `${availability}%`,
                }}
              />
            </div>

            <strong className="text-sm">
              {availability}%
            </strong>
          </div>
        </div>

        <div className="max-h-[590px] overflow-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 z-10 bg-white">
              <tr className="border-b text-left text-[11px] uppercase tracking-wide text-zinc-500">
                <th className="py-3">
                  Dispositivo
                </th>

                <th>
                  Red
                </th>

                <th>
                  Estado
                </th>

                <th>
                  Latencia
                </th>

                <th>
                  Ponches hoy
                </th>

                <th>
                  Último registro
                </th>
              </tr>
            </thead>

            <tbody>
              {query.isLoading && (
                <tr>
                  <td
                    colSpan={6}
                    className="py-12 text-center text-zinc-400"
                  >
                    Consultando relojes…
                  </td>
                </tr>
              )}

              {!query.isLoading &&
                filtered.length ===
                  0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-12 text-center text-zinc-400"
                    >
                      No hay dispositivos disponibles.
                    </td>
                  </tr>
                )}

              {filtered.map(
                device => {
                  const latencyWarning =
                    (
                      device
                        .latency_ms ??
                      0
                    ) > 500

                  return (
                    <tr
                      key={
                        `${device.name}-${device.ip ?? ''}`
                      }
                      className="border-b border-zinc-100 transition hover:bg-zinc-50"
                    >
                      <td className="py-3">
                        <div className="flex items-center gap-3">
                          <div
                            className={
                              `grid h-9 w-9 place-items-center rounded-xl ${
                                device.online
                                  ? 'bg-emerald-50 text-emerald-600'
                                  : 'bg-rose-50 text-rose-500'
                              }`
                            }
                          >
                            {device.online
                              ? (
                                <Wifi
                                  size={17}
                                />
                              )
                              : (
                                <WifiOff
                                  size={17}
                                />
                              )}
                          </div>

                          <div>
                            <div className="font-semibold text-zinc-800">
                              {
                                device.name
                              }
                            </div>

                            <div className="text-xs text-zinc-400">
                              {device.location ||
                                'Sin ubicación'}
                            </div>
                          </div>
                        </div>
                      </td>

                      <td className="font-mono text-xs text-zinc-600">
                        {device.ip ||
                          'Sin IP'}

                        {device.port
                          ? `:${device.port}`
                          : ''}
                      </td>

                      <td>
                        <span
                          className={
                            `rounded-full px-2.5 py-1 text-xs font-semibold ${
                              device.online
                                ? 'bg-emerald-50 text-emerald-700'
                                : 'bg-rose-50 text-rose-700'
                            }`
                          }
                        >
                          {device.online
                            ? 'Online'
                            : 'Offline'}
                        </span>
                      </td>

                      <td>
                        {!device.online
                          ? '—'
                          : latencyWarning
                            ? (
                              <span className="inline-flex items-center gap-1 text-amber-700">
                                <AlertTriangle
                                  size={13}
                                />

                                {
                                  device.latency_ms
                                } ms
                              </span>
                            )
                            : `${device.latency_ms ?? '—'} ms`}
                      </td>

                      <td className="font-semibold">
                        {device.punches_today ??
                          0}
                      </td>

                      <td className="text-xs text-zinc-500">
                        {device.last_fecha ||
                          '—'}

                        {' '}

                        {device.last_entrada ||
                          ''}
                      </td>
                    </tr>
                  )
                },
              )}
            </tbody>
          </table>
        </div>
      </Panel>

      {attention.length >
        0 && (
        <Panel>
          <div className="mb-3 flex items-center gap-2">
            <Server
              size={18}
              className="text-rose-600"
            />

            <h3 className="font-bold">
              Requieren atención
            </h3>
          </div>

          <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
            {attention.map(
              device => (
                <div
                  key={
                    `attention-${device.name}`
                  }
                  className="rounded-xl border border-zinc-200 bg-zinc-50 p-3"
                >
                  <strong className="text-sm">
                    {
                      device.name
                    }
                  </strong>

                  <p className="mt-1 text-xs text-zinc-500">
                    {!device.online
                      ? 'Sin comunicación'
                      : `Latencia ${device.latency_ms ?? 0} ms`}
                  </p>
                </div>
              ),
            )}
          </div>
        </Panel>
      )}
    </div>
  )
}

function Metric({
  title,
  value,
  detail,
}: {
  title: string
  value: number
  detail: string
}) {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
      <p className="text-xs font-semibold uppercase tracking-wide text-zinc-500">
        {title}
      </p>

      <p className="mt-2 text-3xl font-black text-zinc-900">
        {value}
      </p>

      <p className="mt-1 text-xs text-zinc-400">
        {detail}
      </p>
    </div>
  )
}