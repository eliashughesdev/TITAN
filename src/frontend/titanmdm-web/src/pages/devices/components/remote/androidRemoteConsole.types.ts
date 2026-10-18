import type {
  LucideIcon,
} from 'lucide-react'

import type {
  AndroidCapabilityStatus,
  AndroidRemoteAction,
} from '../../../../types/androidEnterprise'

export type AndroidActionTone =
  | 'standard'
  | 'warning'
  | 'danger'

export type AndroidActionCategory =
  | 'security'
  | 'device'
  | 'recovery'
  | 'compliance'
  | 'destructive'

/**
 * Operaciones administrativas soportadas por Android Management API.
 * `action` coincide con el contrato del backend.
 */
export interface AndroidActionDefinition {
  action: AndroidRemoteAction
  label: string
  description: string
  category: AndroidActionCategory
  tone: AndroidActionTone
  icon: LucideIcon
  destructive: boolean
  requiresReauth: boolean
}

/**
 * Accesos a módulos de gestión que no son comandos directos al
 * dispositivo, pero completan la consola (aplicaciones, políticas,
 * kiosk y restricciones).
 */
export interface AndroidModuleLink {
  id: string
  label: string
  description: string
  to: string
  icon: LucideIcon
}

export type AndroidCommandState =
  | 'pending'
  | 'executing'
  | 'accepted'
  | 'failed'
  | 'expired'

export interface AndroidCommandEntry {
  id: string
  action: string
  label: string
  state: AndroidCommandState
  requestedAtUtc: string
  correlationId: string | null
  googleCommandName: string | null
  detail: string | null
  actor: string | null
}

export interface AndroidCapabilityView {
  status: AndroidCapabilityStatus
  reason: string | null
}

export interface AndroidRemoteConsoleData {
  capabilities: import('../../../../types/androidEnterprise')
    .AndroidDeviceCapabilities | null
  capabilitiesError: string | null
}
