import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
  type InputHTMLAttributes,
  type ReactNode,
} from 'react'
import {
  Area,
  AreaChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Boxes, UserCheck, Wrench } from 'lucide-react'
import { InventoryDataTable, type InventoryColumn, type InventoryRow } from './InventoryDataTable'
import { ExecutiveDashboard } from './ExecutiveDashboard'
import { IframeModal } from './IframeModal'
import { useItamModalClose } from './itamModalContext'
import { PhysicalReconciliation } from './PhysicalReconciliation'
import { QRGenerator } from './QRGenerator'
import { SignaturePad } from './SignaturePad'
import { TypeaheadSearch } from './TypeaheadSearch'
import type { InventorySectionId } from './inventoryNav'
import {
  deliveryFilterLabels,
  deliveryReport,
  filterDeliveries,
  filterReturns,
  monthKeyLabel,
  overdueDays,
  returnReport,
  type DeliveryFilter,
  type ReturnFilter,
} from './inventoryOperational'
import type { OperationalReport } from './inventoryReportExport'
import { useInventoryAccess } from './inventoryAccess'
import {
  itamApi,
  type Asset,
  type CatalogItemInput,
  type CatalogKind,
  type Catalogs,
  type CatalogItem,
  type Delivery,
  type Employee,
  type InventoryCount,
  type InventorySession,
  type ReconcileResult,
} from './inventoryApi'

const statusOptions = ['disponible', 'asignado', 'mantenimiento', 'danado', 'baja', 'reservado', 'transito']
const conditionOptions = ['nuevo', 'bueno', 'regular', 'malo']

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'No se pudo completar la operación.'

function useAsync<T>(loader: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [tick, setTick] = useState(0)

  useEffect(() => {
    let active = true

    const run = async () => {
      await Promise.resolve()
      if (!active) return
      setLoading(true)

      try {
        const result = await loader()
        if (active) {
          setData(result)
          setError('')
        }
      } catch (error) {
        if (active) setError(errorMessage(error))
      } finally {
        if (active) setLoading(false)
      }
    }

    void run()

    return () => {
      active = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick])

  const reload = useCallback(() => setTick(value => value + 1), [])

  return { data, loading, error, reload }
}

function StatusLine({ loading, error }: { loading?: boolean; error?: string }) {
  if (loading) return <p className="inventory-muted">Cargando datos...</p>
  if (error) return <p className="inventory-error">{error}</p>
  return null
}

function Field({
  label,
  children,
}: {
  label: string
  children: ReactNode
}) {
  return (
    <label className="inventory-field">
      <span>{label}</span>
      {children}
    </label>
  )
}

function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className="inventory-input" {...props} />
}

function Select({
  value,
  onChange,
  options,
  placeholder = 'Seleccione...',
}: {
  value: string
  onChange: (value: string) => void
  options: CatalogItem[]
  placeholder?: string
}) {
  return (
    <select className="inventory-input" value={value} onChange={event => onChange(event.target.value)}>
      <option value="">{placeholder}</option>
      {options.map(option => (
        <option key={option.id} value={option.id}>{option.name}</option>
      ))}
    </select>
  )
}

function catalogNames(list?: CatalogItem[]) {
  return new Map((list ?? []).map(item => [item.id, item.name]))
}

const dateOnly = (value?: string | null) => (value ? value.slice(0, 10) : '')
const money = (value: unknown) =>
  `RD$ ${Number(value ?? 0).toLocaleString('es-DO', { maximumFractionDigits: 0 })}`

// ============================================================
// DASHBOARD
// ============================================================

function DashboardScreen({ onOpen }: { onOpen: (id: InventorySectionId) => void }) {
  const access = useInventoryAccess()
  const dashboard = useAsync(() => itamApi.dashboard(), [])
  const assets = useAsync(() => itamApi.assets(), [])
  const catalogs = useAsync(() => itamApi.catalogs(), [])

  return (
    <>
      <StatusLine loading={dashboard.loading || assets.loading || catalogs.loading} error={dashboard.error || assets.error || catalogs.error} />
      {dashboard.data && (
        <ExecutiveDashboard
          dashboard={dashboard.data}
          assets={assets.data ?? []}
          categories={catalogs.data?.categories ?? []}
        />
      )}
      <div className="inventory-actions">
        {access.canSee('assets') && <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => onOpen('assets')}>{access.canManageAssets ? 'Gestionar activos' : 'Ver activos'}</button>}
        {access.canManageAssets && access.canSee('deliveries') && <button type="button" className="inventory-btn" onClick={() => onOpen('deliveries')}>Registrar entrega</button>}
        {access.canManageAssets && access.canSee('returns') && <button type="button" className="inventory-btn" onClick={() => onOpen('returns')}>Registrar devolución</button>}
        {access.canSee('physical') && <button type="button" className="inventory-btn" onClick={() => onOpen('physical')}>Conteo físico</button>}
      </div>
      {dashboard.data && (
        <div className="inventory-grid">
          <article className="inventory-card">
            <h3>Alertas</h3>
            <div className="inventory-value-row">
              <span>Garantías por vencer (90 días)</span>
              <strong>{dashboard.data.warrantiesExpiringSoon}</strong>
            </div>
            <div className="inventory-value-row">
              <span>Entregas activas sin devolución</span>
              <strong>{dashboard.data.activeDeliveries}</strong>
            </div>
            <div className="inventory-value-row">
              <span>Activos dañados</span>
              <strong>{dashboard.data.damagedAssets}</strong>
            </div>
            <div className="inventory-value-row">
              <span>Activos dados de baja</span>
              <strong>{dashboard.data.retiredAssets}</strong>
            </div>
          </article>
          <article className="inventory-card">
            <h3>Activos por sucursal</h3>
            <InventoryDataTable
              rows={dashboard.data.byBranch as unknown as InventoryRow[]}
              columns={[['name', 'Sucursal'], ['count', 'Activos']]}
            />
          </article>
        </div>
      )}
    </>
  )
}

// ============================================================
// ASSETS
// ============================================================

type AssetFormState = {
  assetTag: string
  name: string
  serialNumber: string
  status: string
  condition: string
  categoryId: string
  branchId: string
  departmentId: string
  manufacturerId: string
  purchaseDate: string
  purchaseCost: string
  warrantyEnd: string
  depreciationYears: string
  macAddress: string
  hostname: string
  assignedToName: string
  notes: string
}

const emptyAssetForm = (): AssetFormState => ({
  assetTag: '',
  name: '',
  serialNumber: '',
  status: 'disponible',
  condition: 'nuevo',
  categoryId: '',
  branchId: '',
  departmentId: '',
  manufacturerId: '',
  purchaseDate: '',
  purchaseCost: '',
  warrantyEnd: '',
  depreciationYears: '',
  macAddress: '',
  hostname: '',
  assignedToName: '',
  notes: '',
})

function assetToForm(asset: Asset): AssetFormState {
  return {
    assetTag: asset.assetTag ?? '',
    name: asset.name ?? '',
    serialNumber: asset.serialNumber ?? '',
    status: asset.status ?? 'disponible',
    condition: asset.condition ?? 'nuevo',
    categoryId: asset.categoryId ?? '',
    branchId: asset.branchId ?? '',
    departmentId: asset.departmentId ?? '',
    manufacturerId: asset.manufacturerId ?? '',
    purchaseDate: dateOnly(asset.purchaseDate),
    purchaseCost: asset.purchaseCost != null ? String(asset.purchaseCost) : '',
    warrantyEnd: dateOnly(asset.warrantyEnd),
    depreciationYears: asset.depreciationYears != null ? String(asset.depreciationYears) : '',
    macAddress: asset.macAddress ?? '',
    hostname: asset.hostname ?? '',
    assignedToName: asset.assignedToName ?? '',
    notes: asset.notes ?? '',
  }
}

