import apiClient from './apiClient'

/*
 * ================================================================
 * WORKSPACE
 * ================================================================
 */

export type DashboardWorkspace =
  | 'global'
  | 'windows'
  | 'android'

/*
 * ================================================================
 * DEVICES
 * ================================================================
 */

export interface DeviceSummary {
  total: number

  online: number

  offline: number

  pending: number

  enrolling: number

  quarantined: number

  retired: number

  managed: number
}

/*
 * ================================================================
 * PLATFORMS
 * ================================================================
 */

export interface PlatformSummary {
  windows: number

  android: number

  unknown: number
}

/*
 * ================================================================
 * COMPLIANCE
 * ================================================================
 */

export interface ComplianceSummary {
  compliant: number

  nonCompliant: number

  evaluating: number

  quarantined: number

  unknown: number

  compliancePercentage:
    number | null
}

/*
 * ================================================================
 * COMMANDS
 * ================================================================
 */

export interface CommandSummary {
  total: number

  pending: number

  queued: number

  dispatching: number

  sent: number

  delivered: number

  executing: number

  success: number

  failed: number

  timeout: number

  cancelled: number

  active: number

  problems: number
}

/*
 * ================================================================
 * SYSTEM
 * ================================================================
 */

export interface SystemStatus {
  api: string

  database: string
}

/*
 * ================================================================
 * WINDOWS ADVANCED
 * ================================================================
 */

export interface WindowsDashboardSummary {
  devices: number

  online: number

  offline: number

  managed: number

  checkInsLast24Hours: number

  updateStatusChecks: number

  updateScanRequests: number

  pendingReboot: number

  updateServiceRunning: number

  updateTelemetryDevices: number

  securityTelemetryDevices: number

  defenderAvailable: number

  firewallAvailable: number

  bitLockerAvailable: number

  tpmAvailable: number

  secureBootEnabled: number

  remoteSessionsTotal: number

  remoteSessionsActive: number

  remoteSessionsCompleted: number

  remoteSessionsFailed: number

  remoteSessionsLast24Hours: number
}

/*
 * ================================================================
 * ANDROID ADVANCED
 * ================================================================
 */

export interface AndroidDashboardSummary {
  devices: number

  managed: number

  missingInGoogle: number

  fullyManaged: number

  dedicated: number

  workProfile: number

  activeEnrollments: number

  expiredEnrollments: number

  revokedEnrollments: number

  policyApplied: number

  policyPendingOrUnknown: number

  applicationsPresent: number

  securityTelemetryDevices: number

  rootDetected: number

  adbEnabled: number

  deviceSecure: number

  encrypted: number

  securityPostureReported: number

  lastSynchronizationUtc:
    string | null
}

/*
 * ================================================================
 * DASHBOARD SUMMARY
 * ================================================================
 *
 * windows/android permanecen opcionales en el contrato frontend.
 *
 * Motivo:
 *
 * - initialSummary puede existir antes de recibir datos.
 * - global puede incluir ambos.
 * - Windows puede trabajar solamente con windows.
 * - Android puede trabajar solamente con android.
 * - mantiene compatibilidad durante refresh y cambios de workspace.
 * ================================================================
 */

export interface DashboardSummary {
  devices:
    DeviceSummary

  platforms:
    PlatformSummary

  compliance:
    ComplianceSummary

  commands:
    CommandSummary

  system:
    SystemStatus

  windows?:
    WindowsDashboardSummary
    | null

  android?:
    AndroidDashboardSummary
    | null

  generatedAtUtc:
    string
}

/*
 * ================================================================
 * REQUEST DEDUPLICATION
 * ================================================================
 */

const activeRequests =
  new Map<
    DashboardWorkspace,
    Promise<DashboardSummary>
  >()

/*
 * ================================================================
 * API
 * ================================================================
 */

export const dashboardApi = {
  async getSummary(
    workspace:
      DashboardWorkspace = 'global',
  ): Promise<DashboardSummary> {
    const existingRequest =
      activeRequests.get(
        workspace,
      )

    if (
      existingRequest
    ) {
      return existingRequest
    }

    const request =
      apiClient
        .get<DashboardSummary>(
          '/dashboard/summary',
          {
            params: {
              workspace,
            },
          },
        )
        .then(
          response =>
            response.data,
        )
        .finally(
          () => {
            activeRequests.delete(
              workspace,
            )
          },
        )

    activeRequests.set(
      workspace,
      request,
    )

    return request
  },
}