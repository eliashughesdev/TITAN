import {
  AppWindow,
  BarChart3,
  Boxes,
  Building2,
  ClipboardCheck,
  CloudCog,
  Factory,
  FileBarChart,
  Gauge,
  Home,
  Inbox,
  LayoutDashboard,
  LayoutGrid,
  Map,
  MapPin,
  MapPinned,
  Monitor,
  MonitorSmartphone,
  Network,
  PackageOpen,
  RadioTower,
  RotateCcw,
  ScanLine,
  ScrollText,
  Settings,
  ShieldCheck,
  Smartphone,
  Tags,
  Truck,
  UserCog,
  Users,
  Workflow,
  Wrench,
  type LucideIcon,
} from 'lucide-react'

import type { TitanModuleId } from './moduleRegistry'

export interface WorkspaceNavigationItem {
  label: string
  path: string
  permission?: string
  icon: LucideIcon
}

const item = (
  label: string,
  path: string,
  icon: LucideIcon,
  permission?: string,
): WorkspaceNavigationItem => ({
  label,
  path,
  icon,
  permission,
})

export const globalNavigation: WorkspaceNavigationItem[] = [
  item('Titan Workspace', '/', Home),
  item('Mis solicitudes', '/my-support', Inbox),
]

const windowsNavigation: WorkspaceNavigationItem[] = [
  item('Dashboard', '/dashboard?workspace=windows', Gauge, 'dashboard.view'),
  item('Dispositivos', '/devices?platform=Windows&workspace=windows', MonitorSmartphone, 'devices.view'),
  item('Grupos y Flota', '/groups?workspace=windows', Network, 'devices.view'),
  item('Inscripción', '/enrollment?workspace=windows', Smartphone, 'enrollment.view'),
  item('Políticas', '/policies?workspace=windows', ClipboardCheck, 'policies.view'),
  item('Seguridad', '/security?workspace=windows', ShieldCheck, 'security.view'),
  item('Cumplimiento', '/compliance?workspace=windows', ShieldCheck, 'compliance.view'),
  item('Automatización', '/automation?workspace=windows', Workflow, 'devices.commands'),
  item('Soporte remoto', '/remote?workspace=windows', RadioTower, 'remote.view'),
  item('Reportes', '/reports?workspace=windows', FileBarChart, 'reports.view'),
]

const androidNavigation: WorkspaceNavigationItem[] = [
  item('Dashboard', '/dashboard?workspace=android', Gauge, 'dashboard.view'),
  item('Dispositivos', '/devices?platform=Android&workspace=android', Smartphone, 'devices.view'),
  item('Inscripción', '/enrollment?workspace=android', Smartphone, 'enrollment.view'),
  item('Políticas', '/policies?workspace=android', ClipboardCheck, 'policies.view'),
  item('Aplicaciones', '/apps?workspace=android', AppWindow, 'apps.view'),
  item('Seguridad', '/security?workspace=android', ShieldCheck, 'security.view'),
  item('Cumplimiento', '/compliance?workspace=android', ShieldCheck, 'compliance.view'),
  item('Kiosk', '/kiosk?workspace=android', PackageOpen, 'kiosk.view'),
  item('Titan Launcher', '/android/titan-launcher', LayoutGrid, 'android.enterprise.view'),
  item('Centro de Control', '/android/control-center?workspace=android', Smartphone, 'android.enterprise.view'),
  item('Geofencing', '/geofencing?workspace=android', MapPinned, 'geofencing.view'),
  item('Mapa de Flota', '/fleet-map?workspace=android', Map, 'geofencing.view'),
  item('Reportes', '/reports?workspace=android', FileBarChart, 'reports.view'),
]

const administrationNavigation:
  WorkspaceNavigationItem[] = [
    item(
      'Dashboard general',
      '/dashboard?workspace=global',
      Gauge,
      'dashboard.global.view',
    ),

    item(
      'Usuarios',
      '/users?workspace=administration',
      Users,
      'users.view',
    ),

    item(
      'Roles y permisos',
      '/roles?workspace=administration',
      UserCog,
      'roles.view',
    ),

    item(
      'Localidades',
      '/sites?workspace=administration',
      Building2,
      'sites.view',
    ),

    item(
      'Auditoría',
      '/audit?workspace=administration',
      ScrollText,
      'audit.view',
    ),

    item(
      'Configuración',
      '/settings?workspace=administration',
      Settings,
      'settings.view',
    ),
  ]

const helpDeskNavigation: WorkspaceNavigationItem[] = [
  item(
    'Operación de la mesa',
    '/helpdesk/operations?workspace=helpdesk',
    Users,
    'helpdesk.manage',
  ),
  item(
    'Inbox',
    '/helpdesk?workspace=helpdesk',
    Inbox,
    'tickets.view',
  ),
  item(
    'Entra ID',
    '/helpdesk/entra?workspace=helpdesk',
    CloudCog,
    'helpdesk.manage',
  ),
  item(
    'Especialidades',
    '/helpdesk/especialidades?workspace=helpdesk',
    Users,
    'helpdesk.manage',
  ),
]

const navigationByWorkspace:
  Record<TitanModuleId, WorkspaceNavigationItem[]> = {
    windows: windowsNavigation,
    android: androidNavigation,
    administration: administrationNavigation,
    helpdesk: helpDeskNavigation,
    ponches: [],
    inventory: [
      item('Panel Ejecutivo', '/inventory?section=dashboard', LayoutDashboard, 'inventory.dashboard.view'),
      item('Activos Tecnológicos', '/inventory?section=assets', Monitor, 'inventory.assets.view'),
      item('Empleados', '/inventory?section=employees', Users, 'inventory.employees.view'),
      item('Reportes', '/inventory?section=reports', BarChart3, 'inventory.reports.view'),
      item('Entregas', '/inventory?section=deliveries', Truck, 'inventory.deliveries.view'),
      item('Devoluciones', '/inventory?section=returns', RotateCcw, 'inventory.returns.view'),
      item('Recepciones', '/inventory?section=receptions', Inbox, 'inventory.receptions.view'),
      item('Mantenimientos', '/inventory?section=maintenance', Wrench, 'inventory.assets.view'),
      item('Inventario Físico', '/inventory?section=physical', ScanLine, 'inventory.sessions.view'),
      item('Departamentos', '/inventory?section=departments', Building2, 'inventory.view'),
      item('Sucursales', '/inventory?section=branches', Building2, 'inventory.view'),
      item('Ubicaciones', '/inventory?section=locations', MapPin, 'inventory.view'),
      item('Fabricantes', '/inventory?section=manufacturers', Factory, 'inventory.view'),
      item('Modelos', '/inventory?section=models', Boxes, 'inventory.view'),
      item('Categorías', '/inventory?section=categories', Tags, 'inventory.view'),
      item('Proveedores', '/inventory?section=suppliers', Factory, 'inventory.view'),
    ],
  }

export function getWorkspaceNavigation(
  workspaceId: TitanModuleId | null,
): WorkspaceNavigationItem[] {
  return workspaceId
    ? [...globalNavigation, ...navigationByWorkspace[workspaceId]]
    : [...globalNavigation]
}