function AssetsScreen({ action }: { action?: string }) {
  const { canManageAssets } = useInventoryAccess()
  const [statusFilter, setStatusFilter] = useState('')
  const [warrantyFilter, setWarrantyFilter] = useState('')
  const assets = useAsync(
    () => itamApi.assets({
      status: statusFilter || undefined,
      warranty: warrantyFilter || undefined,
    }),
    [statusFilter, warrantyFilter],
  )
  const catalogs = useAsync(() => itamApi.catalogs(), [])
  const [editing, setEditing] = useState<Asset | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [qrAsset, setQrAsset] = useState<Asset | null>(null)
  const handledAction = useRef<string | undefined>(undefined)

  // El boton "Nuevo Activo" del hero abre esta pantalla ya con el formulario.
  useEffect(() => {
    if (action !== 'create' || !canManageAssets || handledAction.current === action) return
    handledAction.current = action
    setEditing(null)
    setShowForm(true)
  }, [action, canManageAssets])

  const categories = useMemo(() => catalogs.data?.categories ?? [], [catalogs.data])
  const branches = useMemo(() => catalogs.data?.branches ?? [], [catalogs.data])

  const categoryNames = useMemo(() => catalogNames(categories), [categories])
  const branchNames = useMemo(() => catalogNames(branches), [branches])

  const rows = useMemo(() => {
    const list = assets.data ?? []
    return list.map(asset => ({
      ...asset,
      categoryName: asset.categoryId ? categoryNames.get(asset.categoryId) ?? '—' : '—',
      branchName: asset.branchId ? branchNames.get(asset.branchId) ?? '—' : '—',
    }))
  }, [assets.data, branchNames, categoryNames])

  const remove = async (asset: Asset) => {
    if (!window.confirm(`¿Dar de baja el activo ${asset.assetTag}?`)) return
    await itamApi.deleteAsset(asset.id)
    assets.reload()
  }

  const linkDevice = async (asset: Asset) => {
    const current = asset.deviceId ?? ''
    const deviceId = window.prompt(
      `ID del dispositivo MDM para ${asset.assetTag}\n(deje vacío para desvincular)`,
      current,
    )
    if (deviceId === null) return
    await itamApi.linkDevice(asset.id, deviceId.trim() || null)
    assets.reload()
  }

  const columns: InventoryColumn[] = [
    ['assetTag', 'Etiqueta'],
    ['name', 'Nombre'],
    ['serialNumber', 'Serie'],
    ['status', 'Estado'],
    ['condition', 'Condición'],
    ['assignedToName', 'Asignado'],
    ['categoryName', 'Categoría'],
    ['branchName', 'Sucursal'],
    ['deviceId', 'Dispositivo MDM'],
    ['osName', 'Sistema operativo'],
    ['lastUser', 'Último usuario'],
    ['purchaseCost', 'Costo'],
    {
      key: 'actions',
      label: 'Acciones',
      render: row => {
        const asset = row as unknown as Asset
        return (
          <div className="inventory-row-actions">
            {canManageAssets && <button type="button" className="inventory-btn inventory-btn--small" onClick={() => { setEditing(asset); setShowForm(true) }}>Editar</button>}
            {canManageAssets && <button type="button" className="inventory-btn inventory-btn--small" onClick={() => linkDevice(asset)}>Vincular</button>}
            <button type="button" className="inventory-btn inventory-btn--small" onClick={() => setQrAsset(asset)}>QR</button>
            <button type="button" className="inventory-btn inventory-btn--small inventory-btn--danger" onClick={() => remove(asset)}>Baja</button>
          </div>
        )
      },
    },
  ]

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Activos</h3>
            <p className="inventory-muted">{rows.length} activo(s) registrados en SQL Server.</p>
          </div>
          <div className="inventory-actions">
            <select className="inventory-input" value={statusFilter} onChange={event => setStatusFilter(event.target.value)}>
              <option value="">Todos los estados</option>
              {statusOptions.map(status => <option key={status} value={status}>{status}</option>)}
            </select>
            <select className="inventory-input" value={warrantyFilter} onChange={event => setWarrantyFilter(event.target.value)}>
              <option value="">Todas las garantías</option>
              <option value="active">Vigente</option>
              <option value="expiring">Por vencer (90 días)</option>
              <option value="expired">Vencida</option>
            </select>
            {canManageAssets && (
              <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => { setEditing(null); setShowForm(true) }}>
                Nuevo activo
              </button>
            )}
          </div>
        </header>

        <StatusLine loading={assets.loading || catalogs.loading} error={assets.error || catalogs.error} />

        <InventoryDataTable rows={rows as unknown as InventoryRow[]} columns={columns} />
      </article>

      {showForm && (
        <IframeModal
          title={editing ? `Editar activo · ${editing.assetTag}` : 'Nuevo activo'}
          subtitle="Formulario API · SQL Server cisa_ti_itam"
          src={`/inventory/embed?form=asset${editing ? `&id=${encodeURIComponent(editing.id)}` : ''}`}
          onClose={() => { setShowForm(false); setEditing(null) }}
          onSaved={() => assets.reload()}
        />
      )}

      {qrAsset && (
        <article className="inventory-card">
          <header className="inventory-card__head">
            <h3>QR · {qrAsset.assetTag}</h3>
            <button type="button" className="inventory-btn" onClick={() => setQrAsset(null)}>Cerrar</button>
          </header>
          <QRGenerator assetTag={qrAsset.assetTag} assetId={qrAsset.id} />
        </article>
      )}
    </>
  )
}

