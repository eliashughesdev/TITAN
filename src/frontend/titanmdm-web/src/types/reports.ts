export interface FleetOverview {
  totalDevices: number
  onlineDevices: number
  offlineDevices: number
  managedDevices: number
  unmanagedDevices: number
  windowsDevices: number
  androidDevices: number
  compliantDevices: number
  nonCompliantDevices: number
  quarantinedDevices: number
}

export interface CommandOverview {
  totalLast30Days: number
  successful: number
  failed: number
  timeout: number
  cancelled: number
  active: number
  successRate: number
}

export interface SecurityOverview {
  evaluatedDevices: number
  averageComplianceScore: number
  criticalRiskDevices: number
  highRiskDevices: number
}

export interface ReportBreakdown {
  label: string
  value: number
}

export interface ReportsOverview {
  generatedAtUtc: string

  fleet: FleetOverview
  commands: CommandOverview
  security: SecurityOverview

  operatingSystems:
    ReportBreakdown[]

  departments:
    ReportBreakdown[]

  commandTypes:
    ReportBreakdown[]
}