import {
  AppWindow,
  ClipboardCheck,
  FileBarChart,
  Gauge,
  Home,
  MapPinned,
  MonitorSmartphone,
  Network,
  PackageOpen,
  RadioTower,
  ScrollText,
  Settings,
  ShieldCheck,
  Smartphone,
  UserCog,
  Users,
  Workflow,
  Inbox,
  CloudCog,
  Building2,
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
  item('Geofencing', '/geofencing?workspace=android', MapPinned, 'geofencing.view'),
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
  }

export function getWorkspaceNavigation(
  workspaceId: TitanModuleId | null,
): WorkspaceNavigationItem[] {
  return workspaceId
    ? [...globalNavigation, ...navigationByWorkspace[workspaceId]]
    : [...globalNavigation]
}