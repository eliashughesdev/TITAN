/*
 * RBAC del modulo ITAM. Espejo exacto de los RequireAnyPermission de
 * ItamController: la UI solo muestra secciones y acciones que el API
 * va a autorizar (sin 403 al hacer clic).
 */
import { useMemo } from 'react'

import { useAuth } from '../../auth/AuthContext'
import { Permissions } from '../../auth/permissions'
import { inventoryNav, type InventoryNavGroup, type InventorySectionId } from './inventoryNav'

const P = Permissions.inventory

const READ_ANY = [P.view, P.manage]
const CATALOGS = [P.catalogsView, P.catalogsManage, ...READ_ANY]

export const SECTION_PERMISSIONS: Record<InventorySectionId, readonly string[]> = {
  dashboard: [P.dashboard, ...READ_ANY],
  reports: [P.reportsView, P.manage],
  assets: [P.assetsView, P.assetsManage, ...READ_ANY],
  maintenance: [P.assetsView, P.assetsManage, ...READ_ANY],
  employees: [P.employees, ...READ_ANY],
  deliveries: [P.deliveries, ...READ_ANY],
  returns: [P.returns, ...READ_ANY],
  receptions: [P.receptions, ...READ_ANY],
  physical: [P.sessions, P.counts, ...READ_ANY],
  departments: CATALOGS,
  branches: CATALOGS,
  locations: CATALOGS,
  manufacturers: CATALOGS,
  models: CATALOGS,
  categories: CATALOGS,
  suppliers: CATALOGS,
}

export interface InventoryAccess {
  canSee: (section: InventorySectionId) => boolean
  /** Crear/editar activos, empleados, entregas, devoluciones y recepciones. */
  canManageAssets: boolean
  /** Sesiones y conteos fisicos (ItamController exige inventory.manage). */
  canManageSessions: boolean
  /** Alta/edicion/baja de sucursales, departamentos, modelos, etc. */
  canManageCatalogs: boolean
  /** Exportacion de reportes ITAM a Excel/PDF. */
  canExportReports: boolean
  nav: InventoryNavGroup[]
}

export function buildInventoryAccess(hasPermission: (permission: string) => boolean): InventoryAccess {
  const any = (permissions: readonly string[]) => permissions.some(hasPermission)
  const canSee = (section: InventorySectionId) => any(SECTION_PERMISSIONS[section])

  return {
    canSee,
    canManageAssets: any([P.manage, P.assetsManage]),
    canManageSessions: hasPermission(P.manage),
    canManageCatalogs: any([P.catalogsManage, P.manage]),
    canExportReports: any([P.reportsExport, P.manage]),
    nav: inventoryNav
      .map(group => ({ ...group, items: group.items.filter(item => canSee(item.id)) }))
      .filter(group => group.items.length > 0),
  }
}

export function useInventoryAccess(): InventoryAccess {
  const { hasPermission } = useAuth()
  return useMemo(() => buildInventoryAccess(hasPermission), [hasPermission])
}
