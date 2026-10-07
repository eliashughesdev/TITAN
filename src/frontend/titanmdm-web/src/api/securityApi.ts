import apiClient from './apiClient'

export interface SecurityDashboard {
  totalDevices: number
  evaluatedDevices: number
  compliantDevices: number
  nonCompliantDevices: number
  rootedDevices: number
  adbEnabledDevices: number
  developerModeDevices: number
  unsecuredDevices: number
  criticalRiskDevices: number
  averageComplianceScore: number
}

export interface ComplianceFinding {
  code: string
  category: string
  severity: string
  compliant: boolean
  title: string
  description: string
}

export interface DeviceSecurity {
  deviceId: string
  deviceName: string
  platform: string
  status: string
  complianceStatus: string
  complianceScore: number
  riskLevel: string
  deviceSecure: boolean
  encryptionStatus: string
  adbEnabled: boolean
  developerOptionsEnabled: boolean
  rootDetected: boolean
  emulatorDetected: boolean
  bootloaderLocked: boolean | null
  selinuxEnforced: boolean | null
  agentInstalled: boolean
  agentVersionName: string
  securityPatchLevel: string | null
  totalChecks: number
  passedChecks: number
  failedChecks: number
  findingsJson: string
  lastSecurityScanAtUtc: string | null
  lastComplianceCheckAtUtc: string | null
}

export interface CreateDeviceCommandRequest {
  deviceId: string
  commandType: string
  payloadJson?: string | null
  expiresInMinutes?: number
}

export const securityApi = {
  async getDashboard(): Promise<SecurityDashboard> {
    const response =
      await apiClient.get<SecurityDashboard>(
        '/security/dashboard',
      )

    return response.data
  },

  async getDevices(): Promise<DeviceSecurity[]> {
    const response =
      await apiClient.get<DeviceSecurity[]>(
        '/security/devices',
      )

    return response.data
  },

  async sendSecurityScan(
    deviceId: string,
  ): Promise<void> {
    await apiClient.post(
      '/device-commands',
      {
        deviceId,
        commandType: 'SECURITY_STATUS',
        payloadJson: '{}',
        expiresInMinutes: 30,
      } satisfies CreateDeviceCommandRequest,
    )
  },

  async sendComplianceCheck(
    deviceId: string,
  ): Promise<void> {
    await apiClient.post(
      '/device-commands',
      {
        deviceId,
        commandType: 'COMPLIANCE_CHECK',
        payloadJson: '{}',
        expiresInMinutes: 30,
      } satisfies CreateDeviceCommandRequest,
    )
  },

  async scanAll(
    devices: DeviceSecurity[],
  ): Promise<void> {
    await Promise.all(
      devices.map((device) =>
        securityApi.sendSecurityScan(
          device.deviceId,
        ),
      ),
    )
  },

  async checkAll(
    devices: DeviceSecurity[],
  ): Promise<void> {
    await Promise.all(
      devices.map((device) =>
        securityApi.sendComplianceCheck(
          device.deviceId,
        ),
      ),
    )
  },
}

export function parseFindings(
  findingsJson: string,
): ComplianceFinding[] {
  if (!findingsJson) {
    return []
  }

  try {
    const parsed: unknown =
      JSON.parse(findingsJson)

    return Array.isArray(parsed)
      ? (parsed as ComplianceFinding[])
      : []
  } catch {
    return []
  }
}