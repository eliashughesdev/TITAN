import { useEffect, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  itamApi,
  type Asset,
  type Catalogs,
  type Delivery,
  type Employee,
} from './inventoryApi'
import {
  AssetForm,
  DeliveryForm,
  EmployeeForm,
  ReceptionForm,
  ReturnForm,
  SessionForm,
} from './InventoryScreens'
import './InventoryPage.css'
import './inventory_theme.css'

type FormName = 'asset' | 'employee' | 'delivery' | 'return' | 'reception' | 'session'

const formTitles: Record<FormName, { title: string; subtitle: string }> = {
  asset: { title: 'Activo de inventario', subtitle: 'Registre o actualice los datos del equipo.' },
  employee: { title: 'Colaborador', subtitle: 'Datos del empleado para asignaciones y devoluciones.' },
  delivery: { title: 'Nueva entrega', subtitle: 'Asigne un equipo disponible y capture la firma.' },
  return: { title: 'Registrar devolución', subtitle: 'Reciba el equipo y documente su condición.' },
  reception: { title: 'Nueva recepción', subtitle: 'Ingrese equipos nuevos al inventario.' },
  session: { title: 'Sesión de conteo', subtitle: 'Defina el alcance del conteo físico.' },
}

function notify(record?: unknown) {
  window.parent.postMessage({ type: 'FORM_SAVED', record }, window.location.origin)
}

/* true cuando el formulario se abre dentro de IframeModal. */
function isEmbedded() {
  try {
    return window.self !== window.top
  } catch {
    return true
  }
}

function closeFrame() {
  window.parent.postMessage({ type: 'CLOSE_IFRAME' }, window.location.origin)
}

