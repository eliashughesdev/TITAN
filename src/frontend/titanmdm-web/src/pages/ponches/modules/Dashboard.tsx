
import { useMemo, useState } from 'react'
import type { ReactNode, CSSProperties } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion, useReducedMotion } from 'motion/react'

import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import {
  Activity,
  AlertTriangle,
  ArrowDownRight,
  ArrowRight,
  ArrowUpRight,
  BellRing,
  Building2,
  CheckCircle2,
  Clock3,
  Database,
  Fingerprint,
  LogIn,
  LogOut,
  Radio,
  RefreshCw,
  Search,
  Server,
  ShieldCheck,
  Sparkles,
  Users,
  Wifi,
  WifiOff,
} from 'lucide-react'

import type { DashboardSnapshot } from '../lib/dashboardApi'
import { ponchesDashboardQueryOptions } from '../lib/dashboardQueries'
import { PunchClockAttentionCenter } from '../components/PunchClockAttentionCenter'

const PALETTE = {
  red: '#c41e43',
  rose: '#e85c79',
  indigo: '#6366f1',
  teal: '#0d9488',
  amber: '#f59e0b',
  blue: '#0ea5e9',
  purple: '#8b5cf6',
}

const DEPARTMENT_COLORS = [
  PALETTE.red,
  PALETTE.rose,
  PALETTE.indigo,
  PALETTE.teal,
  PALETTE.amber,
  PALETTE.purple,
  PALETTE.blue,
]

const formatNumber = new Intl.NumberFormat('es-DO')

function fmt(value?: number | null): string {
  return formatNumber.format(value ?? 0)
}

