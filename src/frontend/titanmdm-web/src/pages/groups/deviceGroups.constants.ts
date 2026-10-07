import type { CommandOption } from './deviceGroups.types'

export const COMMAND_OPTIONS: CommandOption[] = [
  {
    value: 'PING',
    label: 'Ping',
    windowsOnly: false,
  },
  {
    value: 'DEVICE_INFO',
    label: 'Actualizar información',
    windowsOnly: false,
  },
  {
    value: 'DEVICE_INVENTORY',
    label: 'Inventario completo',
    windowsOnly: true,
  },
  {
    value: 'APP_INVENTORY',
    label: 'Inventario de aplicaciones',
    windowsOnly: false,
  },
  {
    value: 'PROCESS_INVENTORY',
    label: 'Inventario de procesos',
    windowsOnly: true,
  },
  {
    value: 'SERVICE_INVENTORY',
    label: 'Inventario de servicios',
    windowsOnly: true,
  },
  {
    value: 'NETWORK_INFO',
    label: 'Información de red',
    windowsOnly: true,
  },
  {
    value: 'SECURITY_STATUS',
    label: 'Estado de seguridad',
    windowsOnly: false,
  },
  {
    value: 'COMPLIANCE_CHECK',
    label: 'Evaluar cumplimiento',
    windowsOnly: false,
  },
  {
    value: 'WINDOWS_UPDATE_STATUS',
    label: 'Estado de Windows Update',
    windowsOnly: true,
  },
  {
    value: 'WINDOWS_UPDATE_SCAN',
    label: 'Buscar actualizaciones',
    windowsOnly: true,
  },
]
