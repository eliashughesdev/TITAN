import {
  useMemo,
  useState,
} from 'react'

import {
  Activity,
  AlertTriangle,
  ArrowDownRight,
  ArrowRight,
  ArrowUpRight,
  Building2,
  CheckCircle2,
  Clock3,
  Database,
  Gauge,
  LogIn,
  LogOut,
  MonitorSmartphone,
  Pause,
  Radio,
  RefreshCw,
  Search,
  Server,
  Users,
  Wifi,
  WifiOff,
} from 'lucide-react'

import {
  useSearchParams,
} from 'react-router-dom'

import {
  useQuery,
} from '@tanstack/react-query'

import {
  ponchesDashboardQueryOptions,
} from '../lib/dashboardQueries'

import {
  Btn,
  PageHeader,
} from '../ui/kit'

type MetricTone =
  | 'default'
  | 'green'
  | 'amber'
  | 'red'
  | 'blue'

type MetricCardProps = {
  label: string
  value:
    | string
    | number
  subtitle: string
  icon:
    React.ReactNode
  tone?:
    MetricTone
  onClick?:
    () => void
}

function MetricCard({
  label,
  value,
  subtitle,
  icon,
  tone = 'default',
  onClick,
}: MetricCardProps) {
  const tones:
    Record<
      MetricTone,
      string
    > = {
      default:
        'bg-white border-zinc-200',

      green:
        'bg-emerald-50/70 border-emerald-100',

      amber:
        'bg-amber-50/70 border-amber-100',

      red:
        'bg-rose-50/70 border-rose-100',

      blue:
        'bg-sky-50/70 border-sky-100',
    }

  const content = (
    <>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[10px] font-bold uppercase tracking-[0.08em] text-zinc-500">
            {label}
          </p>

          <p className="mt-1.5 text-2xl font-black tracking-tight text-zinc-900">
            {value}
          </p>

          <p className="mt-1 truncate text-[11px] text-zinc-500">
            {subtitle}
          </p>
        </div>

        <div className="shrink-0 rounded-xl bg-white p-2 text-[#c8102e] shadow-sm ring-1 ring-black/5">
          {icon}
        </div>
      </div>

      {onClick && (
        <div className="mt-3 flex items-center justify-end text-[10px] font-semibold text-[#c8102e] opacity-0 transition-opacity group-hover:opacity-100">
          Ver detalle
          <ArrowRight
            size={12}
            className="ml-1"
          />
        </div>
      )}
    </>
  )

  const className =
    `group rounded-2xl border p-4 text-left shadow-sm transition ${
      tones[
        tone
      ]
    } ${
      onClick
        ? 'cursor-pointer hover:-translate-y-0.5 hover:shadow-md'
        : ''
    }`

  if (
    onClick
  ) {
    return (
      <button
        type="button"
        className={
          className
        }
        onClick={
          onClick
        }
      >
        {content}
      </button>
    )
  }

  return (
    <article
      className={
        className
      }
    >
      {content}
    </article>
  )
}

function EmptyCompact({
  title,
  description,
}: {
  title: string
  description: string
}) {
  return (
    <div className="flex min-h-[82px] items-center justify-center rounded-xl border border-dashed border-zinc-200 bg-zinc-50/60 px-4 py-5 text-center">
      <div>
        <p className="text-sm font-semibold text-zinc-700">
          {title}
        </p>

        <p className="mt-1 text-xs text-zinc-400">
          {description}
        </p>
      </div>
    </div>
  )
}

function percentChange(
  current: number,
  previous: number,
) {
  if (
    previous <= 0
  ) {
    return null
  }

  return Math.round(
    (
      (
        current -
        previous
      ) /
      previous
    ) *
      100,
  )
}

function formatDateTime(
  value?:
    | string
    | null,
) {
  if (
    !value
  ) {
    return '—'
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
    return value
  }

  return date
    .toLocaleString()
}

