
import { useMemo, useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'

import {
  Activity,
  AlertCircle,
  ArrowRight,
  ArrowUpRight,
  CalendarClock,
  Fingerprint,
  RefreshCw,
  Search,
  UserRoundPen,
  Users,
  Building2,
  CheckCircle2,
  X,
} from 'lucide-react'

import { titanFetch, readJson } from '../lib/api'

type Employee = {
  codigo: string
  nombre: string | null
  departamento: string | null
  total_registros: number
  ultima_fecha: string | null
  ultimo_dispositivo: string | null
}

type EmployeeResponse = {
  count: number
  items: Employee[]
}

type Accent = 'indigo' | 'rose' | 'emerald'

const accents: Record<
  Accent,
  {
    color: string
    background: string
  }
> = {
  indigo: {
    color: '#6366f1',
    background: '#eef2ff',
  },
  rose: {
    color: '#e11d48',
    background: '#fff1f2',
  },
  emerald: {
    color: '#059669',
    background: '#ecfdf5',
  },
}

const numberFormatter = new Intl.NumberFormat('es-DO')

function formatNumber(value: number): string {
  return numberFormatter.format(value)
}

function displayName(employee: Employee): string {
  const value = (employee.nombre ?? '').trim()

  if (
    !value ||
    value === employee.codigo ||
    /^NN[-_ ]?\d+$/i.test(value) ||
    /^\d+$/.test(value)
  ) {
    return 'Identidad pendiente de verificar'
  }

  return value
}

function formatDate(value: string | null): string {
  if (!value) return 'Sin registros'

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) return value

  return date.toLocaleString('es-DO', {
    dateStyle: 'short',
    timeStyle: 'short',
  })
}

async function getEmployees(
  search: string,
  signal?: AbortSignal,
): Promise<EmployeeResponse> {
  const params = new URLSearchParams({
    search,
    limit: '200',
  })

  const response = await titanFetch(
    `/api/ponches/employees?${params.toString()}`,
    { signal },
  )

  const result = await readJson<EmployeeResponse>(response)

  if (!result || !Array.isArray(result.items)) {
    throw new Error(
      'El servidor no devolvió una lista válida de empleados.',
    )
  }

  return result
}

/**
 * Botón corporativo reutilizable dentro de Empleados.
 *
 * La apariencia se controla también por estilos explícitos
 * para evitar que reglas CSS heredadas reintroduzcan
 * bordes y controles con apariencia antigua.
 */
function PremiumButton({
  children,
  onClick,
  disabled = false,
  variant = 'primary',
  type = 'button',
  title,
}: {
  children: React.ReactNode
  onClick?: () => void
  disabled?: boolean
  variant?: 'primary' | 'secondary' | 'ghost'
  type?: 'button' | 'submit'
  title?: string
}) {
  const reducedMotion = useReducedMotion()

  const background =
    variant === 'primary'
      ? 'linear-gradient(135deg,#273c69 0%,#192a4b 100%)'
      : variant === 'secondary'
        ? '#fff'
        : 'transparent'

  return (
    <motion.button
      type={type}
      title={title}
      onClick={onClick}
      disabled={disabled}
      whileHover={
        reducedMotion || disabled
          ? undefined
          : { y: -2 }
      }
      whileTap={
        reducedMotion || disabled
          ? undefined
          : { scale: 0.97 }
      }
      transition={{ duration: 0.15 }}
      className={
        'inline-flex min-h-[36px] shrink-0 items-center justify-center ' +
        'gap-2 whitespace-nowrap rounded-xl px-3.5 py-2 text-xs ' +
        'font-bold shadow-sm transition-[box-shadow,opacity] ' +
        'focus-visible:outline focus-visible:outline-2 ' +
        'focus-visible:outline-offset-2 focus-visible:outline-indigo-500 ' +
        'disabled:cursor-not-allowed disabled:opacity-50 ' +
        (variant === 'primary'
          ? 'shadow-[0_5px_14px_rgba(28,45,83,0.18)] hover:shadow-[0_9px_20px_rgba(28,45,83,0.26)]'
          : 'hover:shadow-md')
      }
      style={{
        appearance: 'none',
        WebkitAppearance: 'none',
        border:
          variant === 'primary'
            ? '1px solid #273c69'
            : variant === 'secondary'
              ? '1px solid #e2e8f0'
              : '1px solid transparent',
        borderRadius: 12,
        background,
        color:
          variant === 'primary'
            ? '#fff'
            : variant === 'secondary'
              ? '#334155'
              : '#be123c',
        cursor: disabled ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit',
      }}
    >
      {children}
    </motion.button>
  )
}

