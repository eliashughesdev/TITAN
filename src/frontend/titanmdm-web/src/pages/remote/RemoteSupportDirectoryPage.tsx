
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import { useNavigate } from 'react-router-dom'
import {
  Activity,
  ArrowUpRight,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  Computer,
  LoaderCircle,
  Monitor,
  Play,
  RefreshCw,
  Search,
  Shield,
  Users,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'

import { devicesApi } from '../../api/devicesApi'
import {
  createRemoteSession,
  listRemoteSessions,
  type RemoteSession,
} from '../../api/remoteSupportApi'

import type {
  DeviceListItem,
  DeviceListResult,
} from '../../types/device'

import { useAuth } from '../../auth/AuthContext'

const PAGE_SIZE = 25
const VIEWER_PATH = '/remote/viewer'

type FleetFilter = 'all' | 'online' | 'offline'

const terminalStatuses = new Set([
  'Completed',
  'Failed',
  'Expired',
  'Cancelled',
])

function isActive(session: RemoteSession): boolean {
  return !terminalStatuses.has(session.status)
}

function toDate(value?: string | null): string {
  if (!value) return 'Sin contacto'

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return 'Fecha no disponible'
  }

  return date.toLocaleString('es-DO', {
    dateStyle: 'short',
    timeStyle: 'short',
  })
}

function openViewerTab(sessionId: string, tab: Window | null) {
  const path = `${VIEWER_PATH}?sessionId=${encodeURIComponent(sessionId)}`

  if (tab && !tab.closed) {
    tab.location.replace(path)
  }
}

