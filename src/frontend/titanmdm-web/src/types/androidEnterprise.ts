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