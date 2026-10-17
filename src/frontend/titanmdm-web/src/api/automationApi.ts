import apiClient from './apiClient'

export type AutomationTriggerType =
  | 'Manual'
  | 'DeviceEnrolled'
  | 'DeviceOnline'
  | 'DeviceOffline'
  | 'DeviceNonCompliant'
  | 'SecurityRisk'
  | 'GeofenceEnter'
  | 'GeofenceExit'
  | 'LowBattery'
  | 'LostModeActivated'
  | 'Schedule'
  | 'DeviceSiteResolved'
  | 'WindowsUpdateScan'
  | 'WindowsUpdateInstallFailed'
  | 'WindowsInventoryChanged'
  | 'AndroidEnrolled'
  | 'AndroidDisconnected'
  | 'AndroidNonCompliant'
  | 'AndroidRequiredAppPending'
  | 'AndroidPolicyError'
  | 'AndroidInstallError'
  | 'TicketCreated'
  | 'TicketAssigned'
  | 'TicketUnassigned'
  | 'TicketPriorityChanged'
  | 'TicketStatusChanged'
  | 'TicketSlaNearDue'
  | 'TicketSlaBreached'
  | 'TicketNoResponse'
  | 'TicketReopened'
  | 'PonchesSyncFailure'
  | 'PonchesClockOffline'
  | 'PonchesAnomaly'
  | 'PonchesPendingOperation'

export type AutomationActionType =
  | 'SendCommand'
  | 'RequestLocation'
  | 'SecurityScan'
  | 'ComplianceScan'
  | 'AppInventory'
  | 'EnableLostMode'
  | 'DisableLostMode'
  | 'Notification'
  | 'WindowsInventory'
  | 'WindowsUpdateScan'
  | 'WindowsUpdateInstall'
  | 'RestartDevice'
  | 'LockDevice'

export interface AutomationRule {
  id: string
  name: string
  description: string | null
  triggerType: AutomationTriggerType
  conditionsJson: string
  actionType: AutomationActionType
  actionPayloadJson: string
  isEnabled: boolean
  executionCount: number
  createdAtUtc: string
  updatedAtUtc: string
  lastExecutedAtUtc: string | null
}

export interface AutomationExecution {
  id: string
  automationRuleId: string
  automationName: string
  deviceId: string | null
  deviceName: string | null
  triggerType: string
  status: 'Running' | 'Success' | 'Failed' | 'Skipped'
  resultJson: string | null
  errorMessage: string | null
  startedAtUtc: string
  completedAtUtc: string | null
}

export interface CreateAutomationRuleRequest {
  name: string
  description?: string | null
  triggerType: AutomationTriggerType
  conditionsJson?: string
  actionType: AutomationActionType
  actionPayloadJson?: string
}

export interface UpdateAutomationRuleRequest
  extends CreateAutomationRuleRequest {
  isEnabled: boolean
}

export interface ExecuteAutomationRequest {
  deviceId?: string | null
  triggerPayloadJson?: string
}

export interface AutomationConditionDto {
  field: string
  operator: string
  value?: string | null
  values?: string[] | null
}

export interface AutomationConditionGroupDto {
  operator: 'All' | 'Any'
  conditions: AutomationConditionDto[]
  groups: AutomationConditionGroupDto[]
}

export interface AutomationActionDto {
  code: string
  order: number
  parameters: Record<string, string | null>
  continueOnError: boolean
}

export interface AutomationPoliciesDto {
  errorPolicy: string
  disableAfterFailures: number
  maxRetries: number
  retryBackoffSeconds: number
  timeoutSeconds: number
  concurrencyPolicy: string
  minSecondsBetweenExecutions: number
}

export interface AutomationScheduleDefinitionDto {
  recurrence: string
  intervalMinutes?: number | null
  timeOfDay?: string | null
  dayOfWeek?: number | null
  dayOfMonth?: number | null
  timeZoneId: string
  startAtUtc?: string | null
  catchUp: boolean
}

export interface AutomationRuleDefinition {
  version: number
  module: string
  trigger: string
  priority: number
  conditions: AutomationConditionGroupDto
  actions: AutomationActionDto[]
  policies: AutomationPoliciesDto
  schedule?: AutomationScheduleDefinitionDto | null
  scopeSiteIds: string[]
}

export interface AutomationEventDescriptor {
  code: string
  module: string
  label: string
  description: string
  connected: boolean
  payloadFields: string[]
}

export interface AutomationActionParameterDescriptor {
  name: string
  label: string
  required: boolean
  defaultValue: string | null
}

export interface AutomationActionDescriptor {
  code: string
  module: string
  label: string
  description: string
  impact: string
  requiresDevice: boolean
  enabled: boolean
  requiredPermission: string | null
  parameters: AutomationActionParameterDescriptor[]
}

