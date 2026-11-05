import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  BarChart3,
  Download,
  Plus,
  ShieldAlert,
} from 'lucide-react'

import { itamApi } from './inventoryApi'
import { findInventoryItem, type InventorySectionId } from './inventoryNav'
import { InventoryScreen } from './InventoryScreens'
import { useInventoryAccess } from './inventoryAccess'
import './InventoryPage.css'
import './inventory_theme.css'

export function InventoryPage() {
  const [params, setParams] = useSearchParams()
  const access = useInventoryAccess()
  const allowedItems = access.nav.flatMap(group => group.items)
  const [exporting, setExporting] = useState(false)

  const requested = findInventoryItem(params.get('section'))
  // Seccion sin permiso (enlace directo o rol cambiado): primera permitida.
  const current = access.canSee(requested.id) ? requested : allowedItems[0]
  const action = params.get('action') ?? undefined

  useEffect(() => {
    if (current) document.title = `${current.label} | Inventario`
  }, [current])

  useEffect(() => {
    if (current && current.id !== requested.id && params.get('section')) {
      setParams({ section: current.id }, { replace: true })
    }
  }, [current, params, requested.id, setParams])

  const open = (id: InventorySectionId, nextAction?: string) => {
    if (!access.canSee(id)) return
    setParams(nextAction ? { section: id, action: nextAction } : { section: id })
  }

  const exportInventory = async () => {
    if (exporting) return
    try {
      setExporting(true)
      const exporter = await import('./inventoryReportExport')
      const [dashboard, assets, depreciation] = await Promise.all([
        itamApi.dashboard(),
        itamApi.assets(),
        itamApi.depreciation(),
      ])
      await exporter.exportInventoryExcel({ dashboard, assets, depreciation })
    } catch (error) {
      console.error('No fue posible exportar el inventario.', error)
    } finally {
      setExporting(false)
    }
  }

  if (!current) {
    return (
      <div className="inventory-app">
        <section className="inventory-stage inventory-stage--denied">
          <ShieldAlert size={36} aria-hidden="true" />
          <h1>Sin acceso al inventario</h1>
          <p>Tu rol no incluye permisos del módulo Inventario. Solicítalos en Configuración → Roles.</p>
        </section>
      </div>
    )
  }

  return (
    <div className="inventory-app">
      <section className="inventory-hero">
        <div className="inventory-hero__glow" aria-hidden="true" />
        <div className="inventory-hero__content">
          <span className="inventory-kicker">ITAM · GESTIÓN DE ACTIVOS</span>
          <h1>Inventario</h1>
          <p>
            Control centralizado del ciclo de vida de los activos: entregas,
            devoluciones, recepciones, mantenimiento y conteo físico.
          </p>
        </div>
        <div className="inventory-hero__actions">
          {access.canManageAssets && (
            <button
              type="button"
              className="inventory-btn inventory-btn--primary"
              onClick={() => open('assets', 'create')}
            >
              <Plus size={16} aria-hidden="true" />
              Nuevo Activo
            </button>
          )}
          {access.canSee('reports') && (
            <button
              type="button"
              className="inventory-btn"
              onClick={() => open('reports')}
            >
              <BarChart3 size={16} aria-hidden="true" />
              Reportes
            </button>
          )}
          {access.canSee('reports') && access.canExportReports && (
            <button
              type="button"
              className="inventory-btn"
              disabled={exporting}
              onClick={() => void exportInventory()}
            >
              <Download size={16} aria-hidden="true" />
              {exporting ? 'Exportando...' : 'Exportar'}
            </button>
          )}
        </div>
      </section>

      <header className="inventory-top">
        <div className="inventory-top__title">
          <div className="inventory-top__mark">IN</div>
          <div>
            <strong>Inventario fijo</strong>
            <span>Módulo TitanMDM · menú superior</span>
          </div>
        </div>
        <nav className="inventory-top__groups" aria-label="Inventario">
          {allowedItems.map(item => (
            <button
              key={item.id}
              type="button"
              className={item.id === current.id ? 'is-active' : undefined}
              aria-current={item.id === current.id ? 'page' : undefined}
              onClick={() => open(item.id)}
            >
              <item.icon size={15} aria-hidden="true" />
              {item.label}
            </button>
          ))}
        </nav>
      </header>

      <section key={current.id} className="inventory-stage inventory-stage--enter">
        <header className="inventory-stage__head">
          <div>
            <div className="inventory-kicker">INVENTARIO FIJO</div>
            <h1>{current.label}</h1>
            <p>{current.subtitle}</p>
          </div>
        </header>
        <InventoryScreen id={current.id} onOpen={open} action={action} />
      </section>
    </div>
  )
}