function useAsync<T>(loader: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    loader()
      .then(result => {
        if (!active) return
        setData(result)
        setError('')
      })
      .catch((err: unknown) => {
        if (!active) return
        setError(err instanceof Error ? err.message : 'No se pudo cargar la información.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)

  return { data, loading, error }
}

function Frame({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle: string
  children: ReactNode
}) {
  return (
    <main className={isEmbedded() ? 'inv-form-page inv-form-page--embedded' : 'inv-form-page'}>
      <div className="inv-form-shell">
        <header>
          <div>
            <strong>{title}</strong>
            <p>{subtitle}</p>
          </div>
          <div>
            <button type="button" className="inventory-btn" onClick={closeFrame}>Cerrar</button>
          </div>
        </header>
        {children}
      </div>
    </main>
  )
}

function LoadingFrame({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <Frame title={title} subtitle={subtitle}>
      <article className="inventory-card">
        <p className="inventory-muted">Cargando formulario...</p>
      </article>
    </Frame>
  )
}

function ErrorFrame({ title, subtitle, message }: { title: string; subtitle: string; message: string }) {
  return (
    <Frame title={title} subtitle={subtitle}>
      <article className="inventory-card">
        <p className="inventory-error">{message}</p>
      </article>
    </Frame>
  )
}

export function InventoryEmbedPage() {
  const [params] = useSearchParams()
  const form = (params.get('form') || 'delivery') as FormName
  const id = params.get('id') ?? ''
  const deliveryId = params.get('deliveryId') ?? id
  const meta = formTitles[form] ?? formTitles.delivery

  if (form === 'asset') return <AssetEmbed id={id} meta={meta} />
  if (form === 'employee') return <EmployeeEmbed id={id} meta={meta} />
  if (form === 'delivery') return <DeliveryEmbed meta={meta} />
  if (form === 'return') return <ReturnEmbed deliveryId={deliveryId} meta={meta} />
  if (form === 'reception') return <ReceptionEmbed meta={meta} />
  return <SessionEmbed meta={meta} />
}

function AssetEmbed({ id, meta }: { id: string; meta: { title: string; subtitle: string } }) {
  const catalogs = useAsync(() => itamApi.catalogs(), [])
  const asset = useAsync(
    () => (id ? itamApi.asset(id) : Promise.resolve(null)),
    [id],
  )

  if (catalogs.loading || asset.loading) return <LoadingFrame {...meta} />
  if (catalogs.error) return <ErrorFrame {...meta} message={catalogs.error} />
  if (asset.error) return <ErrorFrame {...meta} message={asset.error} />

  return (
    <Frame {...meta}>
      <AssetForm
        initial={asset.data}
        categories={catalogs.data?.categories ?? []}
        branches={catalogs.data?.branches ?? []}
        departments={catalogs.data?.departments ?? []}
        manufacturers={catalogs.data?.manufacturers ?? []}
        onCancel={closeFrame}
        onSave={async form => {
          const payload: Partial<Asset> = {
            assetTag: form.assetTag,
            name: form.name,
            serialNumber: form.serialNumber || null,
            status: form.status,
            condition: form.condition,
            categoryId: form.categoryId || null,
            branchId: form.branchId || null,
            departmentId: form.departmentId || null,
            manufacturerId: form.manufacturerId || null,
            purchaseDate: form.purchaseDate || null,
            purchaseCost: form.purchaseCost ? Number(form.purchaseCost) : null,
            warrantyEnd: form.warrantyEnd || null,
            depreciationYears: form.depreciationYears ? Number(form.depreciationYears) : null,
            macAddress: form.macAddress || null,
            hostname: form.hostname || null,
            assignedToName: form.assignedToName || null,
            notes: form.notes || null,
          }

          const saved = id
            ? await itamApi.updateAsset(id, payload)
            : await itamApi.createAsset(payload)

          notify(saved)
        }}
      />
    </Frame>
  )
}

function EmployeeEmbed({ id, meta }: { id: string; meta: { title: string; subtitle: string } }) {
  const catalogs = useAsync(() => itamApi.catalogs(), [])
  const employees = useAsync(
    () => (id ? itamApi.employees({ pageSize: 500, includeInactive: true }) : Promise.resolve([] as Employee[])),
    [id],
  )

  if (catalogs.loading || employees.loading) return <LoadingFrame {...meta} />
  if (catalogs.error) return <ErrorFrame {...meta} message={catalogs.error} />
  if (employees.error) return <ErrorFrame {...meta} message={employees.error} />

  const initial = id ? (employees.data ?? []).find(employee => employee.id === id) ?? null : null

  return (
    <Frame {...meta}>
      <EmployeeForm
        initial={initial}
        departments={catalogs.data?.departments ?? []}
        branches={catalogs.data?.branches ?? []}
        onCancel={closeFrame}
        onSave={async payload => {
          const saved = id
            ? await itamApi.updateEmployee(id, payload)
            : await itamApi.createEmployee(payload)

          notify(saved)
        }}
      />
    </Frame>
  )
}

function DeliveryEmbed({ meta }: { meta: { title: string; subtitle: string } }) {
  return (
    <Frame {...meta}>
      <DeliveryForm
        onCancel={closeFrame}
        onSaved={() => notify({ entity: 'delivery' })}
      />
    </Frame>
  )
}

function ReturnEmbed({ deliveryId, meta }: { deliveryId: string; meta: { title: string; subtitle: string } }) {
  const deliveries = useAsync(() => itamApi.deliveries(), [])

  if (deliveries.loading) return <LoadingFrame {...meta} />
  if (deliveries.error) return <ErrorFrame {...meta} message={deliveries.error} />

  const delivery: Delivery | null =
    (deliveries.data ?? []).find(item => item.id === deliveryId) ?? null

  if (!delivery) {
    return <ErrorFrame {...meta} message="No se encontró la entrega activa seleccionada." />
  }

  return (
    <Frame {...meta}>
      <ReturnForm
        delivery={delivery}
        onCancel={closeFrame}
        onSaved={() => notify({ entity: 'return' })}
      />
    </Frame>
  )
}

function ReceptionEmbed({ meta }: { meta: { title: string; subtitle: string } }) {
  const catalogs = useAsync(() => itamApi.catalogs(), [])

  if (catalogs.loading) return <LoadingFrame {...meta} />
  if (catalogs.error) return <ErrorFrame {...meta} message={catalogs.error} />

  return (
    <Frame {...meta}>
      <ReceptionForm
        categories={catalogs.data?.categories ?? []}
        onCancel={closeFrame}
        onSaved={() => notify({ entity: 'reception' })}
      />
    </Frame>
  )
}

function SessionEmbed({ meta }: { meta: { title: string; subtitle: string } }) {
  const catalogs = useAsync(() => itamApi.catalogs(), [])

  if (catalogs.loading) return <LoadingFrame {...meta} />
  if (catalogs.error) return <ErrorFrame {...meta} message={catalogs.error} />

  const catalogData: Catalogs = catalogs.data ?? {
    categories: [],
    branches: [],
    departments: [],
    manufacturers: [],
    suppliers: [],
    models: [],
    locations: [],
  }

  return (
    <Frame {...meta}>
      <SessionForm
        catalogs={catalogData}
        onCancel={closeFrame}
        onSaved={() => notify({ entity: 'session' })}
      />
    </Frame>
  )
}