export interface AutomationIntegration {
  module: string
  label: string
  status: 'connected' | 'prepared'
  connected: boolean
  eventCodes: string[]
  owner: string
  connectionPoint: string
  notes: string
}

export interface AutomationToolContract {
  toolCode: string
  owner: string
  requiredPermission: string
  writeEnabled: boolean
  requiresApproval: boolean
  notes: string
}

export interface AutomationValidationIssue {
  severity: string
  path: string
  message: string
}

export interface AutomationDefinitionValidation {
  isValid: boolean
  issues: AutomationValidationIssue[]
}

export interface AutomationConditionTrace {
  path: string
  field: string
  operator: string
  expected: string | null
  actual: string | null
  passed: boolean
}

export interface AutomationPlannedAction {
  code: string
  order: number
  valid: boolean
  message: string | null
}

export interface AutomationSimulationResult {
  ruleId: string
  matched: boolean
  simulated: boolean
  conditionTraces: AutomationConditionTrace[]
  plannedActions: AutomationPlannedAction[]
  warnings: string[]
}

export interface AutomationAlert {
  severity: string
  message: string
  ruleId: string | null
  createdAtUtc: string
}

export interface AutomationDashboard {
  totalRules: number
  activeRules: number
  executionsLast24Hours: number
  failuresLast24Hours: number
  queuePending: number
  queueDeadLetter: number
  successRate: number
  averageDurationMs: number
  recentExecutions: AutomationExecution[]
  alerts: AutomationAlert[]
}

export interface AutomationActionRun {
  code: string
  order: number
  status: string
  message: string | null
  resultJson: string | null
}

export interface AutomationExecutionDetail {
  id: string
  automationRuleId: string
  automationName: string
  deviceId: string | null
  deviceName: string | null
  triggerType: string
  status: string
  simulated: boolean
  retryCount: number
  cycleDepth: number
  durationMs: number | null
  resultJson: string | null
  errorMessage: string | null
  actions: AutomationActionRun[]
  startedAtUtc: string
  completedAtUtc: string | null
}

export interface AutomationNotification {
  id: string
  automationRuleId: string | null
  automationName: string | null
  deviceId: string | null
  title: string
  message: string
  severity: string
  channel: string
  isRead: boolean
  createdAtUtc: string
  readAtUtc: string | null
}

export interface AutomationSchedule {
  id: string
  automationRuleId: string
  recurrence: string
  intervalMinutes: number | null
  timeOfDay: string | null
  dayOfWeek: number | null
  dayOfMonth: number | null
  timeZoneId: string
  startAtUtc: string | null
  nextRunAtUtc: string | null
  lastRunAtUtc: string | null
  catchUp: boolean
  isEnabled: boolean
}

export interface AutomationScheduleRequest {
  recurrence: string
  intervalMinutes?: number | null
  timeOfDay?: string | null
  dayOfWeek?: number | null
  dayOfMonth?: number | null
  timeZoneId?: string | null
  startAtUtc?: string | null
  catchUp: boolean
  isEnabled: boolean
}

export interface AutomationQueueStats {
  pending: number
  retried: number
  running: number
  deadLetter: number
}

export interface EnterpriseRule {
  id: string
  name: string
  description: string | null
  isEnabled: boolean
  module: string
  priority: number
  consecutiveFailures: number
  executionCount: number
  updatedAtUtc: string
  lastExecutedAtUtc: string | null
  definition: AutomationRuleDefinition
}

export interface EnterpriseCreateRuleRequest {
  name: string
  description?: string | null
  isEnabled: boolean
  definition: AutomationRuleDefinition
}

export type EnterpriseUpdateRuleRequest =
  EnterpriseCreateRuleRequest

export interface SimulateRuleRequest {
  deviceId?: string | null
  triggerPayloadJson?: string
}

