export type EnrollmentPlatform =
  | 'Android'
  | 'Windows'

export type EnrollmentStatus =
  | 'Active'
  | 'Completed'
  | 'Expired'
  | 'Revoked'

export type WindowsInstallerDeploymentMode =
  | 'individual'
  | 'gpo'

export interface EnrollmentToken {
  id: string
  platform: EnrollmentPlatform
  status: EnrollmentStatus
  maxUses: number
  usedCount: number
  expiresAtUtc: string
  createdByUserId: string
  createdAtUtc: string
  updatedAtUtc: string
  lastUsedAtUtc: string | null
  revokedAtUtc: string | null
}

export interface CreatedEnrollmentToken {
  id: string
  token: string
  platform: EnrollmentPlatform
  status: EnrollmentStatus
  maxUses: number
  usedCount: number
  expiresAtUtc: string
  createdAtUtc: string
}

export interface CreateEnrollmentTokenRequest {
  platform: EnrollmentPlatform
  expirationMinutes: number
  maxUses: number
}

export interface EnrollmentTokenValidationResult {
  isValid: boolean
  enrollmentTokenId: string | null
  organizationId: string | null
  platform: string | null
  errorCode: string | null
  message: string | null
}

export interface CreateWindowsInstallerRequest {
  deploymentMode:
    WindowsInstallerDeploymentMode

  expirationMinutes:
    number

  maxUses:
    number
}

export interface WindowsAgentPackageInfo {
  fileName: string
  sizeBytes: number
  sha256: string
  lastModifiedUtc: string
}