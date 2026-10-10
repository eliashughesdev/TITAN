
import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion, useReducedMotion } from 'motion/react'
import {
  Activity,
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Clock3,
  Cpu,
  Database,
  Fingerprint,
  HardDrive,
  MapPin,
  Network,
  RefreshCw,
  Search,
  Server,
  Settings2,
  ShieldCheck,
  Users,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'

import { titanFetch, readJson } from '../lib/api'

type Device = {
  name: string
  ip?: string | null
  port?: number | null
  location?: string | null
  online?: boolean
  configured?: boolean
  latency_ms?: number | null
  punches_today?: number | null
  punches_total?: number | null
  last_fecha?: string | null
  last_entrada?: string | null

  // Optional future contract fields.
  // Not currently returned by the verified health endpoint.
  firmware?: string | null
  serial_number?: string | null
  model?: string | null
  users_count?: number | null
  registered_users?: number | null
  platform?: string | null
}

type DeviceHealthResponse = {
  total?: number
  online?: number
  offline?: number
  items?: Device[]
}

const numberFormatter = new Intl.NumberFormat('es-DO')

function fmt(value?: number | null) {
  return numberFormatter.format(value ?? 0)
}

function displayValue(value?: string | null) {
  return value?.trim() || 'No disponible'
}

function formatLastRecord(device: Device) {
  const raw = [
    device.last_fecha,
    device.last_entrada,
  ].filter(Boolean).join(' ').trim()

  if (!raw) return 'Sin registros disponibles'

  const date = new Date(raw)

  if (Number.isNaN(date.getTime())) return raw

  return date.toLocaleString('es-DO', {
    dateStyle: 'short',
    timeStyle: 'short',
  })
}

async function fetchDevices(
  signal?: AbortSignal,
): Promise<DeviceHealthResponse> {
  // Endpoint directo, confirmado en PonchesController.cs.
  // No utilizar authFetch(), que agrega el prefijo legado.
  const response = await titanFetch(
    '/api/ponches/device-health',
    { signal },
  )

  const data = await readJson<DeviceHealthResponse>(response)

  if (!data || !Array.isArray(data.items)) {
    throw new Error(
      'El servidor no devolvió un inventario válido de relojes.',
    )
  }

  return data
}

function InfoItem({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode
  label: string
  value: string
}) {
  return (
    <div className="min-w-0 rounded-xl border border-slate-100 bg-slate-50/70 px-3 py-2.5">
      <span className="flex items-center gap-1.5 text-[10px] font-medium text-slate-500">
        {icon}
        {label}
      </span>

      <strong
        className="mt-1.5 block break-words text-xs font-bold text-slate-800"
        title={value}
      >
        {value}
      </strong>
    </div>
  )
}

function KpiCard({
  label,
  value,
  detail,
  icon,
  color,
  surface,
}: {
  label: string
  value: string
  detail: string
  icon: React.ReactNode
  color: string
  surface: string
}) {
  const reducedMotion = useReducedMotion()

  return (
    <motion.article
      initial={reducedMotion ? false : { opacity: 0, y: 9 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={reducedMotion ? undefined : { y: -3 }}
      className="relative min-w-0 overflow-hidden rounded-[23px] border border-slate-200/80 p-5 shadow-[0_10px_32px_rgba(20,35,70,0.05)]"
      style={{
        background: `linear-gradient(145deg, ${surface} 0%, white 78%)`,
      }}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="text-[10px] font-extrabold uppercase tracking-[0.11em] text-slate-500">
          {label}
        </span>

        <span
          className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white shadow-sm"
          style={{ color }}
        >
          {icon}
        </span>
      </div>

      <strong className="mt-3 block text-3xl font-black tracking-tight text-slate-900">
        {value}
      </strong>

      <p className="mt-1 text-xs text-slate-500">
        {detail}
      </p>
    </motion.article>
  )
}

function DeviceCard({
  device,
}: {
  device: Device
}) {
  const reducedMotion = useReducedMotion()
  const online = device.online === true
  const latency = device.latency_ms
  const highLatency = online && latency != null && latency > 1200
  const hasIssue = !online || highLatency

  const usersCount =
    device.registered_users ?? device.users_count

  return (
    <motion.article
      layout={!reducedMotion}
      initial={reducedMotion ? false : { opacity: 0, y: 9 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={reducedMotion ? undefined : { y: -3 }}
      transition={{ duration: reducedMotion ? 0 : 0.2 }}
      className={
        'flex min-w-0 flex-col overflow-hidden rounded-[23px] border bg-white ' +
        'shadow-[0_10px_30px_rgba(17,34,68,0.055)] transition-shadow ' +
        'hover:shadow-[0_18px_42px_rgba(17,34,68,0.11)] ' +
        (hasIssue
          ? 'border-rose-200/90'
          : 'border-slate-200/80')
      }
    >
      <div
        className={
          'h-1.5 w-full ' +
          (!online
            ? 'bg-gradient-to-r from-rose-600 to-orange-400'
            : highLatency
              ? 'bg-gradient-to-r from-amber-500 to-orange-400'
              : 'bg-gradient-to-r from-teal-500 to-emerald-400')
        }
      />

      <div className="flex flex-1 flex-col p-5">
        <div className="flex items-start justify-between gap-3">
          <span
            className={
              'flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl ' +
              (online
                ? 'bg-emerald-50 text-emerald-600'
                : 'bg-rose-50 text-rose-600')
            }
          >
            {online
              ? <Wifi size={23} />
              : <WifiOff size={23} />}
          </span>

          <span
            className={
              'inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 ' +
              'text-[10px] font-extrabold ' +
              (!online
                ? 'bg-rose-100 text-rose-700'
                : highLatency
                  ? 'bg-amber-100 text-amber-700'
                  : 'bg-emerald-100 text-emerald-700')
            }
          >
            <span
              className={
                'h-1.5 w-1.5 rounded-full ' +
                (!online
                  ? 'bg-rose-500'
                  : highLatency
                    ? 'bg-amber-500'
                    : 'bg-emerald-500')
              }
            />
            {!online
              ? 'Offline'
              : highLatency
                ? 'Alta latencia'
                : 'Online'}
          </span>
        </div>

        <h3 className="mt-4 min-h-[42px] text-base font-extrabold leading-snug text-slate-900">
          {device.name}
        </h3>

        <p className="mt-1 flex items-center gap-1.5 text-xs text-slate-500">
          <MapPin size={13} />
          {displayValue(device.location)}
        </p>

        <div className="mt-4 grid grid-cols-2 gap-2">
          <InfoItem
            icon={<Network size={13} />}
            label="Dirección IP"
            value={
              device.ip
                ? `${device.ip}${device.port ? `:${device.port}` : ''}`
                : 'No disponible'
            }
          />

          <InfoItem
            icon={<Activity size={13} />}
            label="Latencia"
            value={
              !online
                ? 'Sin conexión'
                : latency == null
                  ? 'No medida'
                  : `${latency} ms`
            }
          />

          <InfoItem
            icon={<Fingerprint size={13} />}
            label="Ponches de hoy"
            value={
              device.punches_today == null
                ? 'No disponible'
                : fmt(device.punches_today)
            }
          />

          <InfoItem
            icon={<Database size={13} />}
            label="Ponches históricos"
            value={
              device.punches_total == null
                ? 'No disponible'
                : fmt(device.punches_total)
            }
          />

          <InfoItem
            icon={<Users size={13} />}
            label="Usuarios registrados"
            value={
              usersCount == null
                ? 'Pendiente de consulta'
                : fmt(usersCount)
            }
          />

          <InfoItem
            icon={<Cpu size={13} />}
            label="Firmware"
            value={
              device.firmware || 'Pendiente de consulta'
            }
          />

          <InfoItem
            icon={<Server size={13} />}
            label="Modelo"
            value={displayValue(device.model)}
          />

          <InfoItem
            icon={<HardDrive size={13} />}
            label="Número de serie"
            value={displayValue(device.serial_number)}
          />
        </div>

        <div className="mt-4 rounded-xl border border-slate-100 bg-slate-50/70 p-3">
          <span className="flex items-center gap-2 text-[10px] font-semibold text-slate-500">
            <Clock3 size={14} />
            Última marcación conocida
          </span>

          <strong className="mt-1 block text-xs text-slate-800">
            {formatLastRecord(device)}
          </strong>
        </div>

        <div className="mt-auto flex items-center justify-between gap-2 border-t border-slate-100 pt-4">
          <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600">
            {online
              ? <CheckCircle2 size={15} className="text-emerald-500" />
              : <AlertTriangle size={15} className="text-rose-500" />}
            {online ? 'Comunicación activa' : 'Sin comunicación'}
          </span>

          <span className="text-[11px] font-medium text-slate-400">
            {device.configured === false
              ? 'Sin configurar'
              : 'Inventario BioTime'}
          </span>
        </div>
      </div>
    </motion.article>
  )
}

export default function Devices() {
  const [search, setSearch] = useState('')
  const [filter, setFilter] =
    useState<'all' | 'online' | 'attention'>('all')

  const query = useQuery({
    queryKey: ['ponches', 'device-health'],
    queryFn: ({ signal }) => fetchDevices(signal),
    staleTime: 15_000,
    refetchInterval: 45_000,
    refetchOnWindowFocus: false,
  })

  const items = useMemo(
    () => query.data?.items ?? [],
    [query.data?.items],
  )

  const total = query.data?.total ?? items.length
  const online = query.data?.online ??
    items.filter(item => item.online === true).length
  const offline = query.data?.offline ??
    Math.max(total - online, 0)

  const attention = useMemo(
    () => items.filter(
      item =>
        item.online !== true ||
        (item.latency_ms != null && item.latency_ms > 1200),
    ),
    [items],
  )

  const availability = total > 0
    ? Math.round((online / total) * 100)
    : 0

  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase()

    return items.filter(item => {
      if (
        filter === 'online' &&
        item.online !== true
      ) {
        return false
      }

      if (
        filter === 'attention' &&
        item.online === true &&
        !(item.latency_ms != null && item.latency_ms > 1200)
      ) {
        return false
      }

      if (!needle) return true

      return [
        item.name,
        item.ip,
        item.location,
        item.serial_number,
        item.model,
      ].some(
        value =>
          String(value ?? '')
            .toLowerCase()
            .includes(needle),
      )
    })
  }, [items, search, filter])

  return (
    <div className="min-w-0 space-y-5 pb-10">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <span className="text-[10px] font-extrabold uppercase tracking-[0.17em] text-rose-600">
            Ponches / Infraestructura biométrica
          </span>

          <h1 className="mt-2 text-2xl font-black tracking-tight text-slate-900 sm:text-3xl">
            Inventario de relojes ZKTeco
          </h1>

          <p className="mt-2 text-sm text-slate-500">
            Estado operativo, conectividad, registros y
            ficha técnica de la flota biométrica.
          </p>
        </div>

        <button
          type="button"
          onClick={() => void query.refetch()}
          disabled={query.isFetching}
          className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-rose-600 bg-rose-600 px-4 py-2 text-xs font-bold text-white shadow-lg shadow-rose-600/15 transition hover:bg-rose-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-500 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <RefreshCw
            size={16}
            className={query.isFetching ? 'animate-spin' : ''}
          />
          Actualizar inventario
        </button>
      </header>

      {query.isError && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700"
        >
          <AlertCircle size={20} className="shrink-0" />

          <div>
            <strong className="block">Error de comunicación</strong>
            <p className="mt-1">
              {query.error instanceof Error
                ? query.error.message
                : 'No fue posible consultar los relojes.'}
            </p>
          </div>
        </div>
      )}

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          label="Total de relojes"
          value={fmt(total)}
          detail="Inventario reportado"
          icon={<Server size={21} />}
          color="#2563eb"
          surface="#eff6ff"
        />

        <KpiCard
          label="Dispositivos online"
          value={fmt(online)}
          detail={`${availability}% de la flota`}
          icon={<Wifi size={21} />}
          color="#059669"
          surface="#ecfdf5"
        />

        <KpiCard
          label="Dispositivos offline"
          value={fmt(offline)}
          detail="Sin comunicación"
          icon={<WifiOff size={21} />}
          color="#e11d48"
          surface="#fff1f2"
        />

        <KpiCard
          label="Requieren atención"
          value={fmt(attention.length)}
          detail="Offline o latencia elevada"
          icon={<AlertTriangle size={21} />}
          color="#d97706"
          surface="#fffbeb"
        />
      </section>

      <section className="rounded-[23px] border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
        <div className="flex flex-wrap items-center gap-4">
          <div className="relative min-w-[220px] flex-1">
            <Search
              size={17}
              className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
            />

            <input
              type="search"
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder="Buscar por nombre, IP, modelo o ubicación..."
              aria-label="Buscar relojes"
              className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pl-10 pr-10 text-sm outline-none focus:border-rose-400 focus:ring-2 focus:ring-rose-100"
            />

            {search && (
              <button
                type="button"
                aria-label="Limpiar búsqueda"
                onClick={() => setSearch('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400"
              >
                <X size={16} />
              </button>
            )}
          </div>

          <div className="inline-flex rounded-xl bg-slate-100 p-1">
            {([
              ['all', 'Todos'],
              ['online', 'Online'],
              ['attention', 'Atención'],
            ] as const).map(([value, label]) => (
              <button
                key={value}
                type="button"
                onClick={() => setFilter(value)}
                aria-pressed={filter === value}
                className={
                  'rounded-lg px-3 py-2 text-xs font-bold transition ' +
                  (filter === value
                    ? 'bg-white text-rose-700 shadow-sm'
                    : 'text-slate-500 hover:text-slate-800')
                }
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <div className="h-2 min-w-[140px] flex-1 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full bg-gradient-to-r from-teal-500 to-emerald-400 transition-all duration-500"
              style={{ width: `${availability}%` }}
            />
          </div>

          <span className="text-xs font-bold text-slate-700">
            {availability}% disponible
          </span>

          <span className="text-xs text-slate-400">
            {filtered.length} de {total} relojes
          </span>
        </div>
      </section>

      {query.isLoading && items.length === 0 ? (
        <div
          role="status"
          className="grid gap-4 md:grid-cols-2 xl:grid-cols-3"
        >
          {Array.from({ length: 6 }, (_, index) => (
            <div
              key={index}
              className="h-[380px] animate-pulse rounded-[23px] bg-slate-200/60"
            />
          ))}
          <span className="sr-only">Consultando dispositivos</span>
        </div>
      ) : filtered.length === 0 ? (
        <div className="rounded-[23px] border border-slate-200 bg-white p-12 text-center">
          <Settings2
            size={35}
            className="mx-auto text-slate-300"
          />
          <strong className="mt-3 block text-sm text-slate-700">
            No hay relojes para mostrar
          </strong>
          <p className="mt-2 text-xs text-slate-500">
            {query.isError
              ? 'El servicio no devolvió información. Revisa el error anterior.'
              : 'Ajusta los filtros o comprueba el inventario.'}
          </p>
        </div>
      ) : (
        <section className="grid items-stretch gap-4 md:grid-cols-2 xl:grid-cols-3">
          {filtered.map((device, index) => (
            <DeviceCard
              key={`${device.ip ?? device.name}-${index}`}
              device={device}
            />
          ))}
        </section>
      )}

      <footer className="flex flex-wrap items-center justify-between gap-3 px-1 text-xs text-slate-500">
        <span className="inline-flex items-center gap-2">
          <ShieldCheck size={15} className="text-emerald-500" />
          Consulta de solo lectura
        </span>

        <span className="inline-flex items-center gap-2">
          <ArrowRight size={14} />
          Fuente: TitanMDM / Python / BioTime
        </span>
      </footer>
    </div>
  )
}
