export type AndroidEnrollmentMode =
  | 'FullyManaged'
  | 'Dedicated'
  | 'WorkProfile'

export interface AndroidEnterpriseStatus {
  isConfigured: boolean
  canAuthenticate: boolean
  hasPublicCallback: boolean
  googleProjectId: string
  enterpriseName: string | null
  enterpriseDisplayName: string | null
  status: string
  connectedAtUtc: string | null
  lastError: string | null
}

export interface AndroidSignupResponse {
  signupUrl: string
  expiresAtUtc: string
}

export interface AndroidEnrollment {
  id: string
  mode: AndroidEnrollmentMode
  googleEnrollmentTokenName: string
  policyId: string | null
  expiresAtUtc: string
  createdAtUtc: string
  revokedAtUtc: string | null
  isRevoked: boolean
  isExpired: boolean
}

export interface CreatedAndroidEnrollment {
  id: string
  mode: AndroidEnrollmentMode
  googleEnrollmentTokenName: string
  enrollmentToken: string
  qrCode: string
  policyId: string | null
  expiresAtUtc: string
  createdAtUtc: string
}

export interface CreateAndroidEnrollmentRequest {
  mode: AndroidEnrollmentMode
  expirationMinutes: number
  policyId?: string | null
}

export interface AndroidDeviceSyncResult {
  receivedFromGoogle: number
  created: number
  updated: number
  markedMissing: number
  failed: number
  startedAtUtc: string
  completedAtUtc: string
  errors: string[]
}

export interface AndroidDeviceInventorySummary {
  total: number
  managed: number
  missingInGoogle: number
  fullyManaged: number
  dedicated: number
  workProfile: number
  compliant: number
  nonCompliant: number
  lastSynchronizationUtc: string | null
}

export type AndroidRemoteAction =
  | 'lock'
  | 'reboot'
  | 'reset-password'
  | 'start-lost-mode'
  | 'stop-lost-mode'
  | 'wipe'
  | 'delete'

export interface AndroidRemoteActionRequest {
  action: AndroidRemoteAction
  newPassword?: string
  lostModeMessage?: string
  lostModePhoneNumber?: string
}

export interface AndroidRemoteActionResponse {
  deviceId: string
  action: string
  status: 'Issued' | 'DeletionRequested'
  googleCommandName: string | null
  correlationId: string | null
  issuedAtUtc: string
}

export interface AndroidInventoryItem {
  deviceId: string
  deviceName: string
  serialNumber: string
  manufacturer: string | null
  model: string | null
  status: string
  complianceStatus: string
  isManaged: boolean
  lastSeenAtUtc: string | null
  managementMode: string | null
  ownership: string | null
  state: string | null
  appliedPolicyName: string | null
  appliedPolicyVersion: number | null
  appliedPolicyState: string | null
  lastPolicySyncTimeUtc: string | null
  lastSynchronizedAtUtc: string
  isDeletedInGoogle: boolean
  apiLevel: number | null
  androidVersion: string | null
  securityPatchLevel: string | null
}

export type AndroidCapabilityStatus =
  | 'Available'
  | 'NotSupported'
  | 'PendingValidation'

export interface AndroidCapability {
  action: string
  status: AndroidCapabilityStatus
  reason: string | null
}

export interface AndroidDeviceCapabilities {
  deviceId: string
  deviceName: string
  manufacturer: string | null
  model: string | null
  apiLevel: number | null
  androidVersion: string | null
  securityPatchLevel: string | null
  managementMode: string | null
  ownership: string | null
  isManaged: boolean
  isDeletedInGoogle: boolean
  hasGoogleMobileServices: boolean
  requiresAlternativeRoute: boolean
  alternativeRouteNote: string | null
  remoteDiagnosticsSupported: boolean
  remoteVisionSupported: boolean
  remoteControlSupported: boolean
  remoteSupportNote: string
  actions: AndroidCapability[]
}

export interface AndroidInventoryPage {
  items: AndroidInventoryItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AndroidInventoryQuery {
  page?: number
  pageSize?: number
  search?: string
}

export interface AndroidAuditEvent {
  id: string
  action: string
  targetType: string
  targetId: string | null
  result: string
  actorName: string
  actorEmail: string | null
  correlationId: string | null
  ipAddress: string | null
  createdAtUtc: string
}

export interface AndroidAuditPage {
  items: AndroidAuditEvent[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AndroidAuditQuery {
  page?: number
  pageSize?: number
  search?: string
}
