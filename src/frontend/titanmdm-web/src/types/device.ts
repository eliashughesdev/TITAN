export type DevicePlatform =
  | 'Android'
  | 'Windows'
  | 'Unknown'

export type DeviceStatus =
  | 'Pending'
  | 'Enrolling'
  | 'Online'
  | 'Offline'
  | 'Locked'
  | 'Quarantined'
  | 'Retired'
  | 'Wiped'

export type ComplianceStatus =
  | 'Unknown'
  | 'Evaluating'
  | 'Compliant'
  | 'NonCompliant'
  | 'Quarantined'

export interface DeviceListItem {
  id: string
  deviceName: string
  platform: DevicePlatform
  status: DeviceStatus
  complianceStatus: ComplianceStatus
  serialNumber: string
  manufacturer: string | null
  model: string | null
  operatingSystem: string | null
  operatingSystemVersion: string | null
  assignedUser: string | null
  department: string | null
  ipAddress: string | null
  batteryLevel: number | null
  isManaged: boolean
  enrolledAtUtc: string | null
  lastSeenAtUtc: string | null
}

export interface DeviceDetails {
  id: string
  organizationId: string
  deviceName: string
  platform: DevicePlatform
  status: DeviceStatus
  complianceStatus: ComplianceStatus
  serialNumber: string
  imei: string | null
  manufacturer: string | null
  model: string | null
  operatingSystem: string | null
  operatingSystemVersion: string | null
  agentVersion: string | null
  ipAddress: string | null
  macAddress: string | null
  assignedUser: string | null
  department: string | null
  batteryLevel: number | null
  isManaged: boolean
  enrolledAtUtc: string | null
  lastSeenAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface DeviceListResult {
  items: DeviceListItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AndroidDeviceDetails {
  androidDeviceId: string
  deviceId: string

  googleDeviceName: string
  googleDeviceId: string | null

  managementMode: string | null
  ownership: string | null
  state: string | null

  appliedPolicyName: string | null
  appliedPolicyVersion: number | null
  appliedPolicyState: string | null

  enrollmentTokenName: string | null
  userName: string | null

  brand: string | null
  hardware: string | null
  deviceBasebandVersion: string | null
  bootloaderVersion: string | null

  securityPatchLevel: string | null
  apiLevel: number | null
  buildNumber: string | null
  kernelVersion: string | null

  androidDevicePolicyVersion: string | null
  androidDevicePolicyVersionCode: string | null

  encryptionStatus: string | null
  securityPosture: string | null

  enrollmentTimeUtc: string | null
  lastStatusReportTimeUtc: string | null
  lastPolicySyncTimeUtc: string | null
  lastSynchronizedAtUtc: string

  isDeletedInGoogle: boolean
  deletedInGoogleAtUtc: string | null
}

export interface DeviceGroupMembership {
  id: string
  name: string
  isDynamic: boolean
  source: string
}

export interface DeviceSecuritySummary {
  complianceScore: number
  riskLevel: string
  complianceStatus: string
  agentInstalled: boolean
  agentVersionName: string
  lastSecurityScanAtUtc: string | null
  lastComplianceCheckAtUtc: string | null
}

export interface DeviceCommandSnapshot {
  id: string
  commandType: string
  status: string
  createdAtUtc: string
  completedAtUtc: string | null
  resultJson: string | null
  errorCode: string | null
  errorMessage: string | null
}

export interface DeviceOperationalSnapshot {
  device: DeviceDetails
  groups: DeviceGroupMembership[]
  security: DeviceSecuritySummary | null

  latestResults: Record<
    string,
    DeviceCommandSnapshot
  >

  lastInventoryAtUtc: string | null
}