function formatClock(
  value?:
    | string
    | null,
) {
  if (
    !value
  ) {
    return '—'
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
    return value
  }

  return date
    .toLocaleTimeString(
      [],
      {
        hour:
          '2-digit',

        minute:
          '2-digit',
      },
    )
}

export default function Dashboard() {
  const [
    ,
    setSearchParams,
  ] =
    useSearchParams()

  const [
    autoRefresh,
    setAutoRefresh,
  ] =
    useState(
      true,
    )

  const [
    search,
    setSearch,
  ] =
    useState(
      '',
    )

  const {
    data:
      snapshot,

    error,

    isLoading,

    isFetching,

    refetch,
  } =
    useQuery({
      ...ponchesDashboardQueryOptions(),

      /*
       * Actualización silenciosa.
       *
       * Los datos actuales NO desaparecen mientras
       * llega el siguiente snapshot.
       */
      refetchInterval:
        autoRefresh
          ? 30_000
          : false,
    })

  function open(
    section: string,
  ) {
    setSearchParams({
      section,
    })
  }

  const onlinePercent =
    snapshot &&
    snapshot.devicesTotal >
      0
      ? Math.round(
          (
            snapshot.devicesOnline /
            snapshot.devicesTotal
          ) *
            100,
        )
      : 0

  const attendanceDelta =
    snapshot
      ? percentChange(
          snapshot.punchesToday,
          snapshot.punchesYesterday,
        )
      : null

  const employeeDelta =
    snapshot
      ? percentChange(
          snapshot.employeesToday,
          snapshot.employeesYesterday,
        )
      : null

  const peakHour =
    useMemo(
      () => {
        const rows =
          snapshot?.byHour ??
          []

        if (
          rows.length ===
          0
        ) {
          return null
        }

        return [
          ...rows,
        ].sort(
          (
            a,
            b,
          ) =>
            b.total -
            a.total,
        )[0]
      },
      [
        snapshot,
      ],
    )

  const maxHour =
    Math.max(
      1,
      ...(
        snapshot?.byHour ??
        []
      ).map(
        item =>
          item.total,
      ),
    )

  const maxDepartment =
    Math.max(
      1,
      ...(
        snapshot?.byDepartment ??
        []
      ).map(
        item =>
          item.total,
      ),
    )

  const filteredPunches =
    useMemo(
      () => {
        const term =
          search
            .trim()
            .toLowerCase()

        if (
          !term
        ) {
          return (
            snapshot
              ?.recentPunches ??
            []
          )
        }

        return (
          snapshot
            ?.recentPunches ??
          []
        ).filter(
          item =>
            [
              item.codigo,
              item.nombre,
              item.departamento,
              item.dispositivo_origen,
            ]
              .filter(
                Boolean,
              )
              .some(
                value =>
                  String(
                    value,
                  )
                    .toLowerCase()
                    .includes(
                      term,
                    ),
              ),
        )
      },
      [
        snapshot,
        search,
      ],
    )

  const problematicDevices =
    useMemo(
      () =>
        (
          snapshot
            ?.healthItems ??
          []
        )
          .filter(
            item =>
              !item.online ||
              (
                item.latencyMs !=
                  null &&
                item.latencyMs >
                  300
              ),
          )
          .sort(
            (
              a,
              b,
            ) =>
              Number(
                a.online,
              ) -
              Number(
                b.online,
              ),
          ),
      [
        snapshot,
      ],
    )

  const warningsCount =
    snapshot
      ?.warnings.length ??
    0

  if (
    isLoading &&
    !snapshot
  ) {
    return (
      <div className="flex min-h-[45vh] flex-col items-center justify-center gap-4">
        <RefreshCw
          size={32}
          className="animate-spin text-[#c8102e]"
        />

        <div className="text-center">
          <p className="font-semibold text-zinc-800">
            Preparando Centro Operativo
          </p>

          <p className="mt-1 text-sm text-zinc-500">
            Recuperando el primer snapshot operacional.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        kicker="Ponches · Centro Operativo"
        title="Dashboard General"
        subtitle={
          snapshot
            ? `Actualizado ${formatDateTime(
                snapshot.generatedAt,
              )}`
            : 'Resumen operacional del módulo biométrico.'
        }
        actions={
          <div className="flex flex-wrap gap-2">
            <Btn
              tone="ghost"
              onClick={() =>
                setAutoRefresh(
                  value =>
                    !value,
                )
              }
            >
              {autoRefresh
                ? (
                  <Radio
                    size={16}
                  />
                )
                : (
                  <Pause
                    size={16}
                  />
                )}

              {autoRefresh
                ? 'En vivo'
                : 'Pausado'}
            </Btn>

            <Btn
              tone="primary"
              disabled={
                isFetching
              }
              onClick={() =>
                void refetch()
              }
            >
              <RefreshCw
                size={16}
                className={
                  isFetching
                    ? 'animate-spin'
                    : ''
                }
              />

              Actualizar
            </Btn>
          </div>
        }
      />

      {error && (
        <div className="flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
          <AlertTriangle
            size={16}
          />

          No se pudo obtener la última actualización.
          Se conservaron los datos disponibles en caché.
        </div>
      )}

      {snapshot
        ?.warnings &&
        snapshot
          .warnings
          .length >
          0 && (
          <div className="flex flex-wrap gap-2">
            {snapshot
              .warnings
              .slice(
                0,
                4,
              )
              .map(
                warning => (
                  <button
                    type="button"
                    key={
                      warning
                    }
                    onClick={() =>
                      open(
                        'devices',
                      )
                    }
                    className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-medium text-amber-800"
                  >
                    {warning}
                  </button>
                ),
              )}
          </div>
        )}

      {/*
       * ======================================================
       * KPIs
       * ======================================================
       */}

      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-8">
        <MetricCard
          label="Ponches hoy"
          value={
            snapshot
              ?.punchesToday ??
            0
          }
          subtitle={
            attendanceDelta ==
              null
              ? 'Actividad registrada'
              : `${
                  attendanceDelta >=
                  0
                    ? '+'
                    : ''
                }${attendanceDelta}% vs ayer`
          }
          icon={
            attendanceDelta !=
              null &&
            attendanceDelta <
              0
              ? (
                <ArrowDownRight
                  size={19}
                />
              )
              : (
                <ArrowUpRight
                  size={19}
                />
              )
          }
          tone="blue"
          onClick={() =>
            open(
              'records',
            )
          }
        />

        <MetricCard
          label="Colaboradores"
          value={
            snapshot
              ?.employeesToday ??
            0
          }
          subtitle={
            employeeDelta ==
              null
              ? 'Registrados hoy'
              : `${
                  employeeDelta >=
                  0
                    ? '+'
                    : ''
                }${employeeDelta}% vs ayer`
          }
          icon={
            <Users
              size={19}
            />
          }
          onClick={() =>
            open(
              'collaborators',
            )
          }
        />

        <MetricCard
          label="Entradas"
          value={
            snapshot
              ?.entriesToday ??
            0
          }
          subtitle="Entradas de hoy"
          icon={
            <LogIn
              size={19}
            />
          }
          tone="green"
          onClick={() =>
            open(
              'records',
            )
          }
        />

        <MetricCard
          label="Salidas"
          value={
            snapshot
              ?.exitsToday ??
            0
          }
          subtitle="Salidas de hoy"
          icon={
            <LogOut
              size={19}
            />
          }
          onClick={() =>
            open(
              'records',
            )
          }
        />

        <MetricCard
          label="Turnos abiertos"
          value={
            snapshot
              ?.openShifts ??
            0
          }
          subtitle="Entrada sin salida"
          icon={
            <Clock3
              size={19}
            />
          }
          tone={
            (
              snapshot
                ?.openShifts ??
              0
            ) >
            0
              ? 'amber'
              : 'green'
          }
          onClick={() =>
            open(
              'records',
            )
          }
        />

        <MetricCard
          label="Relojes online"
          value={
            `${
              snapshot
                ?.devicesOnline ??
              0
            }/${
              snapshot
                ?.devicesTotal ??
              0
            }`
          }
          subtitle={`${onlinePercent}% disponible`}
          icon={
            <Wifi
              size={19}
            />
          }
          tone="green"
          onClick={() =>
            open(
              'devices',
            )
          }
        />

        <MetricCard
          label="Relojes offline"
          value={
            snapshot
              ?.devicesOffline ??
            0
          }
          subtitle="Requieren atención"
          icon={
            <WifiOff
              size={19}
            />
          }
          tone={
            (
              snapshot
                ?.devicesOffline ??
              0
            ) >
            0
              ? 'red'
              : 'green'
          }
          onClick={() =>
            open(
              'devices',
            )
          }
        />

        <MetricCard
          label="Alertas"
          value={
            warningsCount
          }
          subtitle={
            snapshot
              ?.databaseOnline
              ? 'Estado operacional'
              : 'Revisar BioTime'
          }
          icon={
            <AlertTriangle
              size={19}
            />
          }
          tone={
            warningsCount >
              0
              ? 'amber'
              : 'green'
          }
          onClick={() =>
            open(
              'devices',
            )
          }
        />
      </section>

      {/*
       * ======================================================
       * INFRAESTRUCTURA + ACTIVIDAD
       * ======================================================
       */}

      <section className="grid items-start gap-4 xl:grid-cols-[1.55fr_.75fr]">
        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h2 className="font-semibold text-zinc-900">
                Actividad por hora
              </h2>

              <p className="text-sm text-zinc-500">
                Distribución de ponches durante el día.
              </p>
            </div>

            <Activity className="text-[#c8102e]" />
          </div>

          {(
            snapshot
              ?.byHour ??
            []
          ).length ===
          0 ? (
            <div className="mt-4">
              <EmptyCompact
                title="Sin actividad suficiente"
                description="El gráfico aparecerá cuando existan registros horarios."
              />
            </div>
          ) : (
            <>
              <div className="mt-5 flex h-40 items-end gap-2">
                {snapshot?.byHour.map(
                  item => (
                    <button
                      type="button"
                      key={
                        item.hour
                      }
                      onClick={() =>
                        open(
                          'records',
                        )
                      }
                      className="group flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-1"
                      title={`${String(
                        item.hour,
                      ).padStart(
                        2,
                        '0',
                      )}:00 · ${
                        item.total
                      } ponches`}
                    >
                      <span className="text-[10px] font-semibold text-zinc-500 opacity-0 group-hover:opacity-100">
                        {
                          item.total
                        }
                      </span>

                      <div
                        className="w-full rounded-t-md bg-gradient-to-t from-[#991b2f] to-[#e11d48]"
                        style={{
                          height:
                            `${Math.max(
                              6,
                              (
                                item.total /
                                maxHour
                              ) *
                                115,
                            )}px`,
                        }}
                      />

                      <span className="text-[9px] text-zinc-400">
                        {String(
                          item.hour,
                        ).padStart(
                          2,
                          '0',
                        )}
                      </span>
                    </button>
                  ),
                )}
              </div>

              <div className="mt-3 flex items-center justify-between border-t border-zinc-100 pt-3 text-xs text-zinc-500">
                <span>
                  Hora pico
                </span>

                <strong className="text-zinc-900">
                  {peakHour
                    ? `${String(
                        peakHour.hour,
                      ).padStart(
                        2,
                        '0',
                      )}:00 · ${
                        peakHour.total
                      } registros`
                    : '—'}
                </strong>
              </div>
            </>
          )}
        </article>

        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <div className="mb-4 flex items-center gap-3">
            <Server className="text-[#c8102e]" />

            <div>
              <h2 className="font-semibold">
                Infraestructura
              </h2>

              <p className="text-xs text-zinc-500">
                TitanMDM → Python → BioTime
              </p>
            </div>
          </div>

          <div className="space-y-2">
            <StatusRow
              label="Gateway Python"
              online={
                snapshot
                  ?.serviceOnline ??
                false
              }
              icon={
                <Server
                  size={16}
                />
              }
            />

            <StatusRow
              label="Base BioTime"
              online={
                snapshot
                  ?.databaseOnline ??
                false
              }
              icon={
                <Database
                  size={16}
                />
              }
            />
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2 border-t border-zinc-100 pt-4">
            <div className="rounded-xl bg-zinc-50 p-3">
              <p className="text-[10px] uppercase tracking-wide text-zinc-400">
                Último snapshot
              </p>

              <p className="mt-1 text-sm font-bold">
                {formatClock(
                  snapshot
                    ?.generatedAt,
                )}
              </p>
            </div>

            <button
              type="button"
              onClick={() =>
                open(
                  'sync-history',
                )
              }
              className="rounded-xl bg-zinc-50 p-3 text-left transition hover:bg-rose-50"
            >
              <p className="text-[10px] uppercase tracking-wide text-zinc-400">
                Sincronización
              </p>

              <p className="mt-1 text-sm font-bold text-[#c8102e]">
                Ver historial
              </p>
            </button>
          </div>
        </article>
      </section>

      {/*
       * ======================================================
       * DEPARTAMENTOS + DISPOSITIVOS PROBLEMÁTICOS
       * ======================================================
       */}

      <section className="grid items-start gap-4 xl:grid-cols-2">
        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="font-semibold">
                Ponches por departamento
              </h2>

              <p className="text-sm text-zinc-500">
                Distribución organizacional.
              </p>
            </div>

            <Building2 className="text-[#c8102e]" />
          </div>

          {(
            snapshot
              ?.byDepartment ??
            []
          ).length ===
          0 ? (
            <div className="mt-4">
              <EmptyCompact
                title="Sin distribución departamental"
                description="No existen datos departamentales para mostrar."
              />
            </div>
          ) : (
            <div className="mt-5 space-y-3">
              {snapshot
                ?.byDepartment
                .slice(
                  0,
                  6,
                )
                .map(
                  item => (
                    <button
                      type="button"
                      key={
                        item.name
                      }
                      onClick={() =>
                        open(
                          'records',
                        )
                      }
                      className="block w-full text-left"
                    >
                      <div className="mb-1 flex items-center justify-between text-xs">
                        <span className="truncate font-semibold">
                          {
                            item.name
                          }
                        </span>

                        <span className="text-zinc-500">
                          {
                            item.total
                          }
                        </span>
                      </div>

                      <div className="h-2 overflow-hidden rounded-full bg-zinc-100">
                        <div
                          className="h-full rounded-full bg-[#c8102e]"
                          style={{
                            width:
                              `${Math.max(
                                3,
                                (
                                  item.total /
                                  maxDepartment
                                ) *
                                  100,
                              )}%`,
                          }}
                        />
                      </div>
                    </button>
                  ),
                )}
            </div>
          )}
        </article>

        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="font-semibold">
                Relojes que requieren atención
              </h2>

              <p className="text-sm text-zinc-500">
                Offline o con latencia elevada.
              </p>
            </div>

            <button
              type="button"
              onClick={() =>
                open(
                  'devices',
                )
              }
              className="flex items-center gap-1 text-sm font-semibold text-[#c8102e]"
            >
              Ver todos

              <ArrowRight
                size={14}
              />
            </button>
          </div>

          {problematicDevices.length ===
          0 ? (
            <div className="mt-4">
              <div className="flex min-h-[82px] items-center gap-3 rounded-xl border border-emerald-100 bg-emerald-50/70 p-4">
                <CheckCircle2
                  size={22}
                  className="text-emerald-600"
                />

                <div>
                  <p className="text-sm font-semibold text-emerald-900">
                    Red biométrica estable
                  </p>

                  <p className="text-xs text-emerald-700">
                    No hay relojes offline ni con latencia crítica.
                  </p>
                </div>
              </div>
            </div>
          ) : (
            <div className="mt-4 divide-y divide-zinc-100">
              {problematicDevices
                .slice(
                  0,
                  5,
                )
                .map(
                  item => (
                    <button
                      type="button"
                      key={
                        item.name
                      }
                      onClick={() =>
                        open(
                          'devices',
                        )
                      }
                      className="flex w-full items-center justify-between gap-3 py-3 text-left hover:bg-zinc-50"
                    >
                      <div className="flex min-w-0 items-center gap-3">
                        <div
                          className={
                            `rounded-xl p-2 ${
                              item.online
                                ? 'bg-amber-50 text-amber-600'
                                : 'bg-rose-50 text-rose-600'
                            }`
                          }
                        >
                          {item.online
                            ? (
                              <Wifi
                                size={16}
                              />
                            )
                            : (
                              <WifiOff
                                size={16}
                              />
                            )}
                        </div>

                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">
                            {
                              item.name
                            }
                          </p>

                          <p className="text-xs text-zinc-500">
                            {
                              item.punchesToday
                            } ponches hoy
                          </p>
                        </div>
                      </div>

                      <div className="text-right text-xs">
                        <p
                          className={
                            item.online
                              ? 'font-semibold text-amber-600'
                              : 'font-semibold text-rose-600'
                          }
                        >
                          {item.online
                            ? 'Latencia'
                            : 'Offline'}
                        </p>

                        <p className="text-zinc-400">
                          {item.latencyMs ==
                          null
                            ? '—'
                            : `${item.latencyMs} ms`}
                        </p>
                      </div>
                    </button>
                  ),
                )}
            </div>
          )}
        </article>
      </section>

      {/*
       * ======================================================
       * PONCHES RECIENTES
       * ======================================================
       */}

      <article className="rounded-2xl border border-zinc-200 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 className="font-semibold">
              Ponches recientes
            </h2>

            <p className="text-xs text-zinc-500">
              Últimos registros recibidos.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <div className="relative">
              <Search
                size={15}
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
                placeholder="Buscar..."
                className="w-56 rounded-xl border border-zinc-200 py-2 pl-9 pr-3 text-sm outline-none focus:border-[#c8102e]"
              />
            </div>

            <button
              type="button"
              onClick={() =>
                open(
                  'records',
                )
              }
              className="rounded-xl border border-zinc-200 px-3 py-2 text-sm font-semibold hover:bg-zinc-50"
            >
              Historial
            </button>
          </div>
        </div>

        {filteredPunches.length ===
        0 ? (
          <div className="p-4">
            <EmptyCompact
              title="Sin registros recientes"
              description="Los últimos ponches aparecerán en esta sección."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[850px] text-left text-sm">
              <thead className="bg-zinc-50 text-[10px] uppercase tracking-wide text-zinc-500">
                <tr>
                  <th className="px-4 py-3">
                    Código
                  </th>

                  <th className="px-4 py-3">
                    Colaborador
                  </th>

                  <th className="px-4 py-3">
                    Departamento
                  </th>

                  <th className="px-4 py-3">
                    Entrada
                  </th>

                  <th className="px-4 py-3">
                    Salida
                  </th>

                  <th className="px-4 py-3">
                    Dispositivo
                  </th>
                </tr>
              </thead>

              <tbody className="divide-y divide-zinc-100">
                {filteredPunches
                  .slice(
                    0,
                    8,
                  )
                  .map(
                    (
                      punch,
                      index,
                    ) => (
                      <tr
                        key={
                          punch.id ??
                          `${punch.codigo}-${index}`
                        }
                        className="cursor-pointer hover:bg-zinc-50"
                        onClick={() =>
                          open(
                            'records',
                          )
                        }
                      >
                        <td className="px-4 py-3 font-mono text-xs">
                          {punch.codigo ||
                            '—'}
                        </td>

                        <td className="px-4 py-3 font-semibold">
                          {punch.nombre ||
                            'Sin identificar'}
                        </td>

                        <td className="px-4 py-3 text-zinc-600">
                          {punch.departamento ||
                            'Sin departamento'}
                        </td>

                        <td className="px-4 py-3">
                          {formatDateTime(
                            punch.entrada,
                          )}
                        </td>

                        <td className="px-4 py-3">
                          {formatDateTime(
                            punch.salida,
                          )}
                        </td>

                        <td className="px-4 py-3 text-zinc-500">
                          {punch.dispositivo_origen ||
                            '—'}
                        </td>
                      </tr>
                    ),
                  )}
              </tbody>
            </table>
          </div>
        )}
      </article>

      {/*
       * ======================================================
       * ACCIONES
       * ======================================================
       */}

      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <QuickAction
          title="Ponches"
          description="Consultar historial"
          icon={
            <Activity
              size={18}
            />
          }
          onClick={() =>
            open(
              'records',
            )
          }
        />

        <QuickAction
          title="Relojes"
          description="Estado biométrico"
          icon={
            <MonitorSmartphone
              size={18}
            />
          }
          onClick={() =>
            open(
              'devices',
            )
          }
        />

        <QuickAction
          title="Colaboradores"
          description="Personal registrado"
          icon={
            <Users
              size={18}
            />
          }
          onClick={() =>
            open(
              'collaborators',
            )
          }
        />

        <QuickAction
          title="Sincronización"
          description="Modo espejo"
          icon={
            <RefreshCw
              size={18}
            />
          }
          onClick={() =>
            open(
              'mirror',
            )
          }
        />

        <QuickAction
          title="Reportes"
          description="Análisis operativo"
          icon={
            <Gauge
              size={18}
            />
          }
          onClick={() =>
            open(
              'reports',
            )
          }
        />
      </section>
    </div>
  )
}

function StatusRow({
  label,
  online,
  icon,
}: {
  label: string
  online: boolean
  icon:
    React.ReactNode
}) {
  return (
    <div className="flex items-center justify-between rounded-xl border border-zinc-100 bg-zinc-50/70 px-3 py-2.5">
      <div className="flex items-center gap-3">
        <span className="text-zinc-500">
          {icon}
        </span>

        <span className="text-sm font-medium">
          {label}
        </span>
      </div>

      <span
        className={
          `flex items-center gap-1 rounded-full px-2 py-1 text-[10px] font-bold ${
            online
              ? 'bg-emerald-100 text-emerald-700'
              : 'bg-rose-100 text-rose-700'
          }`
        }
      >
        {online
          ? (
            <CheckCircle2
              size={11}
            />
          )
          : (
            <AlertTriangle
              size={11}
            />
          )}

        {online
          ? 'Operativo'
          : 'Revisar'}
      </span>
    </div>
  )
}

function QuickAction({
  title,
  description,
  icon,
  onClick,
}: {
  title: string
  description: string
  icon:
    React.ReactNode
  onClick:
    () => void
}) {
  return (
    <button
      type="button"
      onClick={
        onClick
      }
      className="group rounded-2xl border border-zinc-200 bg-white p-4 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-rose-200 hover:shadow-md"
    >
      <div className="flex items-center justify-between">
        <span className="text-[#c8102e]">
          {icon}
        </span>

        <ArrowUpRight
          size={15}
          className="text-zinc-300 transition group-hover:text-[#c8102e]"
        />
      </div>

      <p className="mt-3 text-sm font-semibold text-zinc-900">
        {title}
      </p>

      <p className="mt-1 text-xs text-zinc-500">
        {description}
      </p>
    </button>
  )
}