function MetricCard({
  label,
  value,
  hint,
  icon,
  accent,
}: {
  label: string
  value: number
  hint: string
  icon: React.ReactNode
  accent: Accent
}) {
  const reducedMotion = useReducedMotion()
  const palette = accents[accent]

  return (
    <motion.article
      initial={reducedMotion ? false : { opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={reducedMotion ? undefined : { y: -3 }}
      transition={{ duration: 0.22 }}
      className="relative overflow-hidden rounded-[23px] border border-slate-200/80 bg-white p-5 shadow-[0_8px_28px_rgba(20,40,74,0.055)]"
    >
      <div
        className="pointer-events-none absolute -right-12 -top-12 h-32 w-32 rounded-full blur-3xl"
        style={{ background: palette.background }}
      />

      <div className="relative flex items-start justify-between gap-3">
        <span className="text-[11px] font-extrabold uppercase tracking-[0.09em] text-slate-500">
          {label}
        </span>

        <span
          className="flex h-11 w-11 items-center justify-center rounded-2xl"
          style={{
            background: palette.background,
            color: palette.color,
          }}
        >
          {icon}
        </span>
      </div>

      <strong className="relative mt-3 block text-3xl font-black tracking-tight text-slate-900">
        {formatNumber(value)}
      </strong>

      <p className="relative mt-2 text-xs text-slate-500">
        {hint}
      </p>
    </motion.article>
  )
}

export default function Employees() {
  const [, setSearchParams] = useSearchParams()

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const query = useQuery({
    queryKey: ['ponches', 'employees', search],
    queryFn: ({ signal }) => getEmployees(search, signal),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  })

  const employees = query.data?.items ?? []

  const totalPunches = useMemo(
    () => employees.reduce(
      (total, employee) =>
        total + Number(employee.total_registros || 0),
      0,
    ),
    [employees],
  )

  const departments = useMemo(
    () => new Set(
      employees
        .map(employee => employee.departamento?.trim())
        .filter(Boolean),
    ).size,
    [employees],
  )

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchInput.trim())
  }

  function clearSearch() {
    setSearchInput('')
    setSearch('')
  }

  function goToCollaborators() {
    setSearchParams({ section: 'collaborators' })
  }

  return (
    <div className="min-w-0 space-y-5 pb-9">
      {/* HEADER */}

      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <span className="text-[10px] font-extrabold uppercase tracking-[0.16em] text-rose-600">
            Ponches / Gestión de personas
          </span>

          <h1 className="mt-2 text-2xl font-black tracking-tight text-slate-900 sm:text-3xl">
            Directorio de empleados
          </h1>

          <p className="mt-2 text-sm text-slate-500">
            Consulta de actividad biométrica y empleados registrados
            en BioTime.
          </p>
        </div>

        <PremiumButton
          onClick={() => void query.refetch()}
          disabled={query.isFetching}
        >
          <RefreshCw
            size={16}
            className={query.isFetching ? 'animate-spin' : ''}
          />
          Actualizar registros
        </PremiumButton>
      </header>

      {/* METRICS */}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <MetricCard
          label="Empleados encontrados"
          value={employees.length}
          hint="Resultados de la consulta actual"
          icon={<Users size={21} />}
          accent="indigo"
        />

        <MetricCard
          label="Registros acumulados"
          value={totalPunches}
          hint="Marcaciones de los empleados mostrados"
          icon={<Fingerprint size={21} />}
          accent="rose"
        />

        <MetricCard
          label="Departamentos"
          value={departments}
          hint="Representados en los resultados"
          icon={<Building2 size={21} />}
          accent="emerald"
        />
      </div>

      {/* DATA TABLE */}

      <section className="overflow-hidden rounded-[25px] border border-slate-200/80 bg-white shadow-[0_10px_35px_rgba(20,40,75,0.055)]">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 p-5 sm:p-6">
          <div>
            <div className="flex items-center gap-2">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-rose-50 text-rose-600">
                <Activity size={18} />
              </span>

              <h2 className="text-base font-extrabold text-slate-900">
                Empleados con actividad
              </h2>
            </div>

            <p className="mt-2 text-xs text-slate-500">
              Consulta y administración mediante el módulo
              Colaboradores.
            </p>
          </div>

          <form
            onSubmit={submitSearch}
            className="flex w-full max-w-lg flex-wrap items-center gap-2"
          >
            <div className="relative min-w-[170px] flex-1">
              <Search
                size={17}
                className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />

              <input
                type="search"
                value={searchInput}
                onChange={event => setSearchInput(event.target.value)}
                placeholder="Buscar código o nombre..."
                maxLength={80}
                aria-label="Buscar empleado"
                className="h-10 w-full rounded-xl border border-slate-200 bg-slate-50/70 pl-10 pr-10 text-xs text-slate-700 outline-none transition focus:border-rose-400 focus:bg-white focus:ring-2 focus:ring-rose-100"
              />

              {searchInput && (
                <button
                  type="button"
                  aria-label="Limpiar búsqueda"
                  onClick={clearSearch}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-rose-600"
                >
                  <X size={15} />
                </button>
              )}
            </div>

            <PremiumButton type="submit">
              <Search size={15} />
              Buscar
            </PremiumButton>
          </form>
        </div>

        {query.isError && (
          <div
            role="alert"
            className="m-5 flex items-start gap-3 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700"
          >
            <AlertCircle size={19} className="shrink-0" />
            <span>
              {query.error instanceof Error
                ? query.error.message
                : 'No fue posible cargar los empleados.'}
            </span>
          </div>
        )}

        {query.isLoading ? (
          <div
            className="flex min-h-[260px] flex-col items-center justify-center gap-3 p-10"
            role="status"
          >
            <RefreshCw
              size={25}
              className="animate-spin text-rose-500"
            />
            <p className="text-sm text-slate-500">
              Consultando empleados...
            </p>
          </div>
        ) : employees.length === 0 ? (
          <div className="flex min-h-[260px] flex-col items-center justify-center p-10 text-center">
            <Users size={36} className="text-slate-300" />
            <strong className="mt-3 text-sm text-slate-700">
              Sin resultados
            </strong>
            <p className="mt-1 text-xs text-slate-500">
              Modifica la búsqueda o comprueba la conexión con BioTime.
            </p>
          </div>
        ) : (
          <div className="max-h-[650px] overflow-auto">
            <table className="w-full min-w-[980px] border-separate border-spacing-0 text-left text-sm">
              <thead className="sticky top-0 z-10 bg-slate-50">
                <tr className="text-[10px] font-bold uppercase tracking-[0.08em] text-slate-500">
                  <th className="px-5 py-4">Código</th>
                  <th className="px-5 py-4">Empleado</th>
                  <th className="px-5 py-4">Departamento</th>
                  <th className="px-5 py-4">Registros</th>
                  <th className="px-5 py-4">Última actividad</th>
                  <th className="px-5 py-4">Dispositivo</th>
                  <th className="px-5 py-4 text-center">Gestión</th>
                </tr>
              </thead>

              <tbody>
                {employees.map(employee => (
                  <tr
                    key={employee.codigo}
                    className="group transition-colors hover:bg-rose-50/30"
                  >
                    <td className="border-b border-slate-100 px-5 py-4 font-mono text-xs text-slate-500">
                      {employee.codigo}
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4">
                      <div className="flex items-center gap-3">
                        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-indigo-50 text-indigo-600">
                          <Users size={16} />
                        </span>

                        <div className="min-w-0">
                          <strong className="block text-xs font-bold text-slate-800">
                            {displayName(employee)}
                          </strong>
                          <span className="mt-1 block text-[10px] text-slate-400">
                            Código: {employee.codigo}
                          </span>
                        </div>
                      </div>
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4">
                      <span className="inline-flex rounded-full bg-slate-100 px-3 py-1 text-[11px] font-semibold text-slate-600">
                        {employee.departamento || 'Sin asignar'}
                      </span>
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4">
                      <span className="inline-flex items-center gap-2 text-xs font-extrabold text-slate-800">
                        <CheckCircle2
                          size={15}
                          className="text-emerald-500"
                        />
                        {formatNumber(
                          Number(employee.total_registros || 0),
                        )}
                      </span>
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4 text-xs text-slate-500">
                      <span className="inline-flex items-center gap-2">
                        <CalendarClock size={15} />
                        {formatDate(employee.ultima_fecha)}
                      </span>
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4 text-xs text-slate-500">
                      {employee.ultimo_dispositivo || '—'}
                    </td>

                    <td className="border-b border-slate-100 px-5 py-4 text-center">
                      <PremiumButton
                        onClick={goToCollaborators}
                        title={`Abrir Colaboradores para consultar el código ${employee.codigo}`}
                      >
                        <UserRoundPen size={15} />
                        Ver ficha
                        <ArrowUpRight size={14} />
                      </PremiumButton>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 px-5 py-4 text-xs text-slate-500">
          <span>
            {employees.length} empleados mostrados
          </span>

          <span className="inline-flex items-center gap-2">
            <CheckCircle2 size={14} className="text-emerald-500" />
            Consulta de lectura
          </span>

          <span>
            Máximo 200 resultados por consulta
          </span>
        </footer>
      </section>

      {/* COLLABORATORS SHORTCUT */}

      <section className="flex flex-wrap items-center justify-between gap-4 rounded-[23px] border border-indigo-100 bg-gradient-to-r from-indigo-50 via-white to-rose-50 p-5">
        <div>
          <h3 className="text-sm font-extrabold text-slate-900">
            Gestión avanzada de colaboradores
          </h3>
          <p className="mt-1 text-xs text-slate-500">
            Perfiles biométricos, asignación a relojes,
            sincronización y validaciones.
          </p>
        </div>

        <PremiumButton onClick={goToCollaborators}>
          <UserRoundPen size={16} />
          Administrar colaboradores
          <ArrowRight size={15} />
        </PremiumButton>
      </section>
    </div>
  )
}