function formatDate(value?: string | null): string {
  if (!value) return '—'

  const parsed = new Date(value)

  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleString('es-DO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

function formatPunchTime(value?: string | null): string {
  if (!value) return '—'

  // SQL may return a time without a calendar date.
  if (/^\d{1,2}:\d{2}(:\d{2})?$/.test(value)) {
    return value
  }

  const parsed = new Date(value)

  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleTimeString('es-DO', {
    hour: '2-digit',
    minute: '2-digit',
  })
}

function percentageChange(
  current: number,
  previous: number,
): number | null {
  if (previous <= 0) return null

  return Math.round(
    ((current - previous) / previous) * 100,
  )
}

function usableEmployeeName(
  name: string | null | undefined,
  code: string | undefined,
): string {
  const value = (name ?? '').trim()

  if (
    !value ||
    /^NN[-_ ]?\d+$/i.test(value) ||
    /^\d+$/.test(value) ||
    (
      value.length > 15 &&
      /^[a-z0-9+/]+={0,2}$/i.test(value)
    )
  ) {
    return code
      ? `Pendiente de identificar · ${code}`
      : 'Colaborador sin identificar'
  }

  return value
}

interface PremiumPanelProps {
  title: string
  eyebrow?: string
  description?: string
  icon: ReactNode
  action?: ReactNode
  children: ReactNode
  className?: string
}

function PremiumPanel({
  title,
  eyebrow,
  description,
  icon,
  action,
  children,
  className = '',
}: PremiumPanelProps) {
  return (
    <section
      className={
        'min-w-0 rounded-[26px] border border-slate-200/80 ' +
        'bg-white p-5 shadow-[0_12px_44px_rgba(20,36,72,0.055)] ' +
        'sm:p-6 ' +
        className
      }
    >
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-rose-50 text-rose-600">
            {icon}
          </span>

          <div className="min-w-0">
            {eyebrow && (
              <p className="mb-1 text-[10px] font-extrabold uppercase tracking-[0.14em] text-rose-500">
                {eyebrow}
              </p>
            )}

            <h2 className="text-base font-extrabold tracking-tight text-slate-900 sm:text-lg">
              {title}
            </h2>

            {description && (
              <p className="mt-1 text-xs leading-relaxed text-slate-500 sm:text-sm">
                {description}
              </p>
            )}
          </div>
        </div>

        {action}
      </div>

      {children}
    </section>
  )
}

interface Metric {
  label: string
  value: string
  detail: string
  icon: ReactNode
  color: string
  surface: string
  section: string
  delta?: number | null
}

function PremiumMetric({
  item,
  onClick,
}: {
  item: Metric
  onClick: () => void
}) {
  const reduced = useReducedMotion()

  return (
    <motion.button
      type="button"
      onClick={onClick}
      initial={reduced ? false : { opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={reduced ? undefined : { y: -4 }}
      transition={{ duration: reduced ? 0 : 0.23 }}
      className="group relative flex min-h-[153px] min-w-0 flex-col justify-between overflow-hidden rounded-[23px] border border-slate-200/70 bg-white p-4 text-left shadow-[0_8px_30px_rgba(23,38,73,0.055)] transition-shadow hover:shadow-[0_18px_44px_rgba(23,38,73,0.12)]"
      style={{
        '--card-accent': item.color,
        '--card-surface': item.surface,
        background: `linear-gradient(145deg, ${item.surface} 0%, #ffffff 72%)`,
      } as CSSProperties}
    >
      <div
        className="pointer-events-none absolute -right-7 -top-8 h-28 w-28 rounded-full opacity-40 blur-2xl"
        style={{ backgroundColor: item.surface }}
      />

      <div className="relative flex items-start justify-between gap-2">
        <span className="text-[10px] font-extrabold uppercase tracking-[0.1em] text-slate-500">
          {item.label}
        </span>

        <span
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[15px] bg-white shadow-sm"
          style={{ color: item.color }}
        >
          {item.icon}
        </span>
      </div>

      <strong className="relative my-2 text-[clamp(1.5rem,2vw,2rem)] font-black tracking-tight text-slate-900">
        {item.value}
      </strong>

      <div className="relative flex items-end justify-between gap-2">
        <span className="min-w-0 text-[11px] leading-snug text-slate-500">
          {item.detail}
        </span>

        {item.delta != null ? (
          <span
            className={
              'inline-flex shrink-0 items-center gap-0.5 rounded-full px-2 py-1 text-[10px] font-bold ' +
              (item.delta >= 0
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-rose-50 text-rose-700')
            }
          >
            {item.delta >= 0
              ? <ArrowUpRight size={12} />
              : <ArrowDownRight size={12} />}
            {Math.abs(item.delta)}%
          </span>
        ) : (
          <ArrowUpRight
            size={17}
            className="shrink-0 opacity-40 transition-opacity group-hover:opacity-100"
            style={{ color: item.color }}
          />
        )}
      </div>
    </motion.button>
  )
}

function EmptyChart({
  message,
}: {
  message: string
}) {
  return (
    <div className="flex min-h-[270px] flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 px-5 text-center">
      <Activity size={30} className="text-slate-300" />
      <strong className="mt-3 text-sm text-slate-700">
        Sin datos disponibles
      </strong>
      <p className="mt-1 max-w-sm text-xs text-slate-500">
        {message}
      </p>
    </div>
  )
}

function ServiceRow({
  name,
  detail,
  online,
  icon,
}: {
  name: string
  detail: string
  online: boolean
  icon: ReactNode
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl border border-slate-100 bg-slate-50/75 p-3.5">
      <div className="flex min-w-0 items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white text-slate-600 shadow-sm">
          {icon}
        </span>

        <div className="min-w-0">
          <strong className="block truncate text-xs font-bold text-slate-800">
            {name}
          </strong>
          <span className="text-[11px] text-slate-500">
            {detail}
          </span>
        </div>
      </div>

      <span
        className={
          'shrink-0 rounded-full px-2.5 py-1 text-[10px] font-bold ' +
          (online
            ? 'bg-emerald-100 text-emerald-700'
            : 'bg-rose-100 text-rose-700')
        }
      >
        {online ? 'Operativo' : 'Sin conexión'}
      </span>
    </div>
  )
}

function buildMetrics(snapshot: DashboardSnapshot): Metric[] {
  return [
    {
      label: 'Ponches hoy',
      value: fmt(snapshot.punchesToday),
      detail: 'Registros biométricos',
      icon: <Fingerprint size={20} />,
      color: PALETTE.red,
      surface: '#fff1f3',
      section: 'records',
      delta: percentageChange(
        snapshot.punchesToday,
        snapshot.punchesYesterday,
      ),
    },
    {
      label: 'Colaboradores',
      value: fmt(snapshot.employeesToday),
      detail: 'Con actividad hoy',
      icon: <Users size={20} />,
      color: PALETTE.indigo,
      surface: '#f0efff',
      section: 'collaborators',
      delta: percentageChange(
        snapshot.employeesToday,
        snapshot.employeesYesterday,
      ),
    },
    {
      label: 'Entradas',
      value: fmt(snapshot.entriesToday),
      detail: 'Entradas registradas',
      icon: <LogIn size={20} />,
      color: PALETTE.teal,
      surface: '#e9faf4',
      section: 'records',
    },
    {
      label: 'Salidas',
      value: fmt(snapshot.exitsToday),
      detail: 'Salidas registradas',
      icon: <LogOut size={20} />,
      color: PALETTE.blue,
      surface: '#eff6ff',
      section: 'records',
    },
    {
      label: 'Turnos abiertos',
      value: fmt(snapshot.openShifts),
      detail: 'Entradas sin salida',
      icon: <Clock3 size={20} />,
      color: '#d97706',
      surface: '#fffbeb',
      section: 'records',
    },
    {
      label: 'Relojes online',
      value: `${snapshot.devicesOnline}/${snapshot.devicesTotal}`,
      detail: 'Dispositivos disponibles',
      icon: <Wifi size={20} />,
      color: '#059669',
      surface: '#ecfdf5',
      section: 'devices',
    },
    {
      label: 'Relojes offline',
      value: fmt(snapshot.devicesOffline),
      detail: 'Requieren atención',
      icon: <WifiOff size={20} />,
      color: '#e11d48',
      surface: '#fff1f2',
      section: 'devices',
    },
    {
      label: 'Alertas',
      value: fmt(snapshot.warnings.length),
      detail: 'Avisos operativos',
      icon: <BellRing size={20} />,
      color: '#b45309',
      surface: '#fef9e7',
      section: 'devices',
    },
  ]
}

export default function PonchesPremiumDashboard() {
  const [, setSearchParams] = useSearchParams()
  const reducedMotion = useReducedMotion()

  const [autoRefresh, setAutoRefresh] = useState(true)
  const [activityMode, setActivityMode] =
    useState<'area' | 'bar'>('area')
  const [search, setSearch] = useState('')

  const {
    data: snapshot,
    error,
    isLoading,
    isFetching,
    refetch,
  } = useQuery({
    ...ponchesDashboardQueryOptions(),
    refetchInterval: autoRefresh ? 30_000 : false,
  })

  function navigate(section: string) {
    setSearchParams({ section })
  }

  const metrics = useMemo(
    () => snapshot ? buildMetrics(snapshot) : [],
    [snapshot],
  )

  const hourly = useMemo(() => {
    const values = new Map(
      (snapshot?.byHour ?? []).map(item => [
        item.hour,
        item.total,
      ]),
    )

    return Array.from({ length: 24 }, (_, hour) => ({
      hour: `${String(hour).padStart(2, '0')}:00`,
      total: values.get(hour) ?? 0,
    }))
  }, [snapshot?.byHour])

  const byDepartment = useMemo(
    () => [...(snapshot?.byDepartment ?? [])]
      .sort((a, b) => b.total - a.total)
      .slice(0, 7),
    [snapshot?.byDepartment],
  )

  const dailyTrend = useMemo(
    () => [...(snapshot?.byDay ?? [])].slice(-14),
    [snapshot?.byDay],
  )

  const recent = useMemo(() => {
    const term = search.trim().toLowerCase()

    return (snapshot?.recentPunches ?? [])
      .filter(row => {
        if (!term) return true

        return [
          row.codigo,
          row.nombre,
          row.departamento,
          row.dispositivo_origen,
        ].some(value =>
          String(value ?? '').toLowerCase().includes(term),
        )
      })
      .slice(0, 8)
  }, [snapshot?.recentPunches, search])

  const peakHour = hourly.reduce(
    (best, item) => item.total > best.total ? item : best,
    hourly[0],
  )

  const hasHourlyData = hourly.some(item => item.total > 0)

  const tooltipStyle: CSSProperties = {
    borderRadius: 14,
    border: '1px solid #e5eaf3',
    boxShadow: '0 14px 42px rgba(15,23,42,0.12)',
    fontSize: 12,
  }

  if (isLoading && !snapshot) {
    return (
      <div className="space-y-5 p-1" role="status">
        <div className="h-40 animate-pulse rounded-[28px] bg-slate-200/70" />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 8 }, (_, index) => (
            <div
              key={index}
              className="h-36 animate-pulse rounded-3xl bg-slate-200/70"
            />
          ))}
        </div>
        <span className="sr-only">Cargando dashboard de Ponches</span>
      </div>
    )
  }

  if (!snapshot) {
    return (
      <PremiumPanel
        title="No fue posible cargar Ponches"
        description={
          error instanceof Error
            ? error.message
            : 'Comprueba la conectividad con Python y BioTime.'
        }
        icon={<AlertTriangle size={21} />}
        action={
          <button
            type="button"
            onClick={() => void refetch()}
            className="rounded-xl bg-rose-600 px-4 py-2 text-sm font-bold text-white"
          >
            Reintentar
          </button>
        }
      >
        <EmptyChart message="No se muestran datos de ejemplo cuando falla el servicio." />
      </PremiumPanel>
    )
  }

  const availability = snapshot.devicesTotal > 0
    ? Math.round(
        (snapshot.devicesOnline / snapshot.devicesTotal) * 100,
      )
    : 0

  const pieData = [
    {
      name: 'Online',
      value: snapshot.devicesOnline,
      color: PALETTE.teal,
    },
    {
      name: 'Offline',
      value: snapshot.devicesOffline,
      color: '#f4c6d0',
    },
  ]

  return (
    <div className="min-w-0 space-y-5 pb-10">
      {/* PREMIUM HERO */}

      <header className="relative isolate overflow-hidden rounded-[30px] bg-[linear-gradient(120deg,#142449_0%,#26376a_52%,#593455_100%)] px-6 py-7 text-white shadow-[0_18px_50px_rgba(25,39,78,0.16)] sm:px-8 sm:py-9">
        <div className="pointer-events-none absolute -right-14 -top-24 h-72 w-72 rounded-full bg-rose-400/15 blur-3xl" />
        <div className="pointer-events-none absolute bottom-[-120px] left-[30%] h-64 w-64 rounded-full bg-indigo-400/15 blur-3xl" />

        <div className="relative z-10 flex flex-wrap items-start justify-between gap-6">
          <div className="max-w-2xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1.5 text-[10px] font-bold uppercase tracking-[0.15em] text-rose-100">
              <Sparkles size={14} />
              TitanMDM · Biometric Intelligence
            </div>

            <h1 className="mt-5 text-2xl font-black tracking-tight sm:text-3xl lg:text-4xl">
              Centro de inteligencia biométrica
            </h1>

            <p className="mt-3 max-w-xl text-sm leading-relaxed text-slate-200">
              Monitoreo ejecutivo de asistencia, colaboradores
              y conectividad de la infraestructura biométrica.
            </p>

            <p className="mt-5 inline-flex items-center gap-2 text-xs text-slate-200">
              <Clock3 size={15} />
              Actualizado: {formatDate(snapshot.generatedAt)}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              aria-pressed={autoRefresh}
              onClick={() => setAutoRefresh(value => !value)}
              className={
                'inline-flex items-center gap-2 rounded-xl border px-3.5 py-2.5 text-xs font-bold ' +
                (autoRefresh
                  ? 'border-emerald-300/40 bg-emerald-400/15 text-emerald-100'
                  : 'border-white/20 bg-white/10 text-white')
              }
            >
              <Radio size={16} />
              {autoRefresh ? 'En vivo' : 'Pausado'}
            </button>

            <button
              type="button"
              disabled={isFetching}
              onClick={() => void refetch()}
              className="inline-flex items-center gap-2 rounded-xl bg-rose-500 px-4 py-2.5 text-xs font-bold text-white shadow-lg shadow-rose-950/20 transition hover:bg-rose-400 disabled:opacity-60"
            >
              <RefreshCw
                size={16}
                className={isFetching ? 'animate-spin' : ''}
              />
              Actualizar
            </button>
          </div>
        </div>

        <Fingerprint
          size={155}
          strokeWidth={0.65}
          className="pointer-events-none absolute -bottom-9 right-12 hidden rotate-[-12deg] text-white/10 lg:block"
        />
      </header>

      {/* OPERATIONAL WARNINGS */}

      {snapshot.warnings.length > 0 && (
        <div className="flex flex-wrap gap-2" role="status">
          {snapshot.warnings.map((warning, index) => (
            <button
              key={`${warning}-${index}`}
              type="button"
              onClick={() => navigate('devices')}
              className="inline-flex items-center gap-2 rounded-full border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-semibold text-amber-800 hover:bg-amber-100"
            >
              <AlertTriangle size={14} />
              {warning}
              <ArrowRight size={13} />
            </button>
          ))}
        </div>
      )}

      {/* KPI CARDS */}

      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 2xl:grid-cols-8">
        {metrics.map(item => (
          <PremiumMetric
            key={item.label}
            item={item}
            onClick={() => navigate(item.section)}
          />
        ))}
      </section>

      {/* ACTIVITY + INFRASTRUCTURE */}

      <section className="grid items-start gap-4 xl:grid-cols-[1.6fr_1fr]">
        <PremiumPanel
          eyebrow="ANALÍTICA EN TIEMPO REAL"
          title="Pulso de asistencia"
          description="Actividad biométrica durante las 24 horas del día."
          icon={<Activity size={21} />}
          action={
            <div className="inline-flex rounded-xl bg-slate-100 p-1">
              {(['area', 'bar'] as const).map(mode => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => setActivityMode(mode)}
                  aria-pressed={activityMode === mode}
                  className={
                    'rounded-lg px-3 py-1.5 text-[11px] font-bold transition ' +
                    (activityMode === mode
                      ? 'bg-white text-rose-600 shadow-sm'
                      : 'text-slate-500 hover:text-slate-800')
                  }
                >
                  {mode === 'area' ? 'Tendencia' : 'Columnas'}
                </button>
              ))}
            </div>
          }
        >
          <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-slate-50 px-4 py-3">
            <div>
              <span className="block text-[11px] text-slate-500">
                Hora pico
              </span>
              <strong className="text-lg text-slate-900">
                {peakHour.hour}
              </strong>
            </div>

            <div>
              <span className="block text-[11px] text-slate-500">
                Registros en hora pico
              </span>
              <strong className="text-lg text-slate-900">
                {fmt(peakHour.total)}
              </strong>
            </div>

            <button
              type="button"
              onClick={() => navigate('records')}
              className="inline-flex items-center gap-1 text-xs font-bold text-rose-600"
            >
              Ver registros <ArrowRight size={15} />
            </button>
          </div>

          {hasHourlyData ? (
            <div className="h-[300px] w-full">
              <ResponsiveContainer width="100%" height="100%">
                {activityMode === 'area' ? (
                  <AreaChart
                    data={hourly}
                    margin={{ top: 12, right: 14, left: -22, bottom: 0 }}
                  >
                    <defs>
                      <linearGradient
                        id="ponchesPremiumGradient"
                        x1="0"
                        y1="0"
                        x2="0"
                        y2="1"
                      >
                        <stop
                          offset="0%"
                          stopColor={PALETTE.red}
                          stopOpacity={0.34}
                        />
                        <stop
                          offset="100%"
                          stopColor={PALETTE.red}
                          stopOpacity={0.015}
                        />
                      </linearGradient>
                    </defs>

                    <CartesianGrid
                      vertical={false}
                      stroke="#edf1f8"
                      strokeDasharray="4 6"
                    />
                    <XAxis
                      dataKey="hour"
                      interval={3}
                      axisLine={false}
                      tickLine={false}
                      tick={{ fontSize: 10, fill: '#94a3b8' }}
                    />
                    <YAxis
                      allowDecimals={false}
                      axisLine={false}
                      tickLine={false}
                      tick={{ fontSize: 11, fill: '#94a3b8' }}
                    />
                    <Tooltip
                      contentStyle={tooltipStyle}
                      formatter={value => [
                        fmt(Number(value)),
                        'Ponches',
                      ]}
                    />
                    <Area
                      dataKey="total"
                      type="monotone"
                      stroke={PALETTE.red}
                      strokeWidth={3.5}
                      fill="url(#ponchesPremiumGradient)"
                      dot={false}
                      activeDot={{ r: 5, strokeWidth: 0 }}
                      isAnimationActive={!reducedMotion}
                    />
                  </AreaChart>
                ) : (
                  <BarChart
                    data={hourly}
                    margin={{ top: 12, right: 14, left: -22, bottom: 0 }}
                  >
                    <CartesianGrid
                      vertical={false}
                      stroke="#edf1f8"
                      strokeDasharray="4 6"
                    />
                    <XAxis
                      dataKey="hour"
                      interval={3}
                      axisLine={false}
                      tickLine={false}
                      tick={{ fontSize: 10, fill: '#94a3b8' }}
                    />
                    <YAxis
                      allowDecimals={false}
                      axisLine={false}
                      tickLine={false}
                    />
                    <Tooltip
                      contentStyle={tooltipStyle}
                      formatter={value => [
                        fmt(Number(value)),
                        'Ponches',
                      ]}
                    />
                    <Bar
                      dataKey="total"
                      fill={PALETTE.red}
                      radius={[7, 7, 0, 0]}
                      maxBarSize={21}
                      isAnimationActive={!reducedMotion}
                    />
                  </BarChart>
                )}
              </ResponsiveContainer>
            </div>
          ) : (
            <EmptyChart message="No existen registros horarios en el período consultado." />
          )}
        </PremiumPanel>

        <PremiumPanel
          eyebrow="CENTRO DE OPERACIONES"
          title="Salud de infraestructura"
          description="Conectividad y disponibilidad biométrica."
          icon={<ShieldCheck size={21} />}
        >
          <div className="grid grid-cols-1 items-center gap-4 sm:grid-cols-2">
            <div className="relative mx-auto h-[205px] w-[205px]">
              {snapshot.devicesTotal > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={pieData}
                      dataKey="value"
                      nameKey="name"
                      innerRadius={67}
                      outerRadius={86}
                      strokeWidth={0}
                      paddingAngle={3}
                      isAnimationActive={!reducedMotion}
                    >
                      {pieData.map(item => (
                        <Cell
                          key={item.name}
                          fill={item.color}
                        />
                      ))}
                    </Pie>
                    <Tooltip contentStyle={tooltipStyle} />
                  </PieChart>
                </ResponsiveContainer>
              ) : (
                <div className="absolute inset-4 rounded-full border-[18px] border-slate-100" />
              )}

              <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                <strong className="text-3xl font-black tracking-tight text-slate-900">
                  {availability}%
                </strong>
                <span className="text-[11px] text-slate-500">
                  disponibilidad
                </span>
              </div>
            </div>

            <div className="space-y-3">
              {[
                {
                  label: 'Registrados',
                  value: snapshot.devicesTotal,
                  color: 'text-slate-900',
                },
                {
                  label: 'Conectados',
                  value: snapshot.devicesOnline,
                  color: 'text-emerald-600',
                },
                {
                  label: 'Desconectados',
                  value: snapshot.devicesOffline,
                  color: 'text-rose-600',
                },
              ].map(item => (
                <div
                  key={item.label}
                  className="flex items-center justify-between rounded-xl bg-slate-50 px-3 py-2.5"
                >
                  <span className="text-xs text-slate-500">
                    {item.label}
                  </span>
                  <strong className={`text-lg ${item.color}`}>
                    {fmt(item.value)}
                  </strong>
                </div>
              ))}
            </div>
          </div>

          <div className="mt-4 space-y-2.5">
            <ServiceRow
              name="Gateway Python"
              detail="Bridge interno"
              online={snapshot.serviceOnline}
              icon={<Server size={17} />}
            />
            <ServiceRow
              name="BioTime SQL"
              detail="Datos biométricos"
              online={snapshot.databaseOnline}
              icon={<Database size={17} />}
            />
          </div>

          <button
            type="button"
            onClick={() => navigate('sync-history')}
            className="mt-4 inline-flex items-center gap-2 text-xs font-bold text-rose-600 hover:text-rose-800"
          >
            Historial de sincronización
            <ArrowRight size={15} />
          </button>
        </PremiumPanel>
      </section>

      {/* DEPARTMENT DISTRIBUTION */}

      <PremiumPanel
        eyebrow="INTELIGENCIA ORGANIZACIONAL"
        title="Distribución por departamento"
        description="Departamentos con mayor volumen de registros biométricos."
        icon={<Building2 size={21} />}
        action={
          <button
            type="button"
            onClick={() => navigate('records')}
            className="inline-flex items-center gap-2 text-xs font-bold text-rose-600"
          >
            Ver registros <ArrowRight size={15} />
          </button>
        }
      >
        {byDepartment.length > 0 ? (
          <div
            className="w-full"
            style={{
              height: Math.max(275, byDepartment.length * 49),
            }}
          >
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={byDepartment}
                layout="vertical"
                margin={{ top: 5, right: 25, left: 8, bottom: 5 }}
              >
                <CartesianGrid
                  horizontal={false}
                  stroke="#edf1f8"
                  strokeDasharray="4 6"
                />
                <XAxis
                  type="number"
                  allowDecimals={false}
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: '#94a3b8' }}
                />
                <YAxis
                  type="category"
                  dataKey="name"
                  width={120}
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: '#64748b' }}
                />
                <Tooltip
                  contentStyle={tooltipStyle}
                  formatter={value => [
                    fmt(Number(value)),
                    'Ponches',
                  ]}
                />
                <Bar
                  dataKey="total"
                  radius={[0, 9, 9, 0]}
                  maxBarSize={24}
                  isAnimationActive={!reducedMotion}
                >
                  {byDepartment.map((item, index) => (
                    <Cell
                      key={item.name}
                      fill={
                        DEPARTMENT_COLORS[
                          index % DEPARTMENT_COLORS.length
                        ]
                      }
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : (
          <EmptyChart message="No hay estadísticas departamentales disponibles." />
        )}
      </PremiumPanel>

      {/* CLOCK ATTENTION CENTER: FULL WIDTH */}

      <PunchClockAttentionCenter
        devices={snapshot.healthItems}
        onViewAll={() => navigate('devices')}
      />

      {/* HISTORICAL TREND */}

      {dailyTrend.length > 1 && (
        <PremiumPanel
          eyebrow="BUSINESS INTELLIGENCE"
          title="Tendencia histórica de asistencia"
          description="Evolución del volumen de marcaciones durante los últimos días disponibles."
          icon={<Activity size={21} />}
        >
          <div className="h-[270px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={dailyTrend}
                margin={{ top: 12, right: 12, left: -15, bottom: 0 }}
              >
                <CartesianGrid
                  vertical={false}
                  stroke="#edf1f8"
                  strokeDasharray="4 6"
                />
                <XAxis
                  dataKey="day"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 10, fill: '#94a3b8' }}
                />
                <YAxis
                  allowDecimals={false}
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 11, fill: '#94a3b8' }}
                />
                <Tooltip
                  contentStyle={tooltipStyle}
                  formatter={value => [
                    fmt(Number(value)),
                    'Ponches',
                  ]}
                />
                <Bar
                  dataKey="total"
                  fill={PALETTE.red}
                  radius={[8, 8, 0, 0]}
                  maxBarSize={37}
                  isAnimationActive={!reducedMotion}
                />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </PremiumPanel>
      )}

      {/* RECENT PUNCHES */}

      <PremiumPanel
        eyebrow="ACTIVIDAD RECIENTE"
        title="Últimas marcaciones"
        description="Registros recientes enviados por la infraestructura biométrica."
        icon={<Fingerprint size={21} />}
        action={
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <Search
                size={14}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                value={search}
                onChange={event => setSearch(event.target.value)}
                placeholder="Buscar..."
                aria-label="Buscar marcaciones recientes"
                className="w-40 rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-3 text-xs text-slate-700 outline-none focus:border-rose-400 sm:w-56"
              />
            </div>

            <button
              type="button"
              onClick={() => navigate('records')}
              className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50"
            >
              Historial completo
            </button>
          </div>
        }
      >
        {recent.length === 0 ? (
          <EmptyChart message="No hay registros que coincidan con la búsqueda." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[750px] text-left text-sm">
              <thead>
                <tr className="bg-slate-50 text-[10px] font-bold uppercase tracking-[0.08em] text-slate-500">
                  <th className="rounded-l-xl px-4 py-3">Código</th>
                  <th className="px-4 py-3">Colaborador</th>
                  <th className="px-4 py-3">Departamento</th>
                  <th className="px-4 py-3">Entrada</th>
                  <th className="px-4 py-3">Salida</th>
                  <th className="rounded-r-xl px-4 py-3">Reloj</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {recent.map((row, index) => (
                  <tr
                    key={`${row.id ?? row.codigo ?? 'row'}-${index}`}
                    className="transition-colors hover:bg-rose-50/40"
                  >
                    <td className="px-4 py-3 font-mono text-xs text-slate-500">
                      {row.codigo ?? '—'}
                    </td>

                    <td className="px-4 py-3 font-semibold text-slate-800">
                      {usableEmployeeName(row.nombre, row.codigo)}
                    </td>

                    <td className="px-4 py-3 text-slate-600">
                      {row.departamento || '—'}
                    </td>

                    <td className="px-4 py-3 text-slate-600">
                      {formatPunchTime(row.entrada)}
                    </td>

                    <td className="px-4 py-3 text-slate-600">
                      {formatPunchTime(row.salida)}
                    </td>

                    <td className="px-4 py-3 text-slate-500">
                      {row.dispositivo_origen || '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </PremiumPanel>

      {/* QUICK ACTIONS */}

      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {[
          {
            label: 'Consultar ponches',
            section: 'records',
            icon: <Activity size={20} />,
          },
          {
            label: 'Administrar relojes',
            section: 'devices',
            icon: <Wifi size={20} />,
          },
          {
            label: 'Colaboradores',
            section: 'collaborators',
            icon: <Users size={20} />,
          },
          {
            label: 'Reportes avanzados',
            section: 'advanced-reports',
            icon: <Building2 size={20} />,
          },
        ].map(item => (
          <motion.button
            key={item.section}
            type="button"
            whileHover={
              reducedMotion ? undefined : { y: -3 }
            }
            onClick={() => navigate(item.section)}
            className="flex min-h-16 items-center justify-between gap-3 rounded-2xl border border-slate-200/80 bg-white px-5 py-4 text-left shadow-sm transition-colors hover:border-rose-200 hover:bg-rose-50/40"
          >
            <span className="inline-flex items-center gap-3 text-sm font-bold text-slate-700">
              <span className="text-rose-600">
                {item.icon}
              </span>
              {item.label}
            </span>
            <ArrowRight size={17} className="text-slate-400" />
          </motion.button>
        ))}
      </section>

      <footer className="flex flex-wrap items-center justify-between gap-2 px-1 text-[11px] text-slate-400">
        <span className="inline-flex items-center gap-2">
          <CheckCircle2 size={14} />
          Datos obtenidos mediante TitanMDM → Python → BioTime
        </span>
        <span>
          Última actualización: {formatDate(snapshot.generatedAt)}
        </span>
      </footer>
    </div>
  )
}
