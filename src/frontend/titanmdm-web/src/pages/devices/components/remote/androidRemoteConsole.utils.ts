import {
  Ban,
  Clock3,
  KeyRound,
  LockKeyhole,
  MapPin,
  MapPinOff,
  RotateCcw,
  ShieldAlert,
  Smartphone,
  Trash2,
} from 'lucide-react'

import type {
  AndroidCapability,
  AndroidCapabilityStatus,
  AndroidDeviceCapabilities,
  AndroidRemoteAction,
} from '../../../../types/androidEnterprise'

import type {
  AndroidActionDefinition,
  AndroidCapabilityView,
  AndroidCommandState,
} from './androidRemoteConsole.types'

export const androidActionCatalog: AndroidActionDefinition[] = [
  {
    action: 'lock',
    label: 'Bloquear pantalla',
    description: 'Solicita el bloqueo inmediato de la pantalla.',
    category: 'security',
    tone: 'standard',
    icon: LockKeyhole,
    destructive: false,
    requiresReauth: false,
  },
  {
    action: 'reboot',
    label: 'Reiniciar dispositivo',
    description: 'Reinicia el dispositivo cuando Android lo admite.',
    category: 'device',
    tone: 'warning',
    icon: RotateCcw,
    destructive: false,
    requiresReauth: false,
  },
  {
    action: 'reset-password',
    label: 'Restablecer contraseña',
    description: 'Define una nueva contraseña de pantalla.',
    category: 'security',
    tone: 'warning',
    icon: KeyRound,
    destructive: false,
    requiresReauth: true,
  },
  {
    action: 'start-lost-mode',
    label: 'Iniciar modo perdido',
    description: 'Muestra el mensaje de recuperación empresarial.',
    category: 'recovery',
    tone: 'warning',
    icon: MapPin,
    destructive: false,
    requiresReauth: false,
  },
  {
    action: 'stop-lost-mode',
    label: 'Detener modo perdido',
    description: 'Finaliza el modo perdido activo.',
    category: 'recovery',
    tone: 'standard',
    icon: MapPinOff,
    destructive: false,
    requiresReauth: false,
  },
  {
    action: 'wipe',
    label: 'Borrar dispositivo',
    description: 'Solicita un borrado de fábrica irreversible.',
    category: 'destructive',
    tone: 'danger',
    icon: Trash2,
    destructive: true,
    requiresReauth: true,
  },
  {
    action: 'delete',
    label: 'Eliminar de Android Enterprise',
    description: 'Elimina el recurso administrado de Google.',
    category: 'destructive',
    tone: 'danger',
    icon: Ban,
    destructive: true,
    requiresReauth: true,
  },
]

const actionLabels: Record<string, string> = {
  lock: 'Bloquear pantalla',
  reboot: 'Reiniciar dispositivo',
  'reset-password': 'Restablecer contraseña',
  'start-lost-mode': 'Iniciar modo perdido',
  'stop-lost-mode': 'Detener modo perdido',
  wipe: 'Borrar dispositivo',
  delete: 'Eliminar de Android Enterprise',
}

export const commandStateLabels: Record<AndroidCommandState, string> = {
  pending: 'Pendiente',
  executing: 'Ejecutando',
  accepted: 'Aceptado por Google',
  failed: 'Fallido',
  expired: 'Expirada',
}

export const capabilityStatusLabels: Record<
  AndroidCapabilityStatus,
  string
> = {
  Available: 'Compatible',
  NotSupported: 'No compatible',
  PendingValidation: 'Pendiente de validación',
}

export function actionLabel(action: string): string {
  return actionLabels[action] ?? action
}

export function formatConsoleDate(
  value?: string | null,
): string {
  if (!value) return 'Sin información'

  const parsed = new Date(value)

  return Number.isNaN(parsed.getTime())
    ? 'Sin información'
    : parsed.toLocaleString('es-DO')
}

export function consoleErrorMessage(error: unknown): string {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error
  ) {
    const response = (
      error as {
        response?: {
          data?: {
            message?: string
            code?: string
          }
        }
      }
    ).response

    if (response?.data?.message) return response.data.message
    if (response?.data?.code) return response.data.code
  }

  return error instanceof Error
    ? error.message
    : 'La operación no pudo completarse.'
}

export function findCapability(
  capabilities: AndroidDeviceCapabilities | null,
  action: AndroidRemoteAction | string,
): AndroidCapabilityView {
  const capability: AndroidCapability | undefined =
    capabilities?.actions.find(item => item.action === action)

  return {
    status: capability?.status ?? 'PendingValidation',
    reason:
      capability?.reason ??
      'Sin matriz de compatibilidad disponible para el dispositivo.',
  }
}

export function isActionEnabled(
  view: AndroidCapabilityView,
  canExecute: boolean,
  busy: boolean,
): boolean {
  return (
    canExecute &&
    !busy &&
    view.status === 'Available'
  )
}

export function auditResultToCommandState(
  result: string,
): AndroidCommandState {
  const normalized = result.toLowerCase()

  if (normalized === 'failure' || normalized === 'denied') {
    return 'failed'
  }

  return 'accepted'
}

export interface AndroidIdentitySummary {
  manufacturer: string
  model: string
  androidVersion: string
  apiLevel: string
  managementMode: string
  ownership: string
  securityPatch: string
}

export function buildIdentity(
  capabilities: AndroidDeviceCapabilities | null,
): AndroidIdentitySummary {
  return {
    manufacturer:
      capabilities?.manufacturer?.trim() || 'Fabricante desconocido',
    model: capabilities?.model?.trim() || 'Modelo desconocido',
    androidVersion:
      capabilities?.androidVersion?.trim() || 'No determinada',
    apiLevel: capabilities?.apiLevel
      ? `API ${capabilities.apiLevel}`
      : 'Sin confirmar',
    managementMode:
      capabilities?.managementMode?.trim() || 'Modo sin verificar',
    ownership: capabilities?.ownership?.trim() || 'Sin verificar',
    securityPatch:
      capabilities?.securityPatchLevel?.trim() || 'Sin información',
  }
}

export const AndroidIdentityIcon = Smartphone
export const AndroidPendingIcon = Clock3
export const AndroidWarningIcon = ShieldAlert
