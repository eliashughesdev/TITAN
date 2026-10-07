import type {
  ReactNode,
} from 'react'

export type WindowsControlTab =
  | 'overview'
  | 'inventory'
  | 'security'
  | 'updates'
  | 'processes'
  | 'services'
  | 'software'
  | 'actions'
  | 'commands'

export type JsonObject =
  Record<string, unknown>

export interface CommandDefinition {
  type: string
  label: string
  description: string
  icon: ReactNode
}

export interface WindowsProcessItem {
  processId: number
  name: string
  workingSetBytes: number
  startTimeUtc: string | null
}

export interface WindowsServiceItem {
  name: string
  displayName: string | null
  imagePath: string | null
  startType: string | null
  serviceType: string | null
}

export interface WindowsApplicationItem {
  name: string
  version: string | null
  publisher: string | null
  installLocation: string | null
  uninstallString: string | null
}

export interface UninstallTarget {
  name: string
  productCode: string
  executable: string
  arguments: string
}

export interface SecurityView {
  available: boolean

  defenderAvailable: boolean
  defenderRealTime: boolean | null
  defenderVersion: string | null

  firewallAvailable: boolean
  firewallEnabledProfiles: number | null

  bitLockerAvailable: boolean
  bitLockerProtected: boolean | null

  tpmAvailable: boolean
  tpmPresent: boolean | null
  tpmReady: boolean | null

  secureBoot: boolean | null
  uacEnabled: boolean | null
  pendingReboot: boolean | null
  remoteDesktopEnabled: boolean | null
}

export interface UpdateHistoryItem {
  title: string
  date: string | null
  resultCode: number | null
  hResult: number | null
}

export interface AvailableWindowsUpdate {
  title: string
  kbArticleIds: string[]
  severity: string | null
  rebootRequired: boolean | null
  isDownloaded: boolean | null
  eulaAccepted: boolean | null
}

export interface UpdateView {
  available: boolean

  serviceStatus: string
  serviceQuerySucceeded: boolean

  pendingReboot: boolean | null

  historyAvailable: boolean
  history: UpdateHistoryItem[]

  availableUpdatesAvailable: boolean
  availableUpdates: AvailableWindowsUpdate[]

  collectedAtUtc: string | null
}

export type SendWindowsCommand =
  (
    commandType: string,
    payload?: Record<string, unknown>,
  ) => Promise<void>