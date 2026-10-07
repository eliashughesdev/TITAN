export interface TitanCapability {
  id: string
  label: string
  description: string
  requiredPermissions: string[]
  mode: 'read' | 'action'
}

export const titanCapabilities:
  TitanCapability[] = [
  {
    id: 'dashboard.read',
    label: 'Consultar dashboard',
    description:
      'Consultar información general de TitanMDM.',
    requiredPermissions: [
      'dashboard.view',
    ],
    mode: 'read',
  },
  {
    id: 'devices.read',
    label: 'Consultar dispositivos',
    description:
      'Consultar inventario y estado de dispositivos.',
    requiredPermissions: [
      'devices.view',
    ],
    mode: 'read',
  },
  {
    id: 'enrollment.read',
    label: 'Consultar inscripciones',
    description:
      'Consultar información de inscripción.',
    requiredPermissions: [
      'enrollment.view',
    ],
    mode: 'read',
  },
  {
    id: 'policies.read',
    label: 'Consultar políticas',
    description:
      'Consultar políticas MDM.',
    requiredPermissions: [
      'policies.view',
    ],
    mode: 'read',
  },
  {
    id: 'apps.read',
    label: 'Consultar aplicaciones',
    description:
      'Consultar inventario de aplicaciones.',
    requiredPermissions: [
      'apps.view',
    ],
    mode: 'read',
  },
  {
    id: 'security.read',
    label: 'Consultar seguridad',
    description:
      'Consultar postura de seguridad.',
    requiredPermissions: [
      'security.view',
    ],
    mode: 'read',
  },
  {
    id: 'compliance.read',
    label: 'Consultar cumplimiento',
    description:
      'Consultar evaluaciones de cumplimiento.',
    requiredPermissions: [
      'compliance.view',
    ],
    mode: 'read',
  },
  {
    id: 'kiosk.read',
    label: 'Consultar Kiosk',
    description:
      'Consultar configuración Kiosk.',
    requiredPermissions: [
      'kiosk.view',
    ],
    mode: 'read',
  },
  {
    id: 'geofencing.read',
    label: 'Consultar geofencing',
    description:
      'Consultar zonas y asignaciones.',
    requiredPermissions: [
      'geofencing.view',
    ],
    mode: 'read',
  },
  {
    id: 'remote.read',
    label: 'Consultar soporte remoto',
    description:
      'Consultar funciones de soporte remoto.',
    requiredPermissions: [
      'remote.view',
    ],
    mode: 'read',
  },
  {
    id: 'reports.read',
    label: 'Consultar reportes',
    description:
      'Consultar reportes disponibles.',
    requiredPermissions: [
      'reports.view',
    ],
    mode: 'read',
  },
  {
    id: 'audit.read',
    label: 'Consultar auditoría',
    description:
      'Consultar registros de auditoría.',
    requiredPermissions: [
      'audit.view',
    ],
    mode: 'read',
  },
  {
    id: 'users.read',
    label: 'Consultar usuarios',
    description:
      'Consultar usuarios del tenant.',
    requiredPermissions: [
      'users.view',
    ],
    mode: 'read',
  },
  {
    id: 'roles.read',
    label: 'Consultar roles',
    description:
      'Consultar roles y permisos.',
    requiredPermissions: [
      'roles.view',
    ],
    mode: 'read',
  },
  {
    id: 'settings.read',
    label: 'Consultar configuración',
    description:
      'Consultar configuración de TitanMDM.',
    requiredPermissions: [
      'settings.view',
    ],
    mode: 'read',
  },
]

export function getAllowedTitanCapabilities(
  permissions: string[],
): TitanCapability[] {
  const permissionSet =
    new Set(permissions)

  return titanCapabilities.filter(
    (capability) =>
      capability.requiredPermissions.every(
        (permission) =>
          permissionSet.has(permission),
      ),
  )
}