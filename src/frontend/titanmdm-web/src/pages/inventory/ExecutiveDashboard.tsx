import { useMemo } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Boxes, Inbox, RotateCcw, Truck } from 'lucide-react'
import type { Asset, CatalogItem, Dashboard } from './inventoryApi'
import { monthKeyLabel } from './inventoryOperational'

const statusColors = ['#0ea5e9', '#10b981', '#8b5cf6', '#f59e0b', '#ef4444', '#6366f1']

const currency = (value: number) =>
  `RD$ ${Number(value ?? 0).toLocaleString('es-DO', { maximumFractionDigits: 0 })}`

const chartTooltip = {
  backgroundColor: '#ffffff',
  border: '1px solid #e2e7ef',
  borderRadius: 12,
  color: '#172033',
  fontSize: 12,
  boxShadow: '0 18px 40px -22px rgba(23, 32, 51, 0.35)',
}

const axisTick = { fill: '#7b8799', fontSize: 11 }
const gridStroke = '#e2e7ef'

function isHandHeld(asset: Asset, categoryName: string) {
  return /hand[\s_-]?held|ck65|handheld/i.test(categoryName) ||
    /hand[\s_-]?held|ck65|handheld/i.test(asset.name ?? '')
}

export function ExecutiveDashboard({
  dashboard,
  assets,
  categories,
}: {
  dashboard: Dashboard
  assets: Asset[]
  categories: CatalogItem[]
}) {
  const categoryData = useMemo(() => {
    const categoryNameById = new Map(categories.map(category => [String(category.id), category.name]))
    const handHeldByCategory = new Map<string, number>()

    assets.forEach(asset => {
      const categoryName = asset.categoryId
        ? categoryNameById.get(asset.categoryId) ?? 'Sin categoría'
        : 'Sin categoría'

      if (isHandHeld(asset, categoryName)) {
        handHeldByCategory.set(categoryName, (handHeldByCategory.get(categoryName) ?? 0) + 1)
      }
    })

    return dashboard.byCategory.map(item => ({
      name: item.name,
      uv: item.count,
      handHeld: handHeldByCategory.get(item.name) ?? 0,
    }))
  }, [assets, categories, dashboard.byCategory])

  const pieData = useMemo(
    () => dashboard.byStatus.map(item => ({ name: item.name, value: item.count })),
    [dashboard.byStatus],
  )

  const manufacturerData = useMemo(
    () => (dashboard.byManufacturer ?? []).slice(0, 7).map(item => ({ name: item.name, activos: item.count })),
    [dashboard.byManufacturer],
  )

  const departmentData = useMemo(
    () => (dashboard.byDepartment ?? []).slice(0, 8).map(item => ({ name: item.name, activos: item.count })),
    [dashboard.byDepartment],
  )

  const monthlyData = useMemo(
    () => (dashboard.monthlyAssignments ?? []).map(item => ({
      name: monthKeyLabel(item.name),
      asignaciones: item.count,
    })),
    [dashboard.monthlyAssignments],
  )

  const branchData = useMemo(
    () => (dashboard.byBranch ?? []).map(item => ({ name: item.name, activos: item.count })),
    [dashboard.byBranch],
  )

  const total = Math.max(dashboard.totalAssets, 1)
  const share = (value: number) => Math.round((value / total) * 100)
  const returnedTotal = Math.max(dashboard.activeDeliveries + dashboard.returnedDeliveries, 1)
  const returnRate = Math.round((dashboard.returnedDeliveries / returnedTotal) * 100)

  const primaryKpis = [
    {
      label: 'Total de Activos',
      value: dashboard.totalAssets,
      hint: 'Activos registrados',
      delta: share(dashboard.availableAssets),
      deltaLabel: 'disponibles',
      icon: Boxes,
      tone: 'blue',
    },
    {
      label: 'Activos Entregados',
      value: dashboard.assignedAssets,
      hint: 'En manos de colaboradores',
      delta: share(dashboard.assignedAssets),
      deltaLabel: 'asignación',
      icon: Truck,
      tone: 'indigo',
    },
    {
      label: 'Devoluciones',
      value: dashboard.returnedDeliveries,
      hint: 'Equipos devueltos',
      delta: returnRate,
      deltaLabel: 'tasa de retorno',
      icon: RotateCcw,
      tone: 'emerald',
    },
    {
      label: 'Recepciones',
      value: dashboard.pendingReceptions,
      hint: 'Pendientes de procesar',
      delta: share(dashboard.pendingReceptions),
      deltaLabel: 'por procesar',
      icon: Inbox,
      tone: 'violet',
    },
  ]

  const secondaryKpis = [
    ['Valor inventario', currency(dashboard.totalPurchaseCost), 'Costo de adquisición', 'tone-cyan'],
    ['En riesgo', dashboard.atRiskAssets, 'Garantía vencida o daño', 'tone-rose'],
    ['Rotación', `${dashboard.rotationRate.toFixed(2)}x`, 'Entregas últimos 12 meses', 'tone-violet'],
    ['Sucursales', dashboard.totalBranches, 'Cobertura física', 'tone-slate'],
    ['Empleados', dashboard.totalEmployees, 'Colaboradores activos', 'tone-orange'],
    ['Garantías por vencer', dashboard.warrantiesExpiringSoon, 'Próximos 90 días', 'tone-amber'],
  ] as const

  return (
    <div className="executive-dashboard">
      <div className="inventory-kpi-grid">
        {primaryKpis.map(kpi => {
          const Icon = kpi.icon
          return (
            <article key={kpi.label} className={`inventory-kpi-card inventory-kpi-card--${kpi.tone}`}>
              <div className="inventory-kpi-card__top">
                <div className="inventory-kpi-card__icon">
                  <Icon size={22} aria-hidden="true" />
                </div>
                <span className="inventory-kpi-delta">+{kpi.delta}%</span>
              </div>
              <strong>{kpi.value}</strong>
              <span className="inventory-kpi-card__label">{kpi.label}</span>
              <small>{kpi.hint} · {kpi.deltaLabel}</small>
            </article>
          )
        })}
      </div>

      <div className="executive-kpis">
        {secondaryKpis.map(([label, value, hint, tone]) => (
          <div key={label} className={`executive-kpi ${tone}`}>
            <span>{label}</span>
            <strong>{value}</strong>
            <small>{hint}</small>
          </div>
        ))}
      </div>

      <div className="executive-values">
        <div className="executive-value">
          <span>Costo total</span>
          <strong>{currency(dashboard.totalPurchaseCost)}</strong>
        </div>
        <div className="executive-value">
          <span>Depreciación acumulada</span>
          <strong>{currency(dashboard.accumulatedDepreciation)}</strong>
        </div>
        <div className="executive-value">
          <span>Valor en libros</span>
          <strong>{currency(dashboard.bookValue)}</strong>
        </div>
        <div className="executive-value">
          <span>Activos en riesgo</span>
          <strong>{dashboard.atRiskAssets}</strong>
        </div>
      </div>

      <div className="executive-chart-grid">
        <article className="inventory-card">
          <h3>Activos por categoría</h3>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={categoryData} margin={{ top: 20, right: 20, left: 0, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke={gridStroke} />
              <XAxis dataKey="name" tick={axisTick} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={axisTick} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={chartTooltip} />
              <Legend />
              <Bar dataKey="uv" fill="#4169e1" name="Activos" radius={[6, 6, 0, 0]} />
              <Bar dataKey="handHeld" fill="#8b5cf6" name="Hand Held" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card">
          <h3>Estado del inventario</h3>
          <ResponsiveContainer width="100%" height={260}>
            <PieChart>
              <Pie data={pieData} dataKey="value" nameKey="name" innerRadius={54} outerRadius={88}>
                {pieData.map((entry, index) => (
                  <Cell key={entry.name} fill={statusColors[index % statusColors.length]} />
                ))}
              </Pie>
              <Tooltip contentStyle={chartTooltip} />
            </PieChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card">
          <h3>Comparativa de marcas</h3>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={manufacturerData} layout="vertical" margin={{ top: 8, right: 20, left: 10, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke={gridStroke} />
              <XAxis type="number" allowDecimals={false} tick={axisTick} axisLine={false} tickLine={false} />
              <YAxis type="category" dataKey="name" width={110} tick={axisTick} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={chartTooltip} />
              <Bar dataKey="activos" fill="#0ea5e9" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card">
          <h3>Activos por departamento</h3>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={departmentData} margin={{ top: 16, right: 16, left: 0, bottom: 32 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke={gridStroke} />
              <XAxis dataKey="name" tick={axisTick} angle={-18} textAnchor="end" height={54} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={axisTick} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={chartTooltip} />
              <Bar dataKey="activos" fill="#10b981" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card executive-chart-wide">
          <h3>Tendencia mensual de asignaciones</h3>
          <ResponsiveContainer width="100%" height={280}>
            <AreaChart data={monthlyData} margin={{ top: 16, right: 24, left: 0, bottom: 8 }}>
              <defs>
                <linearGradient id="itamAssignments" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#4169e1" stopOpacity={0.35} />
                  <stop offset="95%" stopColor="#4169e1" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke={gridStroke} />
              <XAxis dataKey="name" tick={axisTick} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={axisTick} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={chartTooltip} />
              <Area
                type="monotone"
                dataKey="asignaciones"
                stroke="#4169e1"
                strokeWidth={2.5}
                fill="url(#itamAssignments)"
                activeDot={{ r: 5, fill: '#0ea5e9', stroke: '#ffffff' }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card executive-chart-wide">
          <h3>Activos por sucursal</h3>
          <ResponsiveContainer width="100%" height={240}>
            <BarChart data={branchData} margin={{ top: 12, right: 20, left: 0, bottom: 40 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke={gridStroke} />
              <XAxis dataKey="name" tick={axisTick} angle={-16} textAnchor="end" height={60} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={axisTick} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={chartTooltip} />
              <Bar dataKey="activos" fill="#8b5cf6" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </article>
      </div>
    </div>
  )
}