export function AssetForm({
  initial,
  categories,
  branches,
  departments,
  manufacturers,
  onSave,
  onCancel,
}: {
  initial: Asset | null
  categories: CatalogItem[]
  branches: CatalogItem[]
  departments: CatalogItem[]
  manufacturers: CatalogItem[]
  onSave: (form: AssetFormState) => Promise<void>
  onCancel: () => void
}) {
  const [form, setForm] = useState<AssetFormState>(initial ? assetToForm(initial) : emptyAssetForm())
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const update = (key: keyof AssetFormState) => (event: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm(current => ({ ...current, [key]: event.target.value }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await onSave(form)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>{initial ? 'Editar activo' : 'Nuevo activo'}</h3>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Etiqueta (vacío = generar)"><TextInput value={form.assetTag} onChange={update('assetTag')} placeholder="Ej. 21166D82C6" /></Field>
        <Field label="Nombre"><TextInput value={form.name} onChange={update('name')} required /></Field>
        <Field label="Serie"><TextInput value={form.serialNumber} onChange={update('serialNumber')} /></Field>
        <Field label="Estado">
          <select className="inventory-input" value={form.status} onChange={update('status')}>
            {statusOptions.map(status => <option key={status} value={status}>{status}</option>)}
          </select>
        </Field>
        <Field label="Condición">
          <select className="inventory-input" value={form.condition} onChange={update('condition')}>
            {conditionOptions.map(condition => <option key={condition} value={condition}>{condition}</option>)}
          </select>
        </Field>
        <Field label="Categoría"><Select value={form.categoryId} onChange={value => setForm({ ...form, categoryId: value })} options={categories} /></Field>
        <Field label="Sucursal"><Select value={form.branchId} onChange={value => setForm({ ...form, branchId: value })} options={branches} /></Field>
        <Field label="Departamento"><Select value={form.departmentId} onChange={value => setForm({ ...form, departmentId: value })} options={departments} /></Field>
        <Field label="Fabricante"><Select value={form.manufacturerId} onChange={value => setForm({ ...form, manufacturerId: value })} options={manufacturers} /></Field>
        <Field label="Fecha de compra"><TextInput type="date" value={form.purchaseDate} onChange={update('purchaseDate')} /></Field>
        <Field label="Costo"><TextInput type="number" step="0.01" value={form.purchaseCost} onChange={update('purchaseCost')} /></Field>
        <Field label="Garantía hasta"><TextInput type="date" value={form.warrantyEnd} onChange={update('warrantyEnd')} /></Field>
        <Field label="Años de depreciación"><TextInput type="number" value={form.depreciationYears} onChange={update('depreciationYears')} /></Field>
        <Field label="MAC"><TextInput value={form.macAddress} onChange={update('macAddress')} /></Field>
        <Field label="Hostname"><TextInput value={form.hostname} onChange={update('hostname')} /></Field>
        <Field label="Asignado a"><TextInput value={form.assignedToName} onChange={update('assignedToName')} /></Field>
        <Field label="Notas"><textarea className="inventory-input" value={form.notes} onChange={update('notes')} rows={2} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Guardar activo'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

// ============================================================
// EMPLOYEES
// ============================================================

function EmployeesScreen() {
  const { canManageAssets } = useInventoryAccess()
  const employees = useAsync(() => itamApi.employees({ pageSize: 500, includeInactive: true }), [])
  const catalogs = useAsync(() => itamApi.catalogs(), [])
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<Employee | null>(null)

  const departments = useMemo(() => catalogs.data?.departments ?? [], [catalogs.data])
  const branches = useMemo(() => catalogs.data?.branches ?? [], [catalogs.data])
  const departmentNames = useMemo(() => catalogNames(departments), [departments])
  const branchNames = useMemo(() => catalogNames(branches), [branches])

  const rows = (employees.data ?? []).map(employee => ({
    ...employee,
    departmentName: employee.departmentId ? departmentNames.get(employee.departmentId) ?? '—' : '—',
    branchName: employee.branchId ? branchNames.get(employee.branchId) ?? '—' : '—',
  }))

  const remove = async (employee: Employee) => {
    if (!window.confirm(`¿Desactivar al empleado ${employee.name}?`)) return
    await itamApi.deleteEmployee(employee.id)
    employees.reload()
  }

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Empleados</h3>
            <p className="inventory-muted">{rows.length} colaborador(es) en SQL Server.</p>
          </div>
          {canManageAssets && (
            <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => { setEditing(null); setShowForm(true) }}>
              Nuevo empleado
            </button>
          )}
        </header>

        <StatusLine loading={employees.loading || catalogs.loading} error={employees.error || catalogs.error} />

        <InventoryDataTable
          rows={rows as unknown as InventoryRow[]}
          columns={[
            ['code', 'Código'],
            ['name', 'Nombre'],
            ['position', 'Puesto'],
            ['departmentName', 'Departamento'],
            ['branchName', 'Sucursal'],
            ['email', 'Correo'],
            ['isActive', 'Activo'],
            {
              key: 'actions',
              label: 'Acciones',
              render: row => {
                const employee = row as unknown as Employee
                if (!canManageAssets) return <span className="inventory-muted">—</span>
                return (
                  <div className="inventory-row-actions">
                    <button type="button" className="inventory-btn inventory-btn--small" onClick={() => { setEditing(employee); setShowForm(true) }}>Editar</button>
                    <button type="button" className="inventory-btn inventory-btn--small inventory-btn--danger" onClick={() => remove(employee)}>Desactivar</button>
                  </div>
                )
              },
            },
          ]}
        />
      </article>

      {showForm && (
        <IframeModal
          title={editing ? `Editar colaborador · ${editing.code}` : 'Nuevo colaborador'}
          subtitle="Formulario API · SQL Server cisa_ti_itam"
          src={`/inventory/embed?form=employee${editing ? `&id=${encodeURIComponent(editing.id)}` : ''}`}
          onClose={() => { setShowForm(false); setEditing(null) }}
          onSaved={() => employees.reload()}
        />
      )}
    </>
  )
}

export function EmployeeForm({
  initial,
  departments,
  branches,
  onSave,
  onCancel,
}: {
  initial: Employee | null
  departments: CatalogItem[]
  branches: CatalogItem[]
  onSave: (payload: Partial<Employee>) => Promise<void>
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    code: initial?.code ?? '',
    name: initial?.name ?? '',
    position: initial?.position ?? '',
    email: initial?.email ?? '',
    phone: initial?.phone ?? '',
    departmentId: initial?.departmentId ?? '',
    branchId: initial?.branchId ?? '',
    supervisor: initial?.supervisor ?? '',
    notes: initial?.notes ?? '',
  })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await onSave({
        ...form,
        departmentId: form.departmentId || null,
        branchId: form.branchId || null,
        code: form.code || null,
        email: form.email || null,
        phone: form.phone || null,
        position: form.position || null,
        supervisor: form.supervisor || null,
        notes: form.notes || null,
        isActive: initial?.isActive ?? true,
      })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>{initial ? 'Editar empleado' : 'Nuevo empleado'}</h3>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Código"><TextInput value={form.code} onChange={event => setForm({ ...form, code: event.target.value })} /></Field>
        <Field label="Nombre"><TextInput value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} required /></Field>
        <Field label="Puesto"><TextInput value={form.position} onChange={event => setForm({ ...form, position: event.target.value })} /></Field>
        <Field label="Correo"><TextInput type="email" value={form.email} onChange={event => setForm({ ...form, email: event.target.value })} /></Field>
        <Field label="Teléfono"><TextInput value={form.phone} onChange={event => setForm({ ...form, phone: event.target.value })} /></Field>
        <Field label="Departamento"><Select value={form.departmentId} onChange={value => setForm({ ...form, departmentId: value })} options={departments} /></Field>
        <Field label="Sucursal"><Select value={form.branchId} onChange={value => setForm({ ...form, branchId: value })} options={branches} /></Field>
        <Field label="Supervisor"><TextInput value={form.supervisor} onChange={event => setForm({ ...form, supervisor: event.target.value })} /></Field>
        <Field label="Notas"><textarea className="inventory-input" rows={2} value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Guardar empleado'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

// ============================================================
// EXPORTACION OPERATIVA (Excel / PDF del filtro activo)
// ============================================================

function OperationalExport({ build, disabled }: { build: () => OperationalReport; disabled?: boolean }) {
  const [busy, setBusy] = useState<'excel' | 'pdf' | null>(null)
  const [error, setError] = useState('')

  const run = async (format: 'excel' | 'pdf') => {
    if (busy) return
    setBusy(format)
    setError('')
    try {
      const exporter = await import('./inventoryReportExport')
      const report = build()
      if (format === 'excel') await exporter.exportOperationalExcel(report)
      else await exporter.exportOperationalPdf(report)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  return (
    <span className="inventory-actions">
      <button type="button" className="inventory-btn inventory-btn--small" disabled={disabled || busy !== null} onClick={() => void run('excel')}>
        {busy === 'excel' ? 'Generando…' : 'Excel'}
      </button>
      <button type="button" className="inventory-btn inventory-btn--small" disabled={disabled || busy !== null} onClick={() => void run('pdf')}>
        {busy === 'pdf' ? 'Generando…' : 'PDF'}
      </button>
      {error && <span className="inventory-error">{error}</span>}
    </span>
  )
}

function QuickFilters<T extends string>({
  value,
  options,
  counts,
  onChange,
}: {
  value: T
  options: Record<T, string>
  counts?: Partial<Record<T, number>>
  onChange: (value: T) => void
}) {
  return (
    <span className="inventory-quick-filters" role="group" aria-label="Filtros rápidos">
      {(Object.keys(options) as T[]).map(key => (
        <button
          key={key}
          type="button"
          aria-pressed={value === key}
          className={value === key ? 'inventory-chip inventory-chip--active' : 'inventory-chip'}
          onClick={() => onChange(key)}
        >
          {options[key]}
          {counts?.[key] !== undefined ? ` (${counts[key]})` : ''}
        </button>
      ))}
    </span>
  )
}

// ============================================================
// DELIVERIES
// ============================================================

function DeliveriesScreen() {
  const { canManageAssets } = useInventoryAccess()
  const deliveries = useAsync(() => itamApi.deliveries(), [])
  const [showForm, setShowForm] = useState(false)
  const [returning, setReturning] = useState<Delivery | null>(null)
  const [filter, setFilter] = useState<DeliveryFilter>('all')

  const allRows = useMemo(() => deliveries.data ?? [], [deliveries.data])
  const rows = useMemo(() => filterDeliveries(allRows, filter), [allRows, filter])
  const counts = useMemo(
    () => ({
      all: allRows.length,
      today: filterDeliveries(allRows, 'today').length,
      pending: filterDeliveries(allRows, 'pending').length,
      overdue: filterDeliveries(allRows, 'overdue').length,
    }),
    [allRows],
  )

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Entregas</h3>
            <p className="inventory-muted">
              {counts.all} entrega(s) · {counts.today} hoy · {counts.pending} pendiente(s) de devolución
              {counts.overdue > 0 ? ` · ${counts.overdue} vencida(s)` : ''}
            </p>
          </div>
          {canManageAssets && (
            <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => setShowForm(true)}>
              Nueva entrega
            </button>
          )}
        </header>

        <StatusLine loading={deliveries.loading} error={deliveries.error} />

        <InventoryDataTable
          tableKey="itam-deliveries"
          rows={rows as unknown as InventoryRow[]}
          emptyText={filter === 'all' ? 'No hay entregas registradas.' : `Sin registros para "${deliveryFilterLabels[filter]}".`}
          toolbar={
            <>
              <QuickFilters value={filter} options={deliveryFilterLabels} counts={counts} onChange={setFilter} />
              <OperationalExport disabled={deliveries.loading} build={() => deliveryReport(rows, filter)} />
            </>
          }
          columns={[
            ['assetTag', 'Activo'],
            ['assetName', 'Equipo'],
            ['assignedToName', 'Empleado'],
            ['employeeCode', 'Código'],
            ['deliveryDate', 'Fecha'],
            ['expectedReturn', 'Retorno'],
            {
              key: 'overdue',
              label: 'Vencida',
              render: row => {
                const days = overdueDays(row as unknown as Delivery)
                return days > 0
                  ? <span className="inventory-badge inventory-badge--danger">{days} día(s)</span>
                  : <span className="inventory-muted">—</span>
              },
            },
            ['returned', 'Devuelto'],
            {
              key: 'actions',
              label: 'Acciones',
              render: row => {
                const delivery = row as unknown as Delivery
                if (delivery.returned) return <span className="inventory-muted">Cerrada</span>
                if (!canManageAssets) return <span className="inventory-muted">Activa</span>
                return (
                  <button type="button" className="inventory-btn inventory-btn--small" onClick={() => setReturning(delivery)}>
                    Devolver
                  </button>
                )
              },
            },
          ]}
        />
      </article>

      {showForm && (
        <IframeModal
          title="Nueva entrega"
          subtitle="Seleccione activo y colaborador, capture la firma."
          src="/inventory/embed?form=delivery"
          onClose={() => setShowForm(false)}
          onSaved={() => deliveries.reload()}
        />
      )}

      {returning && (
        <IframeModal
          title={`Devolución · ${returning.assetTag}`}
          subtitle={`Entrega de ${returning.assignedToName} · ${returning.deliveryDate ?? ''}`}
          src={`/inventory/embed?form=return&deliveryId=${encodeURIComponent(returning.id)}`}
          onClose={() => setReturning(null)}
          onSaved={() => deliveries.reload()}
        />
      )}
    </>
  )
}

export function DeliveryForm({ onSaved, onCancel }: { onSaved: () => void; onCancel: () => void }) {
  const [asset, setAsset] = useState<Asset | null>(null)
  const [employee, setEmployee] = useState<Employee | null>(null)
  const [expectedReturn, setExpectedReturn] = useState('')
  const [condition, setCondition] = useState('bueno')
  const [accessories, setAccessories] = useState('')
  const [notes, setNotes] = useState('')
  const [signature, setSignature] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!asset || !employee) {
      setError('Seleccione un activo y un empleado.')
      return
    }
    setSaving(true)
    setError('')
    try {
      await itamApi.createDelivery({
        assetId: asset.id,
        assetTag: asset.assetTag,
        assetName: asset.name,
        serialNumber: asset.serialNumber,
        assignedToName: employee.name,
        employeeCode: employee.code,
        departmentId: employee.departmentId,
        branchId: employee.branchId,
        expectedReturn: expectedReturn || null,
        condition,
        accessories: accessories || null,
        notes: notes || null,
        signature: signature || null,
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>Nueva entrega</h3>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Activo">
          <TypeaheadSearch
            url="/itam/assets/search"
            placeholder="Buscar por etiqueta, serie o nombre..."
            onSelect={result => setAsset(result.raw as Asset)}
          />
          {asset && <small className="inventory-muted">Seleccionado: {asset.assetTag} · {asset.name}</small>}
        </Field>

        <Field label="Empleado">
          <TypeaheadSearch
            url="/itam/employees"
            placeholder="Buscar por nombre, código o correo..."
            onSelect={result => setEmployee(result.raw as Employee)}
          />
          {employee && <small className="inventory-muted">Seleccionado: {employee.name} {employee.code ? `· ${employee.code}` : ''}</small>}
        </Field>

        <Field label="Retorno esperado"><TextInput type="date" value={expectedReturn} onChange={event => setExpectedReturn(event.target.value)} /></Field>
        <Field label="Condición de entrega">
          <select className="inventory-input" value={condition} onChange={event => setCondition(event.target.value)}>
            {conditionOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </Field>
        <Field label="Accesorios"><TextInput value={accessories} onChange={event => setAccessories(event.target.value)} placeholder="Cargador, base, funda..." /></Field>
        <Field label="Notas"><textarea className="inventory-input" rows={2} value={notes} onChange={event => setNotes(event.target.value)} /></Field>
        <Field label="Firma del empleado"><SignaturePad onChange={data => setSignature(data.base64)} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Registrar entrega'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

// ============================================================
// RETURNS
// ============================================================

function ReturnsScreen() {
  const { canManageAssets } = useInventoryAccess()
  const returns = useAsync(() => itamApi.returns(), [])
  const deliveries = useAsync(() => itamApi.deliveries({ returned: false }), [])
  const [selectedDelivery, setSelectedDelivery] = useState<Delivery | null>(null)
  const [returnFilter, setReturnFilter] = useState<ReturnFilter>('all')

  const active = useMemo(() => deliveries.data ?? [], [deliveries.data])
  const allReturns = useMemo(() => returns.data ?? [], [returns.data])
  const visibleReturns = useMemo(() => filterReturns(allReturns, returnFilter), [allReturns, returnFilter])
  const pending = useMemo(() => filterDeliveries(active, 'pending'), [active])
  const overdueCount = useMemo(() => filterDeliveries(active, 'overdue').length, [active])

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Devoluciones</h3>
            <p className="inventory-muted">{returns.data?.length ?? 0} devolución(es) registradas.</p>
          </div>
          <div className="inventory-actions" hidden={!canManageAssets}>
            <select
              className="inventory-input"
              value={selectedDelivery?.id ?? ''}
              onChange={event => setSelectedDelivery(active.find(item => item.id === event.target.value) ?? null)}
            >
              <option value="">Seleccione entrega activa...</option>
              {active.map(delivery => (
                <option key={delivery.id} value={delivery.id}>
                  {delivery.assetTag} · {delivery.assignedToName}
                </option>
              ))}
            </select>
          </div>
        </header>

        <StatusLine loading={returns.loading || deliveries.loading} error={returns.error || deliveries.error} />

        <InventoryDataTable
          tableKey="itam-returns"
          rows={visibleReturns as unknown as InventoryRow[]}
          toolbar={
            <>
              <QuickFilters
                value={returnFilter}
                options={{ all: 'Todas', today: 'Devueltas hoy' } as Record<ReturnFilter, string>}
                counts={{ all: allReturns.length, today: filterReturns(allReturns, 'today').length }}
                onChange={setReturnFilter}
              />
              <OperationalExport disabled={returns.loading} build={() => returnReport(visibleReturns, returnFilter)} />
            </>
          }
          columns={[
            ['assetTag', 'Activo'],
            ['assetName', 'Equipo'],
            ['returnedBy', 'Devuelve'],
            ['employeeCode', 'Código'],
            ['condition', 'Condición'],
            ['returnDate', 'Fecha'],
            ['receivedBy', 'Recibe'],
          ]}
        />
      </article>

      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Devoluciones pendientes</h3>
            <p className="inventory-muted">
              {pending.length} equipo(s) entregados sin devolver{overdueCount > 0 ? ` · ${overdueCount} con retorno vencido` : ''}.
            </p>
          </div>
          <OperationalExport disabled={deliveries.loading} build={() => deliveryReport(pending, 'pending')} />
        </header>

        <InventoryDataTable
          tableKey="itam-pending-returns"
          rows={pending as unknown as InventoryRow[]}
          emptyText="No hay devoluciones pendientes."
          columns={[
            ['assetTag', 'Activo'],
            ['assetName', 'Equipo'],
            ['assignedToName', 'Empleado'],
            ['deliveryDate', 'Entregado'],
            ['expectedReturn', 'Retorno previsto'],
            {
              key: 'overdue',
              label: 'Atraso',
              render: row => {
                const days = overdueDays(row as unknown as Delivery)
                return days > 0
                  ? <span className="inventory-badge inventory-badge--danger">{days} día(s)</span>
                  : <span className="inventory-badge inventory-badge--ok">En plazo</span>
              },
            },
            {
              key: 'actions',
              label: 'Acciones',
              render: row => canManageAssets ? (
                <button
                  type="button"
                  className="inventory-btn inventory-btn--small"
                  onClick={() => setSelectedDelivery(row as unknown as Delivery)}
                >
                  Registrar devolución
                </button>
              ) : <span className="inventory-muted">—</span>,
            },
          ]}
        />
      </article>

      {selectedDelivery && (
        <ReturnForm
          delivery={selectedDelivery}
          onCancel={() => setSelectedDelivery(null)}
          onSaved={() => { setSelectedDelivery(null); returns.reload(); deliveries.reload() }}
        />
      )}
    </>
  )
}

export function ReturnForm({
  delivery,
  onSaved,
  onCancel,
}: {
  delivery: Delivery
  onSaved: () => void
  onCancel: () => void
}) {
  const [condition, setCondition] = useState(delivery.condition ?? 'bueno')
  const [notes, setNotes] = useState('')
  const [signature, setSignature] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await itamApi.createReturn({
        deliveryId: delivery.id,
        assetId: delivery.assetId,
        assetTag: delivery.assetTag,
        assetName: delivery.assetName,
        employeeCode: delivery.employeeCode,
        returnedBy: delivery.assignedToName,
        condition,
        notes: notes || null,
        signature: signature || null,
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>Devolución · {delivery.assetTag}</h3>
      <p className="inventory-muted">Empleado: {delivery.assignedToName} {delivery.employeeCode ? `· ${delivery.employeeCode}` : ''}</p>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Condición de retorno">
          <select className="inventory-input" value={condition} onChange={event => setCondition(event.target.value)}>
            {conditionOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </Field>
        <Field label="Notas"><textarea className="inventory-input" rows={2} value={notes} onChange={event => setNotes(event.target.value)} /></Field>
        <Field label="Firma del empleado"><SignaturePad onChange={data => setSignature(data.base64)} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Registrar devolución'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

// ============================================================
// RECEPTIONS
// ============================================================

function ReceptionsScreen() {
  const { canManageAssets } = useInventoryAccess()
  const receptions = useAsync(() => itamApi.receptions(), [])
  const [showForm, setShowForm] = useState(false)

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Recepciones</h3>
            <p className="inventory-muted">{receptions.data?.length ?? 0} ingreso(s) registrados.</p>
          </div>
          {canManageAssets && (
            <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => setShowForm(true)}>
              Nueva recepción
            </button>
          )}
        </header>

        <StatusLine loading={receptions.loading} error={receptions.error} />

        <InventoryDataTable
          rows={(receptions.data ?? []) as unknown as InventoryRow[]}
          columns={[
            ['description', 'Descripción'],
            ['serialNumber', 'Serie'],
            ['deliveredBy', 'Entrega'],
            ['employeeCode', 'Código'],
            ['condition', 'Condición'],
            ['receptionDate', 'Fecha'],
            ['receivedBy', 'Recibe'],
          ]}
        />
      </article>

      {showForm && (
        <IframeModal
          title="Nueva recepción"
          subtitle="Registre equipos nuevos con firma y categorías."
          src="/inventory/embed?form=reception"
          onClose={() => setShowForm(false)}
          onSaved={() => receptions.reload()}
        />
      )}
    </>
  )
}

export function ReceptionForm({
  categories,
  onSaved,
  onCancel,
}: {
  categories: CatalogItem[]
  onSaved: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    description: '',
    serialNumber: '',
    categoryId: '',
    deliveredBy: '',
    employeeCode: '',
    condition: 'nuevo',
    notes: '',
  })
  const [signature, setSignature] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await itamApi.createReception({
        ...form,
        categoryId: form.categoryId || null,
        deliveredBy: form.deliveredBy || null,
        employeeCode: form.employeeCode || null,
        notes: form.notes || null,
        signature: signature || null,
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>Nueva recepción</h3>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Descripción"><TextInput value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} required /></Field>
        <Field label="Serie"><TextInput value={form.serialNumber} onChange={event => setForm({ ...form, serialNumber: event.target.value })} /></Field>
        <Field label="Categoría"><Select value={form.categoryId} onChange={value => setForm({ ...form, categoryId: value })} options={categories} /></Field>
        <Field label="Entregado por"><TextInput value={form.deliveredBy} onChange={event => setForm({ ...form, deliveredBy: event.target.value })} /></Field>
        <Field label="Código de empleado"><TextInput value={form.employeeCode} onChange={event => setForm({ ...form, employeeCode: event.target.value })} /></Field>
        <Field label="Condición">
          <select className="inventory-input" value={form.condition} onChange={event => setForm({ ...form, condition: event.target.value })}>
            {conditionOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </Field>
        <Field label="Notas"><textarea className="inventory-input" rows={2} value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} /></Field>
        <Field label="Firma"><SignaturePad onChange={data => setSignature(data.base64)} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Registrar recepción'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

// ============================================================
// PHYSICAL INVENTORY
// ============================================================

function PhysicalScreen() {
  const { canManageSessions } = useInventoryAccess()
  const sessions = useAsync(() => itamApi.sessions(), [])
  const [selected, setSelected] = useState('')
  const [showForm, setShowForm] = useState(false)

  const counts = useAsync(
    () => (selected ? itamApi.counts(selected) : Promise.resolve([] as InventoryCount[])),
    [selected],
  )
  const reconciliation = useAsync(
    () => (selected ? itamApi.reconcile(selected) : Promise.resolve(null)),
    [selected],
  )

  const current = (sessions.data ?? []).find(session => session.id === selected) ?? null

  const closeSession = async () => {
    if (!selected) return
    if (!window.confirm('¿Cerrar la sesión de conteo y guardar el reporte?')) return
    await itamApi.closeSession(selected)
    sessions.reload()
    reconciliation.reload()
  }

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>Sesiones de conteo</h3>
            <p className="inventory-muted">{sessions.data?.length ?? 0} sesión(es).</p>
          </div>
          {canManageSessions && (
            <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => setShowForm(true)}>
              Nueva sesión
            </button>
          )}
        </header>

        <StatusLine loading={sessions.loading} error={sessions.error} />

        <InventoryDataTable
          rows={(sessions.data ?? []) as unknown as InventoryRow[]}
          columns={[
            ['name', 'Sesión'],
            ['status', 'Estado'],
            ['counted', 'Contados'],
            ['createdBy', 'Creada por'],
            ['createdAt', 'Fecha'],
            {
              key: 'actions',
              label: 'Acciones',
              render: row => {
                const session = row as unknown as InventorySession
                return (
                  <button type="button" className="inventory-btn inventory-btn--small" onClick={() => setSelected(session.id)}>
                    Abrir
                  </button>
                )
              },
            },
          ]}
        />
      </article>

      {showForm && (
        <IframeModal
          title="Nueva sesión de conteo"
          subtitle="Abra el conteo físico y registre los assets encontrados."
          src="/inventory/embed?form=session"
          onClose={() => setShowForm(false)}
          onSaved={() => sessions.reload()}
        />
      )}

      {selected && (
        <article className="inventory-card">
          <header className="inventory-card__head">
            <div>
              <h3>Conteo · {current?.name ?? selected}</h3>
              <p className="inventory-muted">Estado: {current?.status ?? '—'} · Contados: {current?.counted ?? 0}</p>
            </div>
            <div className="inventory-actions">
              <button type="button" className="inventory-btn" onClick={() => reconciliation.reload()}>Recalcular</button>
              {canManageSessions && current?.status !== 'closed' && (
                <button type="button" className="inventory-btn inventory-btn--danger" onClick={closeSession}>Cerrar sesión</button>
              )}
            </div>
          </header>

          {canManageSessions && current?.status !== 'closed' && (
            <CountForm sessionId={selected} onSaved={() => { counts.reload(); reconciliation.reload(); sessions.reload() }} />
          )}

          <StatusLine loading={counts.loading || reconciliation.loading} error={counts.error || reconciliation.error} />

          {reconciliation.data && (
            <PhysicalReconciliation result={reconciliation.data as ReconcileResult} onRefresh={() => reconciliation.reload()} />
          )}

          <h4>Conteos registrados</h4>
          <InventoryDataTable
            rows={(counts.data ?? []) as unknown as InventoryRow[]}
            columns={[
              ['assetTag', 'Activo'],
              ['assetName', 'Nombre'],
              ['found', 'Encontrado'],
              ['condition', 'Condición'],
              ['locationNote', 'Ubicación'],
              ['countedBy', 'Contado por'],
              ['countedAt', 'Fecha'],
            ]}
          />
        </article>
      )}
    </>
  )
}

export function SessionForm({
  catalogs,
  onSaved,
  onCancel,
}: {
  catalogs: Catalogs | null
  onSaved: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({ name: '', branchId: '', departmentId: '', notes: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await itamApi.createSession({
        name: form.name,
        branchId: form.branchId || null,
        departmentId: form.departmentId || null,
        notes: form.notes || null,
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className="inventory-card">
      <h3>Nueva sesión de conteo</h3>
      <form className="inventory-form" onSubmit={submit}>
        <Field label="Nombre"><TextInput value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} placeholder="Conteo general Q4" /></Field>
        <Field label="Sucursal"><Select value={form.branchId} onChange={value => setForm({ ...form, branchId: value })} options={catalogs?.branches ?? []} /></Field>
        <Field label="Departamento"><Select value={form.departmentId} onChange={value => setForm({ ...form, departmentId: value })} options={catalogs?.departments ?? []} /></Field>
        <Field label="Notas"><textarea className="inventory-input" rows={2} value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} /></Field>

        {error && <p className="inventory-error">{error}</p>}

        <div className="inventory-form__actions">
          <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Crear sesión'}</button>
          <button type="button" className="inventory-btn" onClick={onCancel}>Cancelar</button>
        </div>
      </form>
    </article>
  )
}

function CountForm({ sessionId, onSaved }: { sessionId: string; onSaved: () => void }) {
  const [assetTag, setAssetTag] = useState('')
  const [assetName, setAssetName] = useState('')
  const [found, setFound] = useState(true)
  const [condition, setCondition] = useState('bueno')
  const [locationNote, setLocationNote] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!assetTag.trim()) {
      setError('Indique la etiqueta del activo.')
      return
    }
    setSaving(true)
    setError('')
    try {
      await itamApi.saveCount(sessionId, {
        assetTag: assetTag.trim(),
        assetName: assetName || null,
        found,
        condition,
        locationNote: locationNote || null,
      })
      setAssetTag('')
      setAssetName('')
      setLocationNote('')
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="inventory-inline-form" onSubmit={submit}>
      <Field label="Activo">
        <TypeaheadSearch
          url="/itam/assets/search"
          placeholder="Escanear o buscar etiqueta..."
          onSelect={result => {
            const asset = result.raw as Asset
            setAssetTag(asset.assetTag)
            setAssetName(asset.name)
          }}
        />
      </Field>
      <Field label="Etiqueta"><TextInput value={assetTag} onChange={event => setAssetTag(event.target.value)} /></Field>
      <Field label="Encontrado">
        <label className="inventory-check">
          <input type="checkbox" checked={found} onChange={event => setFound(event.target.checked)} /> Sí está físicamente
        </label>
      </Field>
      <Field label="Condición">
        <select className="inventory-input" value={condition} onChange={event => setCondition(event.target.value)}>
          {conditionOptions.map(option => <option key={option} value={option}>{option}</option>)}
        </select>
      </Field>
      <Field label="Ubicación"><TextInput value={locationNote} onChange={event => setLocationNote(event.target.value)} placeholder="Piso / área" /></Field>
      <div className="inventory-form__actions">
        <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>{saving ? 'Guardando...' : 'Registrar conteo'}</button>
      </div>
      {error && <p className="inventory-error">{error}</p>}
    </form>
  )
}

// ============================================================
// REPORTS
// ============================================================

const REPORT_STATUS_COLORS = ['#0ea5e9', '#10b981', '#8b5cf6', '#f59e0b', '#ef4444', '#6366f1', '#06b6d4']

const reportTooltipStyle = {
  backgroundColor: '#ffffff',
  border: '1px solid #e2e7ef',
  borderRadius: 12,
  color: '#172033',
  fontSize: 12,
  boxShadow: '0 18px 40px -22px rgba(23, 32, 51, 0.35)',
}

const normalizeReportKey = (value: string) =>
  value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()

function buildMonthlySeries(rows: { name: string; count: number }[]) {
  return rows.map(row => ({ name: monthKeyLabel(row.name), entregas: row.count }))
}

function ReportsScreen() {
  const access = useInventoryAccess()
  const dashboard = useAsync(() => itamApi.dashboard(), [])
  const assets = useAsync(() => itamApi.assets(), [])
  const [exporting, setExporting] = useState<'excel' | 'pdf' | null>(null)
  const [exportError, setExportError] = useState('')
  const [hiddenStatuses, setHiddenStatuses] = useState<Set<string>>(() => new Set())

  const byStatus = dashboard.data?.byStatus ?? []

  const monthlySeries = useMemo(
    () => buildMonthlySeries(dashboard.data?.monthlyAssignments ?? []),
    [dashboard.data?.monthlyAssignments],
  )

  const totalAssets = dashboard.data?.totalAssets ?? 0

  const countByAliases = (aliases: string[]) =>
    byStatus.reduce(
      (sum, item) =>
        aliases.some(alias => normalizeReportKey(item.name).includes(alias))
          ? sum + item.count
          : sum,
      0,
    )

  const inUse = countByAliases(['asignado', 'en uso', 'entregado', 'prestado'])
  const inMaintenance = countByAliases(['mantenimiento', 'reparacion'])
  const available = Math.max(totalAssets - inUse - inMaintenance, 0)

  const pieData = byStatus.map(item => ({ name: item.name, value: item.count })).filter(item => item.value > 0)
  const colorByStatus = new Map(
    pieData.map((item, index) => [item.name, REPORT_STATUS_COLORS[index % REPORT_STATUS_COLORS.length]]),
  )
  const visiblePie = pieData.filter(item => !hiddenStatuses.has(item.name))

  const toggleStatus = (name: string) => {
    setHiddenStatuses(current => {
      const next = new Set(current)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  const exportReport = async (format: 'excel' | 'pdf') => {
    if (!access.canExportReports || !dashboard.data || !assets.data || exporting) return
    setExporting(format)
    setExportError('')
    try {
      const depreciation = await itamApi.depreciation()
      const report = { dashboard: dashboard.data, assets: assets.data, depreciation }
      const exporter = await import('./inventoryReportExport')
      if (format === 'excel') await exporter.exportInventoryExcel(report)
      else await exporter.exportInventoryPdf(report)
    } catch (error) {
      setExportError(`No fue posible exportar ${format === 'excel' ? 'Excel' : 'PDF'}: ${errorMessage(error)}`)
    } finally {
      setExporting(null)
    }
  }

  const exportCsv = () => {
    if (!access.canExportReports) return
    const list = assets.data ?? []
    const header = ['asset_tag', 'name', 'serial_number', 'status', 'condition', 'purchase_cost', 'warranty_end', 'assigned_to_name']
    const lines = [header.join(',')].concat(
      list.map(asset => [
        asset.assetTag,
        asset.name,
        asset.serialNumber ?? '',
        asset.status,
        asset.condition,
        asset.purchaseCost ?? '',
        asset.warrantyEnd ?? '',
        asset.assignedToName ?? '',
      ].map(value => `"${String(value).replace(/"/g, '""')}"`).join(',')),
    )
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'inventario-activos.csv'
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <>
      <article className="inventory-card inventory-report-hero">
        <header className="inventory-card__head">
          <div>
            <div className="inventory-kicker">ANALÍTICA ITAM</div>
            <h3>Reportes y analítica</h3>
            <p className="inventory-muted">Indicadores consolidados calculados desde SQL Server.</p>
          </div>
          {access.canExportReports && (
            <div className="inventory-actions">
              <button
                type="button"
                className="inventory-btn inventory-btn--primary"
                disabled={!dashboard.data || !assets.data || exporting !== null}
                onClick={() => void exportReport('excel')}
              >
                {exporting === 'excel' ? 'Generando Excel…' : 'Exportar Excel'}
              </button>
              <button
                type="button"
                className="inventory-btn"
                disabled={!dashboard.data || !assets.data || exporting !== null}
                onClick={() => void exportReport('pdf')}
              >
                {exporting === 'pdf' ? 'Generando PDF…' : 'Exportar PDF'}
              </button>
              <button type="button" className="inventory-btn" disabled={!assets.data} onClick={exportCsv}>
                CSV
              </button>
            </div>
          )}
        </header>

        <StatusLine loading={dashboard.loading || assets.loading} error={dashboard.error || assets.error || exportError} />

        <div className="inventory-report-kpis">
          <article className="inventory-report-kpi">
            <div className="inventory-report-kpi__icon inventory-report-kpi__icon--blue">
              <Boxes size={20} aria-hidden="true" />
            </div>
            <div>
              <span>Activos totales</span>
              <strong>{totalAssets}</strong>
              <small>Inventario consolidado</small>
            </div>
          </article>
          <article className="inventory-report-kpi">
            <div className="inventory-report-kpi__icon inventory-report-kpi__icon--green">
              <UserCheck size={20} aria-hidden="true" />
            </div>
            <div>
              <span>En uso</span>
              <strong>{inUse}</strong>
              <small>Asignados a colaboradores</small>
            </div>
          </article>
          <article className="inventory-report-kpi">
            <div className="inventory-report-kpi__icon inventory-report-kpi__icon--violet">
              <Wrench size={20} aria-hidden="true" />
            </div>
            <div>
              <span>En mantenimiento</span>
              <strong>{inMaintenance}</strong>
              <small>En taller o reparación</small>
            </div>
          </article>
          <article className="inventory-report-kpi">
            <div className="inventory-report-kpi__icon inventory-report-kpi__icon--cyan">
              <Boxes size={20} aria-hidden="true" />
            </div>
            <div>
              <span>Disponibles</span>
              <strong>{available}</strong>
              <small>Listos para asignar</small>
            </div>
          </article>
        </div>

        {dashboard.data && (
          <div className="inventory-grid inventory-grid--three inventory-report-values">
            <div className="inventory-value-card"><span>Costo total</span><strong>{money(dashboard.data.totalPurchaseCost)}</strong></div>
            <div className="inventory-value-card"><span>Depreciación acumulada</span><strong>{money(dashboard.data.accumulatedDepreciation)}</strong></div>
            <div className="inventory-value-card"><span>Valor en libros</span><strong>{money(dashboard.data.bookValue)}</strong></div>
          </div>
        )}
      </article>

      <div className="inventory-report-chart-grid">
        <article className="inventory-card inventory-report-chart inventory-report-chart--wide">
          <header className="inventory-card__head">
            <div>
              <h3>Resumen general</h3>
              <p className="inventory-muted">Tendencia de entregas mensuales · últimos 12 meses</p>
            </div>
              <span className="inventory-report-legend__item">
                <i className="inventory-report-legend__dot" style={{ background: '#4169e1' }} />
                Entregas
              </span>
          </header>
          <ResponsiveContainer width="100%" height={300}>
            <AreaChart data={monthlySeries} margin={{ top: 12, right: 18, left: 0, bottom: 4 }}>
              <defs>
                <linearGradient id="itamReportArea" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#4169e1" stopOpacity={0.35} />
                  <stop offset="95%" stopColor="#4169e1" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e7ef" />
              <XAxis dataKey="name" tick={{ fill: '#7b8799', fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis allowDecimals={false} tick={{ fill: '#7b8799', fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={reportTooltipStyle}
                labelStyle={{ color: '#172033', fontWeight: 700 }}
                itemStyle={{ color: '#4169e1' }}
              />
              <Area
                type="monotone"
                dataKey="entregas"
                name="Entregas"
                stroke="#4169e1"
                strokeWidth={2.5}
                fill="url(#itamReportArea)"
                activeDot={{ r: 5, fill: '#0ea5e9', stroke: '#ffffff' }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </article>

        <article className="inventory-card inventory-report-chart">
          <header className="inventory-card__head">
            <div>
              <h3>Distribución por estado</h3>
              <p className="inventory-muted">Haz clic en la leyenda para filtrar.</p>
            </div>
          </header>
          <ResponsiveContainer width="100%" height={260}>
            <PieChart>
              <Pie
                data={visiblePie}
                dataKey="value"
                nameKey="name"
                innerRadius={54}
                outerRadius={92}
                paddingAngle={3}
                stroke="#ffffff"
              >
                {visiblePie.map(entry => (
                  <Cell key={entry.name} fill={colorByStatus.get(entry.name) ?? '#0ea5e9'} />
                ))}
              </Pie>
              <Tooltip
                contentStyle={reportTooltipStyle}
                labelStyle={{ color: '#172033', fontWeight: 700 }}
              />
            </PieChart>
          </ResponsiveContainer>
          <div className="inventory-report-legend inventory-report-legend--interactive">
            {pieData.map(item => (
              <button
                key={item.name}
                type="button"
                className={
                  hiddenStatuses.has(item.name)
                    ? 'inventory-report-legend__item is-off'
                    : 'inventory-report-legend__item'
                }
                aria-pressed={!hiddenStatuses.has(item.name)}
                onClick={() => toggleStatus(item.name)}
              >
                <i
                  className="inventory-report-legend__dot"
                  style={{ background: colorByStatus.get(item.name) }}
                />
                <span>{item.name}</span>
                <strong>{item.value}</strong>
              </button>
            ))}
          </div>
        </article>
      </div>

      {dashboard.data && (
        <div className="inventory-grid">
          <ReportTable title="Por estado" rows={dashboard.data.byStatus} />
          <ReportTable title="Por condición" rows={dashboard.data.byCondition} />
          <ReportTable title="Por categoría" rows={dashboard.data.byCategory} />
          <ReportTable title="Por sucursal" rows={dashboard.data.byBranch} />
        </div>
      )}
    </>
  )
}

function ReportTable({ title, rows }: { title: string; rows: { name: string; count: number }[] }) {
  return (
    <article className="inventory-card">
      <h3>{title}</h3>
      <InventoryDataTable
        rows={rows as unknown as InventoryRow[]}
        columns={[['name', 'Grupo'], ['count', 'Cantidad']]}
      />
    </article>
  )
}

// ============================================================
// CATALOGS
// ============================================================

type CatalogFieldDef = {
  key: string
  label: string
  type?: 'text' | 'number' | 'email' | 'url' | 'tel'
  /** Selector alimentado por otro catalogo. */
  ref?: CatalogKind
  required?: boolean
  wide?: boolean
  placeholder?: string
}

type CatalogSectionDef = {
  title: string
  singular: string
  fields: CatalogFieldDef[]
  columns: InventoryColumn[]
}

const catalogSections: Record<CatalogKind, CatalogSectionDef> = {
  departments: {
    title: 'Departamentos',
    singular: 'departamento',
    fields: [
      { key: 'name', label: 'Nombre', required: true, wide: true },
      { key: 'code', label: 'Código' },
      { key: 'manager', label: 'Gerente / responsable' },
    ],
    columns: [['name', 'Nombre'], ['code', 'Código'], ['manager', 'Gerente']],
  },
  branches: {
    title: 'Sucursales',
    singular: 'sucursal',
    fields: [
      { key: 'name', label: 'Nombre', required: true, wide: true },
      { key: 'code', label: 'Código' },
      { key: 'city', label: 'Ciudad' },
      { key: 'phone', label: 'Teléfono', type: 'tel' },
      { key: 'address', label: 'Dirección', wide: true },
    ],
    columns: [['name', 'Nombre'], ['code', 'Código'], ['city', 'Ciudad'], ['address', 'Dirección'], ['phone', 'Teléfono']],
  },
  locations: {
    title: 'Ubicaciones',
    singular: 'ubicación',
    fields: [
      { key: 'name', label: 'Nombre', required: true, wide: true, placeholder: 'Ej.: Almacén TI, Oficina 2' },
      { key: 'branchId', label: 'Sucursal', ref: 'branches', wide: true },
      { key: 'floor', label: 'Piso' },
      { key: 'area', label: 'Área' },
    ],
    columns: [['name', 'Nombre'], ['branchName', 'Sucursal'], ['floor', 'Piso'], ['area', 'Área']],
  },
  manufacturers: {
    title: 'Fabricantes',
    singular: 'fabricante',
    fields: [
      { key: 'name', label: 'Nombre', required: true, wide: true, placeholder: 'Ej.: Honeywell, Dell, HP' },
      { key: 'website', label: 'Sitio web', type: 'url', placeholder: 'https://' },
      { key: 'supportPhone', label: 'Teléfono de soporte', type: 'tel' },
    ],
    columns: [['name', 'Nombre'], ['website', 'Sitio'], ['supportPhone', 'Soporte']],
  },
  models: {
    title: 'Modelos',
    singular: 'modelo',
    fields: [
      { key: 'name', label: 'Modelo', required: true, wide: true, placeholder: 'Ej.: CT40, Latitude 5440' },
      { key: 'modelNumber', label: 'Número de modelo' },
      { key: 'manufacturerId', label: 'Fabricante', ref: 'manufacturers' },
      { key: 'categoryId', label: 'Categoría', ref: 'categories', wide: true },
    ],
    columns: [['name', 'Modelo'], ['modelNumber', 'Número'], ['manufacturerName', 'Fabricante'], ['categoryName', 'Categoría']],
  },
  categories: {
    title: 'Categorías',
    singular: 'categoría',
    fields: [
      { key: 'name', label: 'Nombre', required: true, wide: true, placeholder: 'Ej.: Handheld, Laptop' },
      { key: 'type', label: 'Tipo', placeholder: 'Ej.: hardware' },
      { key: 'depreciationYears', label: 'Años de depreciación', type: 'number' },
    ],
    columns: [['name', 'Nombre'], ['type', 'Tipo'], ['depreciationYears', 'Años depreciación']],
  },
  suppliers: {
    title: 'Proveedores',
    singular: 'proveedor',
    fields: [
      { key: 'name', label: 'Nombre / razón social', required: true, wide: true },
      { key: 'rnc', label: 'RNC' },
      { key: 'contactName', label: 'Contacto' },
      { key: 'phone', label: 'Teléfono', type: 'tel' },
      { key: 'email', label: 'Correo', type: 'email' },
      { key: 'address', label: 'Dirección', wide: true },
    ],
    columns: [['name', 'Nombre'], ['rnc', 'RNC'], ['contactName', 'Contacto'], ['phone', 'Teléfono'], ['email', 'Correo']],
  },
}

const isCatalogKind = (value: string): value is CatalogKind => value in catalogSections

/** Mensaje del API ({ message }) en lugar del generico de axios. */
const apiErrorMessage = (error: unknown) => {
  const data = (error as { response?: { data?: unknown } })?.response?.data
  if (data && typeof data === 'object' && 'message' in data && typeof data.message === 'string') return data.message
  if (typeof data === 'string' && data.trim()) return data
  return errorMessage(error)
}

function CatalogScreen({ section }: { section: string }) {
  const { canManageCatalogs } = useInventoryAccess()
  const catalogs = useAsync(() => itamApi.catalogs(), [])
  const [editing, setEditing] = useState<CatalogItem | 'new' | null>(null)
  const [notice, setNotice] = useState('')
  const kind = isCatalogKind(section) ? section : null
  const config = kind ? catalogSections[kind] : null

  const rows = useMemo(() => {
    if (!catalogs.data || !kind) return []
    const names = {
      branches: catalogNames(catalogs.data.branches),
      manufacturers: catalogNames(catalogs.data.manufacturers),
      categories: catalogNames(catalogs.data.categories),
    }
    const label = (map: Map<string, string>, id: unknown) =>
      typeof id === 'string' && id ? map.get(id) ?? '—' : '—'
    return catalogs.data[kind].map(item => ({
      ...item,
      branchName: label(names.branches, item.branchId),
      manufacturerName: label(names.manufacturers, item.manufacturerId),
      categoryName: label(names.categories, item.categoryId),
    }))
  }, [catalogs.data, kind])

  if (!config || !kind) return null

  const remove = async (item: CatalogItem) => {
    if (!window.confirm(`¿Eliminar ${config.singular} "${item.name}"?`)) return
    try {
      await itamApi.deleteCatalogItem(kind, item.id)
      setNotice(`${item.name} eliminado.`)
      catalogs.reload()
    } catch (error) {
      setNotice(apiErrorMessage(error))
    }
  }

  const columns: InventoryColumn[] = canManageCatalogs
    ? [
        ...config.columns,
        {
          key: 'actions',
          label: 'Acciones',
          render: row => {
            const item = row as unknown as CatalogItem
            return (
              <div className="inventory-row-actions">
                <button type="button" className="inventory-btn inventory-btn--small" onClick={() => setEditing(item)}>Editar</button>
                <button type="button" className="inventory-btn inventory-btn--small inventory-btn--danger" onClick={() => void remove(item)}>Eliminar</button>
              </div>
            )
          },
        },
      ]
    : config.columns

  return (
    <>
      <article className="inventory-card">
        <header className="inventory-card__head">
          <div>
            <h3>{config.title}</h3>
            <p className="inventory-muted">{rows.length} registro(s) en SQL Server.</p>
          </div>
          {canManageCatalogs && (
            <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => { setNotice(''); setEditing('new') }}>
              + Nuevo {config.singular}
            </button>
          )}
        </header>
        {notice && <p className="inventory-muted" role="status">{notice}</p>}
        <StatusLine loading={catalogs.loading} error={catalogs.error} />
        <InventoryDataTable
          rows={rows as unknown as InventoryRow[]}
          columns={columns}
          emptyText={canManageCatalogs ? `Sin registros. Use "Nuevo ${config.singular}" para agregar.` : 'Sin registros.'}
        />
      </article>

      {editing && catalogs.data && (
        <IframeModal
          size="compact"
          title={editing === 'new' ? `Nuevo ${config.singular}` : `Editar ${config.singular} · ${editing.name}`}
          subtitle="Catálogo ITAM · SQL Server cisa_ti_itam"
          onClose={() => setEditing(null)}
        >
          <CatalogItemForm
              kind={kind}
              initial={editing === 'new' ? null : editing}
              catalogs={catalogs.data!}
              onSaved={saved => {
                setNotice(`${saved.name} guardado.`)
                catalogs.reload()
              }}
            />
        </IframeModal>
      )}
    </>
  )
}

export function CatalogItemForm({
  kind,
  initial,
  catalogs,
  onSaved,
}: {
  kind: CatalogKind
  initial: CatalogItem | null
  catalogs: Catalogs
  onSaved: (item: CatalogItem) => void
}) {
  const close = useItamModalClose()
  const config = catalogSections[kind]
  const [form, setForm] = useState<Record<string, string>>(() =>
    Object.fromEntries(config.fields.map(field => {
      const value = initial?.[field.key]
      return [field.key, value === null || value === undefined ? '' : String(value)]
    })),
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!form.name?.trim()) {
      setError('El nombre es obligatorio.')
      return
    }

    const payload: CatalogItemInput = { name: form.name.trim() }
    for (const field of config.fields) {
      if (field.key === 'name') continue
      const raw = form[field.key]?.trim() ?? ''
      payload[field.key] = raw === '' ? null : field.type === 'number' ? Number(raw) : raw
    }

    try {
      setSaving(true)
      setError('')
      const saved = initial
        ? await itamApi.updateCatalogItem(kind, initial.id, payload)
        : await itamApi.createCatalogItem(kind, payload)
      close(() => onSaved(saved))
    } catch (submitError) {
      setError(apiErrorMessage(submitError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="inventory-catalog-form" onSubmit={submit} noValidate>
      {config.fields.map(field => (
        <label key={field.key} className={field.wide ? 'inventory-field inventory-field--wide' : 'inventory-field'}>
          <span>{field.label}{field.required ? ' *' : ''}</span>
          {field.ref ? (
            <Select
              value={form[field.key] ?? ''}
              onChange={value => setForm({ ...form, [field.key]: value })}
              options={catalogs[field.ref]}
              placeholder="Sin asignar"
            />
          ) : (
            <TextInput
              type={field.type ?? 'text'}
              min={field.type === 'number' ? 0 : undefined}
              max={field.type === 'number' ? 50 : undefined}
              value={form[field.key] ?? ''}
              placeholder={field.placeholder}
              required={field.required}
              autoFocus={field.key === 'name'}
              onChange={event => setForm({ ...form, [field.key]: event.target.value })}
            />
          )}
        </label>
      ))}
      {error && <p className="inventory-error inventory-field--wide" role="alert">{error}</p>}
      <div className="inventory-catalog-form__actions">
        <button type="button" className="inventory-btn" onClick={() => close()} disabled={saving}>Cancelar</button>
        <button type="submit" className="inventory-btn inventory-btn--primary" disabled={saving}>
          {saving ? 'Guardando…' : initial ? 'Guardar cambios' : 'Crear'}
        </button>
      </div>
    </form>
  )
}

// ============================================================
// MAINTENANCE
// ============================================================

function MaintenanceScreen() {
  const assets = useAsync(() => itamApi.assets(), [])

  const rows = (assets.data ?? []).filter(
    asset => asset.status === 'mantenimiento' || asset.status === 'danado' || asset.condition === 'malo',
  )

  return (
    <article className="inventory-card">
      <h3>Mantenimientos y equipos fuera de operación</h3>
      <p className="inventory-muted">
        Activos en estado mantenimiento, dañado o con condición mala. Las órdenes de mantenimiento se gestionan desde Helpdesk.
      </p>
      <StatusLine loading={assets.loading} error={assets.error} />
      <InventoryDataTable
        rows={rows as unknown as InventoryRow[]}
        columns={[
          ['assetTag', 'Etiqueta'],
          ['name', 'Nombre'],
          ['status', 'Estado'],
          ['condition', 'Condición'],
          ['assignedToName', 'Asignado'],
          ['notes', 'Notas'],
        ]}
        emptyText="No hay activos en mantenimiento o dañados."
      />
    </article>
  )
}

// ============================================================
// HISTORY / AUDIT
// ============================================================

function HistoryScreen() {
  const history = useAsync(() => itamApi.history({ limit: 300 }), [])

  return (
    <article className="inventory-card">
      <h3>Historial de activos</h3>
      <p className="inventory-muted">Movimientos registrados por el motor de inventario.</p>
      <StatusLine loading={history.loading} error={history.error} />
      <InventoryDataTable
        rows={(history.data ?? []) as unknown as InventoryRow[]}
        columns={[
          ['timestamp', 'Fecha'],
          ['userName', 'Usuario'],
          ['action', 'Acción'],
          ['detail', 'Detalle'],
          ['assetId', 'Activo'],
        ] as InventoryColumn[]}
        emptyText="Sin movimientos registrados."
      />
    </article>
  )
}

// ============================================================
// STATIC INFO
// ============================================================

function UsersScreen({ onOpen }: { onOpen: (id: InventorySectionId) => void }) {
  return (
    <article className="inventory-card">
      <h3>Usuarios y roles</h3>
      <p>El acceso se controla con los permisos inventory.view, inventory.manage y workspace.inventory.view.</p>
      <div className="inventory-actions">
        <button type="button" className="inventory-btn inventory-btn--primary" onClick={() => onOpen('assets')}>Ver activos</button>
        <button type="button" className="inventory-btn" onClick={() => onOpen('employees')}>Ver empleados</button>
      </div>
    </article>
  )
}

// ============================================================
// ENTRY
// ============================================================

export function InventoryScreen({
  id,
  onOpen,
  action,
}: {
  id: InventorySectionId
  onOpen: (id: InventorySectionId, action?: string) => void
  action?: string
}) {
  switch (id) {
    case 'dashboard':
      return <DashboardScreen onOpen={onOpen} />
    case 'assets':
      return <AssetsScreen action={action} />
    case 'employees':
      return <EmployeesScreen />
    case 'deliveries':
      return <DeliveriesScreen />
    case 'returns':
      return <ReturnsScreen />
    case 'receptions':
      return <ReceptionsScreen />
    case 'physical':
      return <PhysicalScreen />
    case 'reports':
      return <ReportsScreen />
    case 'maintenance':
      return <MaintenanceScreen />
    case 'departments':
    case 'branches':
    case 'locations':
    case 'manufacturers':
    case 'models':
    case 'categories':
    case 'suppliers':
      return <CatalogScreen section={id} />
    default:
      return (
        <>
          <HistoryScreen />
          <UsersScreen onOpen={onOpen} />
        </>
      )
  }
}
