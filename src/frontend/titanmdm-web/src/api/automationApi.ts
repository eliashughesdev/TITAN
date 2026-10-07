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
}