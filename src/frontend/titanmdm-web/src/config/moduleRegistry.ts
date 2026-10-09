import {
  Clock3,
  Headphones,
  MonitorCog,
  Settings,
  Smartphone,
  type LucideIcon,
} from 'lucide-react'

export type TitanModuleId =
  | 'windows'
  | 'android'
  | 'helpdesk'
  | 'ponches'
  | 'administration'

export interface TitanModuleTheme {
  primary: string
  primaryDark: string
  soft: string
  border: string
  gradient: string
}

export interface TitanModuleDefinition {
  id: TitanModuleId
  title: string
  shortTitle: string
  description: string
  path: string
  icon: LucideIcon
  permissions: string[]
  enabled: boolean
  badge?: string
  theme: TitanModuleTheme
}

/*
 * ================================================================
 * WINDOWS
 * ================================================================
 */

const windowsModule:
  TitanModuleDefinition = {
    id:
      'windows',

    title:
      'Windows Management',

    shortTitle:
      'Windows',

    description:
      'Administración, inventario, políticas, seguridad y soporte remoto para equipos Windows.',

    path:
      '/dashboard?workspace=windows',

    icon:
      MonitorCog,

    permissions: [
      'workspace.windows.view',
    ],

    enabled:
      true,

    badge:
      'MDM',

    theme: {
      primary:
        '#2563eb',

      primaryDark:
        '#1d4ed8',

      soft:
        '#eff6ff',

      border:
        '#bfdbfe',

      gradient:
        'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)',
    },
  }

/*
 * ================================================================
 * ANDROID
 * ================================================================
 */

const androidModule:
  TitanModuleDefinition = {
    id:
      'android',

    title:
      'Android Management',

    shortTitle:
      'Android',

    description:
      'Administración de Android Enterprise, aplicaciones, políticas, seguridad y modo kiosk.',

    path:
      '/dashboard?workspace=android',

    icon:
      Smartphone,

    permissions: [
      'workspace.android.view',
    ],

    enabled:
      true,

    badge:
      'Enterprise',

    theme: {
      primary:
        '#16a34a',

      primaryDark:
        '#15803d',

      soft:
        '#f0fdf4',

      border:
        '#bbf7d0',

      gradient:
        'linear-gradient(135deg, #22c55e 0%, #15803d 100%)',
    },
  }

/*
 * ================================================================
 * HELPDESK
 * ================================================================
 */

const helpDeskModule:
  TitanModuleDefinition = {
    id:
      'helpdesk',

    title:
      'Mesa de Ayuda',

    shortTitle:
      'Help Desk',

    description:
      'Gestión de incidentes, solicitudes, SLA, automatización y atención al usuario.',

    path:
      '/helpdesk?workspace=helpdesk',

    icon:
      Headphones,

    /*
     * Durante la transición conservamos permisos legacy.
     *
     * El permiso principal nuevo es:
     *
     * workspace.helpdesk.view
     */

    permissions: [
      'workspace.helpdesk.view',

      'helpdesk.portal.access',
      'helpdesk.agent.access',
      'helpdesk.admin.access',

      'helpdesk.view',

      'tickets.view',
      'tickets.create',
    ],

    enabled:
      true,

    badge:
      'Entra ID',

    theme: {
      primary:
        '#7656d6',

      primaryDark:
        '#6045bb',

      soft:
        '#f5f3ff',

      border:
        '#ddd6fe',

      gradient:
        'linear-gradient(135deg, #8064dc 0%, #6045bb 100%)',
    },
  }

/*
 * ================================================================
 * PONCHES
 * ================================================================
 */

const ponchesModule:
  TitanModuleDefinition = {
    id:
      'ponches',

    title:
      'Visualizador de Ponches',

    shortTitle:
      'Ponches',

    description:
      'Asistencia, colaboradores, relojes biométricos, sincronización, reportes y Fiorella.',

    path:
      '/ponches',

    icon:
      Clock3,

    permissions: [
      'workspace.ponches.view',
      'ponches.manage',
    ],

    enabled:
      true,

    badge:
      'BioTime',

    theme: {
      primary:
        '#dc2626',

      primaryDark:
        '#991b1b',

      soft:
        '#fef2f2',

      border:
        '#fecaca',

      gradient:
        'linear-gradient(135deg, #ef4444 0%, #991b1b 100%)',
    },
  }

/*
 * ================================================================
 * ADMINISTRATION
 * ================================================================
 */

const administrationModule:
  TitanModuleDefinition = {
    id:
      'administration',

    title:
      'Administración',

    shortTitle:
      'Administración',

    description:
      'Usuarios, roles, permisos, localidades, auditoría y configuración global de TitanMDM.',

    path:
      '/settings?workspace=administration',

    icon:
      Settings,

    permissions: [
      'workspace.administration.view',
    ],

    enabled:
      true,

    badge:
      'Sistema',

    theme: {
      primary:
        '#475569',

      primaryDark:
        '#334155',

      soft:
        '#f8fafc',

      border:
        '#cbd5e1',

      gradient:
        'linear-gradient(135deg, #64748b 0%, #334155 100%)',
    },
  }

export const titanModules:
  TitanModuleDefinition[] = [
    windowsModule,
    androidModule,
    helpDeskModule,
    ponchesModule,
    administrationModule,
  ]

export function getTitanModule(
  moduleId: TitanModuleId,
): TitanModuleDefinition | undefined {
  return titanModules.find(
    module =>
      module.id ===
      moduleId,
  )
}

export function getEnabledModules():
  TitanModuleDefinition[] {
  return titanModules.filter(
    module =>
      module.enabled,
  )
}