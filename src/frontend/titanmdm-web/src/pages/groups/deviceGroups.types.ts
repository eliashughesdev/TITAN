export type GroupMode = 'static' | 'dynamic'

export type PlatformFilter =
  | 'All'
  | 'Windows'
  | 'Android'

export type StatusFilter =
  | 'All'
  | 'Online'
  | 'Offline'
  | 'Quarantined'

export interface CommandOption {
  value: string
  label: string
  windowsOnly: boolean
}