export const automationApi = {
  async getRules(): Promise<AutomationRule[]> {
    const response =
      await apiClient.get<AutomationRule[]>(
        '/automation/rules',
      )

    return response.data
  },

  async getRule(
    ruleId: string,
  ): Promise<AutomationRule> {
    const response =
      await apiClient.get<AutomationRule>(
        `/automation/rules/${ruleId}`,
      )

    return response.data
  },

  async create(
    request: CreateAutomationRuleRequest,
  ): Promise<AutomationRule> {
    const response =
      await apiClient.post<AutomationRule>(
        '/automation/rules',
        request,
      )

    return response.data
  },

  async update(
    ruleId: string,
    request: UpdateAutomationRuleRequest,
  ): Promise<AutomationRule> {
    const response =
      await apiClient.put<AutomationRule>(
        `/automation/rules/${ruleId}`,
        request,
      )

    return response.data
  },

  async setEnabled(
    ruleId: string,
    enabled: boolean,
  ): Promise<void> {
    await apiClient.post(
      `/automation/rules/${ruleId}/${
        enabled ? 'enable' : 'disable'
      }`,
    )
  },

  async execute(
    ruleId: string,
    request: ExecuteAutomationRequest,
  ): Promise<void> {
    await apiClient.post(
      `/automation/rules/${ruleId}/execute`,
      request,
    )
  },

  async delete(
    ruleId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/automation/rules/${ruleId}`,
    )
  },

  async getExecutions(
    limit = 100,
  ): Promise<AutomationExecution[]> {
    const response =
      await apiClient.get<
        AutomationExecution[]
      >(
        '/automation/executions',
        {
          params: {
            limit,
          },
        },
      )

    return response.data
  },

  async getEvents(): Promise<
    AutomationEventDescriptor[]
  > {
    const response =
      await apiClient.get<
        AutomationEventDescriptor[]
      >('/automation/platform/events')

    return response.data
  },

  async getActions(): Promise<
    AutomationActionDescriptor[]
  > {
    const response =
      await apiClient.get<
        AutomationActionDescriptor[]
      >('/automation/platform/actions')

    return response.data
  },

  async getIntegrations(): Promise<
    AutomationIntegration[]
  > {
    const response =
      await apiClient.get<AutomationIntegration[]>(
        '/automation/platform/integrations',
      )

    return response.data
  },

  async getToolContracts(): Promise<
    AutomationToolContract[]
  > {
    const response =
      await apiClient.get<AutomationToolContract[]>(
        '/automation/platform/tool-contracts',
      )

    return response.data
  },

  async validateDefinition(
    definition: AutomationRuleDefinition,
  ): Promise<AutomationDefinitionValidation> {
    const response =
      await apiClient.post<
        AutomationDefinitionValidation
      >(
        '/automation/platform/definitions/validate',
        definition,
      )

    return response.data
  },

  async getEnterpriseRules(): Promise<
    EnterpriseRule[]
  > {
    const response =
      await apiClient.get<EnterpriseRule[]>(
        '/automation/platform/rules',
      )

    return response.data
  },

  async createEnterpriseRule(
    request: EnterpriseCreateRuleRequest,
  ): Promise<EnterpriseRule> {
    const response =
      await apiClient.post<EnterpriseRule>(
        '/automation/platform/rules',
        request,
      )

    return response.data
  },

  async updateEnterpriseRule(
    ruleId: string,
    request: EnterpriseUpdateRuleRequest,
  ): Promise<EnterpriseRule> {
    const response =
      await apiClient.put<EnterpriseRule>(
        `/automation/platform/rules/${ruleId}`,
        request,
      )

    return response.data
  },

  async simulate(
    ruleId: string,
    request: SimulateRuleRequest,
  ): Promise<AutomationSimulationResult> {
    const response =
      await apiClient.post<
        AutomationSimulationResult
      >(
        `/automation/platform/rules/${ruleId}/simulate`,
        request,
      )

    return response.data
  },

  async getDashboard(): Promise<AutomationDashboard> {
    const response =
      await apiClient.get<AutomationDashboard>(
        '/automation/platform/dashboard',
      )

    return response.data
  },

  async getQueueStats(): Promise<AutomationQueueStats> {
    const response =
      await apiClient.get<AutomationQueueStats>(
        '/automation/platform/queue',
      )

    return response.data
  },

  async getExecutionDetail(
    executionId: string,
  ): Promise<AutomationExecutionDetail> {
    const response =
      await apiClient.get<
        AutomationExecutionDetail
      >(
        `/automation/platform/executions/${executionId}`,
      )

    return response.data
  },

  async retryExecution(
    executionId: string,
  ): Promise<void> {
    await apiClient.post(
      `/automation/platform/executions/${executionId}/retry`,
    )
  },

  async getNotifications(
    limit = 50,
  ): Promise<AutomationNotification[]> {
    const response =
      await apiClient.get<
        AutomationNotification[]
      >(
        '/automation/platform/notifications',
        {
          params: {
            limit,
          },
        },
      )

    return response.data
  },

  async markNotificationRead(
    notificationId: string,
  ): Promise<void> {
    await apiClient.post(
      `/automation/platform/notifications/${notificationId}/read`,
    )
  },

  async getSchedule(
    ruleId: string,
  ): Promise<AutomationSchedule> {
    const response =
      await apiClient.get<AutomationSchedule>(
        `/automation/platform/rules/${ruleId}/schedule`,
      )

    return response.data
  },

  async saveSchedule(
    ruleId: string,
    request: AutomationScheduleRequest,
  ): Promise<AutomationSchedule> {
    const response =
      await apiClient.put<AutomationSchedule>(
        `/automation/platform/rules/${ruleId}/schedule`,
        request,
      )

    return response.data
  },

  async deleteSchedule(
    ruleId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/automation/platform/rules/${ruleId}/schedule`,
    )
  },
}
