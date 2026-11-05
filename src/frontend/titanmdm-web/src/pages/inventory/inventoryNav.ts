import type { LucideIcon } from 'lucide-react'
import {
  BarChart3,
  Boxes,
  Building2,
  Factory,
  Inbox,
  LayoutDashboard,
  MapPin,
  Monitor,
  RotateCcw,
  ScanLine,
  Tags,
  Truck,
  UserSquare2,
  Wrench,
} from 'lucide-react'

export type InventorySectionId =
  | 'dashboard'
  | 'assets'
  | 'employees'
  | 'reports'
  | 'deliveries'
  | 'returns'
  | 'receptions'
  | 'maintenance'
  | 'physical'
  | 'departments'
  | 'branches'
  | 'locations'
  | 'manufacturers'
  | 'models'
  | 'categories'
  | 'suppliers'

export type InventoryNavItem = {
  id: InventorySectionId
  label: string
  icon: LucideIcon
  subtitle: string
}

export type InventoryNavGroup = {
  title: string
  items: InventoryNavItem[]
}

export const inventoryNav: InventoryNavGroup[] = [
  {
    title: 'General',
    items: [
      { id: 'dashboard', label: 'Panel', icon: LayoutDashboard, subtitle: 'Visión operativa del inventario fijo.' },
      { id: 'assets', label: 'Activos', icon: Monitor, subtitle: 'Equipos, series, garantías y estado.' },
      { id: 'employees', label: 'Empleados', icon: UserSquare2, subtitle: 'Colaboradores que reciben equipos.' },
      { id: 'reports', label: 'Reportes', icon: BarChart3, subtitle: 'Consultas operativas del inventario.' },
    ],
  },
  {
    title: 'Operación',
    items: [
      { id: 'deliveries', label: 'Entregas', icon: Truck, subtitle: 'Asignaciones activas e históricas.' },
      { id: 'returns', label: 'Devoluciones', icon: RotateCcw, subtitle: 'Retornos, condición y vencimientos.' },
      { id: 'receptions', label: 'Recepciones', icon: Inbox, subtitle: 'Ingreso de equipos nuevos.' },
      { id: 'maintenance', label: 'Mantenimientos', icon: Wrench, subtitle: 'Preventivos y correctivos.' },
      { id: 'physical', label: 'Conteo físico', icon: ScanLine, subtitle: 'Sesiones y conciliación.' },
    ],
  },
  {
    title: 'Catálogos',
    items: [
      { id: 'departments', label: 'Departamentos', icon: Building2, subtitle: 'Áreas organizacionales.' },
      { id: 'branches', label: 'Sucursales', icon: Building2, subtitle: 'Sedes y plantas.' },
      { id: 'locations', label: 'Ubicaciones', icon: MapPin, subtitle: 'Pisos y áreas.' },
      { id: 'manufacturers', label: 'Fabricantes', icon: Factory, subtitle: 'Marcas de los activos.' },
      { id: 'models', label: 'Modelos', icon: Boxes, subtitle: 'Modelos de equipos.' },
      { id: 'categories', label: 'Categorías', icon: Tags, subtitle: 'Clasificación y depreciación.' },
      { id: 'suppliers', label: 'Proveedores', icon: Factory, subtitle: 'Suplidores de equipos.' },
    ],
  },
]

export const inventoryItems = inventoryNav.flatMap(group => group.items)

export function findInventoryItem(id: string | null) {
  return inventoryItems.find(item => item.id === id) ?? inventoryItems[0]
}
