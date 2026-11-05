import { describe, expect, it } from 'vitest'

import { INVENTORY_ACCESS_PERMISSIONS } from '../../auth/permissions'
import { buildInventoryAccess } from './inventoryAccess'

const access = (...granted: string[]) => buildInventoryAccess(code => granted.includes(code))

describe('inventoryAccess', () => {
  it('sin permisos de inventario no ve ninguna seccion', () => {
    const result = access('dashboard.view', 'workspace.windows.view')

    expect(result.nav).toHaveLength(0)
    expect(result.canManageAssets).toBe(false)
  })

  it('solo entregas: ve Entregas, no puede registrar', () => {
    const result = access('inventory.deliveries.view')
    const ids = result.nav.flatMap(group => group.items.map(item => item.id))

    expect(ids).toEqual(['deliveries'])
    expect(result.canManageAssets).toBe(false)
    expect(result.canSee('assets')).toBe(false)
  })

  it('assets.manage permite gestionar activos pero no sesiones de conteo', () => {
    const result = access('inventory.assets.manage')

    expect(result.canManageAssets).toBe(true)
    expect(result.canManageSessions).toBe(false)
    expect(result.canSee('assets')).toBe(true)
  })

  it('reportes exige reports.view y la exportacion reports.export', () => {
    const reader = access('inventory.reports.view')
    const exporter = access('inventory.reports.view', 'inventory.reports.export')

    expect(reader.canSee('reports')).toBe(true)
    expect(reader.canExportReports).toBe(false)
    expect(exporter.canExportReports).toBe(true)
  })

  it('inventory.manage lo ve y gestiona todo', () => {
    const result = access('inventory.manage')

    expect(result.nav.flatMap(group => group.items)).toHaveLength(16)
    expect(result.canManageSessions).toBe(true)
  })

  it('la ruta del modulo solo acepta permisos de inventario', () => {
    expect(INVENTORY_ACCESS_PERMISSIONS.every(code => code.includes('inventory'))).toBe(true)
    expect(INVENTORY_ACCESS_PERMISSIONS).not.toContain('dashboard.view')
  })
})
