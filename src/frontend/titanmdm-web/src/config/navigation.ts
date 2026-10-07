import {
  AppWindow,
  ClipboardCheck,
  FileBarChart,
  Home,
  MapPinned,
  MonitorSmartphone,
  Network,
  PackageOpen,
  PanelsTopLeft,
  RadioTower,
  ScrollText,
  Settings,
  ShieldCheck,
  Smartphone,
  UserCog,
  Users,
  Workflow,
  Headphones,
  Inbox,
  type LucideIcon,
} from 'lucide-react'

export interface NavigationItem {
  label: string

  path: string

  /*
   * Si no existe permission,
   * cualquier usuario autenticado
   * podrá visualizar el elemento.
   */
  permission?: string

  icon: LucideIcon
}

export const navigationItems:
  NavigationItem[] = [
    {
      label:
        'Inicio',

      path:
        '/',

      icon:
        Home,
    },

    {
      label:
        'Dashboard',

      path:
        '/dashboard',

      permission:
        'dashboard.view',

      icon:
        PanelsTopLeft,
    },

    {
      label:
        'Dispositivos',

      path:
        '/devices',

      permission:
        'devices.view',

      icon:
        MonitorSmartphone,
    },

    {
      label:
        'Grupos y Flota',

      path:
        '/groups',

      permission:
        'devices.view',

      icon:
        Network,
    },

    {
      label:
        'Inscripción',

      path:
        '/enrollment',

      permission:
        'enrollment.view',

      icon:
        Smartphone,
    },

    {
      label:
        'Políticas',

      path:
        '/policies',

      permission:
        'policies.view',

      icon:
        ClipboardCheck,
    },

    {
      label:
        'Aplicaciones',

      path:
        '/apps',

      permission:
        'apps.view',

      icon:
        AppWindow,
    },

    {
      label:
        'Seguridad',

      path:
        '/security',

      permission:
        'security.view',

      icon:
        ShieldCheck,
    },

    {
      label:
        'Cumplimiento',

      path:
        '/compliance',

      permission:
        'compliance.view',

      icon:
        PanelsTopLeft,
    },

    {
      label:
        'Kiosk',

      path:
        '/kiosk',

      permission:
        'kiosk.view',

      icon:
        PackageOpen,
    },

    {
      label:
        'Geofencing',

      path:
        '/geofencing',

      permission:
        'geofencing.view',

      icon:
        MapPinned,
    },

    {
      label:
        'Automatización',

      path:
        '/automation',

      permission:
        'devices.commands',

      icon:
        Workflow,
    },

    {
      label:
        'Soporte remoto',

      path:
        '/remote',

      permission:
        'remote.view',

      icon:
        RadioTower,
    },
    {
  label: 'Mesa de ayuda',
  path: '/helpdesk?workspace=helpdesk',
  permission: 'tickets.view',
  icon: Headphones,
},
{
  label: 'Operación de la mesa',
  path: '/helpdesk/operations?workspace=helpdesk',
  permission: 'helpdesk.manage',
  icon: MapPinned,
},
{
  label: 'Mis solicitudes',
  path: '/my-support',
  icon: Inbox,
},

    {
      label:
        'Reportes',

      path:
        '/reports',

      permission:
        'reports.view',

      icon:
        FileBarChart,
    },

    {
      label:
        'Auditoría',

      path:
        '/audit',

      permission:
        'audit.view',

      icon:
        ScrollText,
    },

    {
      label:
        'Usuarios',

      path:
        '/users',

      permission:
        'users.view',

      icon:
        Users,
    },

    {
      label:
        'Roles',

      path:
        '/roles',

      permission:
        'roles.view',

      icon:
        UserCog,
    },

    {
      label:
        'Configuración',

      path:
        '/settings',

      permission:
        'settings.view',

      icon:
        Settings,
    },
  ]