export function RemoteSupportDirectoryPage() {
  const navigate = useNavigate()
  const { user } = useAuth()

  const canManage =
    user?.permissions?.includes('remote.manage') ?? false

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<FleetFilter>('all')
  const [page, setPage] = useState(1)

  const [data, setData] = useState<DeviceListResult | null>(null)
  const [sessions, setSessions] = useState<RemoteSession[]>([])

  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [connectingId, setConnectingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [selectedDevice, setSelectedDevice] =
    useState<DeviceListItem | null>(null)

  const [reason, setReason] = useState(
    'Soporte técnico remoto autorizado',
  )
  const [minutes, setMinutes] = useState(120)
  const [allowMouse, setAllowMouse] = useState(true)
  const [allowKeyboard, setAllowKeyboard] = useState(true)

  const loadData = useCallback(async (
    signal?: AbortSignal,
    background = false,
  ) => {
    if (background) {
      setRefreshing(true)
    } else {
      setLoading(true)
    }

    try {
      const [fleet, active] = await Promise.all([
        devicesApi.getDevices({
          platform: 'Windows',
          managed: true,
          search: search || undefined,
          status:
            filter === 'all'
              ? undefined
              : filter === 'online'
                ? 'Online'
                : 'Offline',
          page,
          pageSize: PAGE_SIZE,
        }),
        listRemoteSessions(200),
      ])

      if (signal?.aborted) return

      setData(fleet)
      setSessions(active.filter(isActive))
      setError(null)
    } catch (err) {
      if (signal?.aborted) return

      setError(
        err instanceof Error
          ? err.message
          : 'No fue posible consultar los equipos.',
      )
    } finally {
      if (!signal?.aborted) {
        setLoading(false)
        setRefreshing(false)
      }
    }
  }, [search, page, filter])

  useEffect(() => {
    const controller = new AbortController()

    void loadData(controller.signal)

    return () => controller.abort()
  }, [loadData])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void loadData(undefined, true)
    }, 20000)

    return () => window.clearInterval(timer)
  }, [loadData])

  const devices = useMemo(
    () => (data?.items ?? []).filter(
      device => device.platform === 'Windows' && device.isManaged,
    ),
    [data],
  )

  const activeSessionByDevice = useMemo(() => {
    const map = new Map<string, RemoteSession>()

    for (const session of sessions) {
      if (!map.has(session.deviceId)) {
        map.set(session.deviceId, session)
      }
    }

    return map
  }, [sessions])

  const onlineOnPage = devices.filter(
    device => device.status === 'Online',
  ).length

  const totalPages = Math.max(
    1,
    data?.totalPages ?? 1,
  )

  const totalDevices = data?.totalCount ?? 0

  function submitSearch(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  function changeFilter(next: FleetFilter) {
    setFilter(next)
    setPage(1)
  }

  function viewSession(sessionId: string) {
    const path =
      `${VIEWER_PATH}?sessionId=${encodeURIComponent(sessionId)}`

    const tab = window.open(
      path,
      '_blank',
    )

    if (!tab) {
      navigate(path)
    }
  }

  async function connect(device: DeviceListItem) {
    if (!canManage || connectingId) return

    if (reason.trim().length < 3) {
      setError('Indica un motivo válido para la sesión.')
      return
    }

    // Se abre durante el clic para evitar el bloqueo del navegador.
    // No lleva tokens ni credenciales en la URL.
    const tab = window.open('about:blank', '_blank')

    try {
      setConnectingId(device.id)
      setError(null)

      const session = await createRemoteSession({
        deviceId: device.id,
        reason: reason.trim(),
        maximumDurationMinutes: minutes,
        allowMouse,
        allowKeyboard,
        allowClipboard: false,
        allowFileTransfer: false,
      })

      setSessions(current => [
        session,
        ...current.filter(item => item.id !== session.id),
      ])

      setSelectedDevice(null)

      if (tab) {
        openViewerTab(session.id, tab)
      } else {
        navigate(
          `${VIEWER_PATH}?sessionId=${encodeURIComponent(session.id)}`,
        )
      }
    } catch (err) {
      if (tab && !tab.closed) tab.close()

      setError(
        err instanceof Error
          ? err.message
          : 'No fue posible iniciar la sesión.',
      )
    } finally {
      setConnectingId(null)
    }
  }

  return (
    <main className="mx-auto w-full max-w-[1850px] space-y-5 pb-10">
      <header className="relative overflow-hidden rounded-[27px] bg-gradient-to-r from-[#15294e] via-[#2a447f] to-[#58529a] p-7 text-white shadow-xl shadow-indigo-950/10">
        <div className="flex flex-wrap items-start justify-between gap-5">
          <div>
            <span className="text-[10px] font-black uppercase tracking-[0.17em] text-indigo-200">
              TitanMDM / Windows Operations
            </span>

            <h1 className="mt-3 text-2xl font-black tracking-tight md:text-3xl">
              Centro de soporte remoto
            </h1>

            <p className="mt-2 max-w-2xl text-sm leading-6 text-indigo-100">
              Localiza estaciones de trabajo, administra sesiones
              activas y abre el visor en una pestaña independiente.
            </p>
          </div>

          <button
            type="button"
            onClick={() => void loadData(undefined, true)}
            disabled={refreshing}
            className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-white/20 bg-white px-4 py-2 text-xs font-extrabold text-indigo-800 shadow-md disabled:opacity-60"
          >
            <RefreshCw
              size={16}
              className={refreshing ? 'animate-spin' : ''}
            />
            Actualizar
          </button>
        </div>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[
          {
            label: 'Equipos encontrados',
            value: totalDevices,
            icon: <Computer size={21} />,
            tone: 'text-blue-600 bg-blue-50',
            note: 'Resultados de la consulta',
          },
          {
            label: 'Online en esta página',
            value: onlineOnPage,
            icon: <Wifi size={21} />,
            tone: 'text-emerald-600 bg-emerald-50',
            note: 'Equipos visibles conectados',
          },
          {
            label: 'Offline en esta página',
            value: devices.filter(
              x => x.status === 'Offline',
            ).length,
            icon: <WifiOff size={21} />,
            tone: 'text-rose-600 bg-rose-50',
            note: 'Equipos visibles desconectados',
          },
          {
            label: 'Sesiones activas',
            value: sessions.length,
            icon: <Activity size={21} />,
            tone: 'text-indigo-600 bg-indigo-50',
            note: 'Sesiones visibles autorizadas',
          },
        ].map(card => (
          <article
            key={card.label}
            className="rounded-[22px] border border-slate-200 bg-white p-5 shadow-sm"
          >
            <div className="flex items-center justify-between gap-3">
              <span className="text-[11px] font-bold text-slate-500">
                {card.label}
              </span>
              <span className={`rounded-xl p-2.5 ${card.tone}`}>
                {card.icon}
              </span>
            </div>
            <strong className="mt-2 block text-3xl font-black text-slate-900">
              {card.value.toLocaleString('es-DO')}
            </strong>
            <p className="mt-1 text-[11px] text-slate-500">
              {card.note}
            </p>
          </article>
        ))}
      </section>

      <section className="overflow-hidden rounded-[24px] border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 p-5">
          <div>
            <h2 className="text-base font-black text-slate-900">
              Equipos Windows
            </h2>
            <p className="mt-1 text-xs text-slate-500">
              Búsqueda paginada por nombre, usuario asignado o IP.
            </p>
          </div>

          <form
            onSubmit={submitSearch}
            className="flex w-full max-w-xl gap-2"
          >
            <div className="relative min-w-0 flex-1">
              <Search
                size={17}
                className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                value={searchInput}
                onChange={event => setSearchInput(event.target.value)}
                placeholder="Equipo, usuario o IP..."
                aria-label="Buscar equipo remoto"
                maxLength={120}
                className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pl-10 pr-10 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              />
              {searchInput && (
                <button
                  type="button"
                  onClick={() => {
                    setSearchInput('')
                    setSearch('')
                    setPage(1)
                  }}
                  aria-label="Limpiar búsqueda"
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400"
                >
                  <X size={16} />
                </button>
              )}
            </div>
            <button
              type="submit"
              className="rounded-xl bg-[#294b92] px-5 text-xs font-bold text-white shadow-sm"
            >
              Buscar
            </button>
          </form>
        </div>

        <div className="flex flex-wrap gap-2 border-b border-slate-100 px-5 py-3">
          {([
            ['all', 'Todos'],
            ['online', 'En línea'],
            ['offline', 'Desconectados'],
          ] as const).map(([key, label]) => (
            <button
              key={key}
              type="button"
              onClick={() => changeFilter(key)}
              aria-pressed={filter === key}
              className={
                'rounded-xl px-4 py-2 text-xs font-bold transition ' +
                (filter === key
                  ? 'bg-indigo-100 text-indigo-700'
                  : 'text-slate-500 hover:bg-slate-100')
              }
            >
              {label}
            </button>
          ))}
        </div>

        {error && (
          <div
            role="alert"
            className="m-4 flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700"
          >
            <Shield size={16} />
            {error}
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="w-full min-w-[1000px] text-left text-sm">
            <thead className="bg-slate-50 text-[10px] font-black uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-5 py-4">Equipo</th>
                <th className="px-5 py-4">Usuario asignado</th>
                <th className="px-5 py-4">IP / Modelo</th>
                <th className="px-5 py-4">Estado</th>
                <th className="px-5 py-4">Último contacto</th>
                <th className="px-5 py-4 text-right">Acción</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100">
              {devices.map(device => {
                const existing =
                  activeSessionByDevice.get(device.id)

                return (
                  <tr
                    key={device.id}
                    className="transition hover:bg-indigo-50/40"
                  >
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <span className="rounded-xl bg-indigo-50 p-3 text-indigo-600">
                          <Monitor size={19} />
                        </span>
                        <div>
                          <strong className="text-xs font-bold text-slate-900">
                            {device.deviceName}
                          </strong>
                          <span className="mt-1 block text-[11px] text-slate-500">
                            {device.operatingSystem ?? 'Windows'}
                          </span>
                        </div>
                      </div>
                    </td>

                    <td className="px-5 py-4 text-xs text-slate-700">
                      <span className="inline-flex items-center gap-2">
                        <Users size={14} className="text-slate-400" />
                        {device.assignedUser || 'Sin asignar'}
                      </span>
                    </td>

                    <td className="px-5 py-4 text-xs text-slate-600">
                      <strong className="block">
                        {device.ipAddress || 'Sin IP'}
                      </strong>
                      <span className="mt-1 block text-slate-400">
                        {device.manufacturer || 'Fabricante no reportado'}
                        {' · '}
                        {device.model || 'Modelo desconocido'}
                      </span>
                    </td>

                    <td className="px-5 py-4">
                      <span className={
                        'inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[10px] font-extrabold ' +
                        (device.status === 'Online'
                          ? 'bg-emerald-50 text-emerald-700'
                          : 'bg-slate-100 text-slate-600')
                      }>
                        {device.status === 'Online'
                          ? <CheckCircle2 size={13} />
                          : <Clock3 size={13} />}
                        {device.status}
                      </span>
                    </td>

                    <td className="px-5 py-4 text-xs text-slate-500">
                      {toDate(device.lastSeenAtUtc)}
                    </td>

                    <td className="px-5 py-4 text-right">
                      {existing ? (
                        <button
                          type="button"
                          onClick={() => viewSession(existing.id)}
                          className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-indigo-50 px-4 py-2 text-xs font-extrabold text-indigo-700 hover:bg-indigo-100"
                        >
                          Abrir sesión
                          <ArrowUpRight size={15} />
                        </button>
                      ) : (
                        <button
                          type="button"
                          disabled={
                            !canManage ||
                            connectingId !== null ||
                            device.status !== 'Online'
                          }
                          onClick={() => setSelectedDevice(device)}
                          className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#234d9c] px-4 py-2 text-xs font-extrabold text-white shadow-sm disabled:cursor-not-allowed disabled:opacity-40"
                        >
                          <Play size={14} />
                          Conectar
                        </button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>

          {!loading && devices.length === 0 && (
            <div className="p-12 text-center text-sm text-slate-500">
              No se encontraron equipos para esta consulta.
            </div>
          )}

          {loading && (
            <div className="flex items-center justify-center gap-2 p-6 text-sm text-slate-500">
              <LoaderCircle size={17} className="animate-spin" />
              Consultando inventario...
            </div>
          )}
        </div>

        <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-100 px-5 py-4">
          <span className="text-xs text-slate-500">
            Página {page} de {totalPages}
            {' · '}
            {totalDevices.toLocaleString('es-DO')} resultados
          </span>

          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1 || loading}
              onClick={() => setPage(p => Math.max(1, p - 1))}
              className="rounded-xl border border-slate-200 p-2.5 disabled:opacity-40"
              aria-label="Página anterior"
            >
              <ChevronLeft size={17} />
            </button>

            <button
              type="button"
              disabled={page >= totalPages || loading}
              onClick={() => setPage(p => p + 1)}
              className="rounded-xl border border-slate-200 p-2.5 disabled:opacity-40"
              aria-label="Página siguiente"
            >
              <ChevronRight size={17} />
            </button>
          </div>
        </footer>
      </section>

      <section className="rounded-[24px] border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-black text-slate-900">
          Sesiones remotas activas
        </h2>

        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {sessions.map(session => (
            <div
              key={session.id}
              className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 p-4"
            >
              <div className="min-w-0">
                <strong className="block truncate text-xs text-slate-800">
                  {session.technicianName}
                </strong>
                <span className="mt-1 block text-[11px] text-slate-500">
                  {session.status} · {session.deviceId.slice(0, 8)}
                </span>
              </div>

              <button
                type="button"
                onClick={() => viewSession(session.id)}
                className="rounded-xl bg-indigo-50 p-2.5 text-indigo-700"
                aria-label="Abrir visor"
              >
                <ArrowUpRight size={17} />
              </button>
            </div>
          ))}

          {!sessions.length && (
            <p className="text-sm text-slate-500">
              No hay sesiones activas.
            </p>
          )}
        </div>
      </section>

      {selectedDevice && (
        <div
          className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm"
          role="presentation"
          onMouseDown={event => {
            if (event.target === event.currentTarget) {
              setSelectedDevice(null)
            }
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="remote-dialog-title"
            className="w-full max-w-lg rounded-[26px] bg-white p-6 shadow-2xl"
          >
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2
                  id="remote-dialog-title"
                  className="text-lg font-black text-slate-900"
                >
                  Nueva sesión remota
                </h2>
                <p className="mt-1 text-xs text-slate-500">
                  {selectedDevice.deviceName}
                </p>
              </div>

              <button
                type="button"
                onClick={() => setSelectedDevice(null)}
                className="rounded-xl bg-slate-100 p-2"
                aria-label="Cerrar"
              >
                <X size={19} />
              </button>
            </div>

            <label className="mt-5 block text-xs font-bold text-slate-700">
              Motivo de soporte
              <textarea
                rows={3}
                maxLength={500}
                value={reason}
                onChange={event => setReason(event.target.value)}
                className="mt-2 w-full rounded-xl border border-slate-200 p-3 text-sm"
              />
            </label>

            <label className="mt-4 block text-xs font-bold text-slate-700">
              Duración máxima
              <select
                value={minutes}
                onChange={event => setMinutes(Number(event.target.value))}
                className="mt-2 w-full rounded-xl border border-slate-200 p-3"
              >
                <option value={30}>30 minutos</option>
                <option value={60}>1 hora</option>
                <option value={120}>2 horas</option>
                <option value={240}>4 horas</option>
              </select>
            </label>

            <div className="mt-4 flex gap-5">
              <label className="flex items-center gap-2 text-xs text-slate-700">
                <input
                  type="checkbox"
                  checked={allowMouse}
                  onChange={event => setAllowMouse(event.target.checked)}
                />
                Ratón
              </label>
              <label className="flex items-center gap-2 text-xs text-slate-700">
                <input
                  type="checkbox"
                  checked={allowKeyboard}
                  onChange={event => setAllowKeyboard(event.target.checked)}
                />
                Teclado
              </label>
            </div>

            <button
              type="button"
              onClick={() => void connect(selectedDevice)}
              disabled={connectingId !== null || reason.trim().length < 3}
              className="mt-6 flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[#274f9c] px-5 py-3 text-sm font-extrabold text-white shadow-md disabled:opacity-50"
            >
              {connectingId
                ? <LoaderCircle size={17} className="animate-spin" />
                : <Play size={16} />}
              Abrir visor remoto
              <ArrowUpRight size={16} />
            </button>

            <p className="mt-3 text-center text-[11px] text-slate-500">
              La sesión estará sujeta a permisos, auditoría y control exclusivo.
            </p>
          </section>
        </div>
      )}
    </main>
  )
}

export default RemoteSupportDirectoryPage
