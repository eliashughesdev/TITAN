import apiClient from './apiClient'

export interface DeviceGroup {
  id: string
  name: string
  description: string | null
  isDynamic: boolean
  ruleJson: string | null
  isEnabled: boolean
  deviceCount: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface DeviceGroupMember {
  deviceId: string
  deviceName: string
  platform: string
  status: string
  complianceStatus: string
  source: string
  addedAtUtc: string
}

export interface DeviceGroupDetails {
  id: string
  name: string
  description: string | null
  isDynamic: boolean
  ruleJson: string | null
  isEnabled: boolean
  members: DeviceGroupMember[]
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateDeviceGroupRequest {
  name: string
  description?: string | null
  isDynamic: boolean
  ruleJson?: string | null
  deviceIds?: string[] | null
}

export interface UpdateDeviceGroupRequest {
  name: string
  description?: string | null
  isDynamic: boolean
  ruleJson?: string | null
}

export interface GroupCommandRequest {
  commandType: string
  payloadJson?: string | null
  expiresInMinutes?: number
}

export interface GroupCommandResult {
  queuedDevices: number
}

export const deviceGroupsApi = {
  async getAll(): Promise<DeviceGroup[]> {
    const response =
      await apiClient.get<DeviceGroup[]>(
        '/device-groups',
      )

    return response.data
  },

  async getById(
    groupId: string,
  ): Promise<DeviceGroupDetails> {
    const response =
      await apiClient.get<DeviceGroupDetails>(
        `/device-groups/${groupId}`,
      )

    return response.data
  },

  async create(
    request: CreateDeviceGroupRequest,
  ): Promise<DeviceGroupDetails> {
    const response =
      await apiClient.post<DeviceGroupDetails>(
        '/device-groups',
        request,
      )

    return response.data
  },

  async update(
    groupId: string,
    request: UpdateDeviceGroupRequest,
  ): Promise<DeviceGroupDetails> {
    const response =
      await apiClient.put<DeviceGroupDetails>(
        `/device-groups/${groupId}`,
        request,
      )

    return response.data
  },

  async addMembers(
    groupId: string,
    deviceIds: string[],
  ): Promise<void> {
    await apiClient.post(
      `/device-groups/${groupId}/members`,
      {
        deviceIds,
      },
    )
  },

  async removeMember(
    groupId: string,
    deviceId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/device-groups/${groupId}/members/${deviceId}`,
    )
  },

  async executeCommand(
    groupId: string,
    request: GroupCommandRequest,
  ): Promise<GroupCommandResult> {
    const response =
      await apiClient.post<GroupCommandResult>(
        `/device-groups/${groupId}/commands`,
        {
          commandType: request.commandType,
          payloadJson:
            request.payloadJson ?? '{}',
          expiresInMinutes:
            request.expiresInMinutes ?? 60,
        },
      )

    return response.data
  },

  async delete(
    groupId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/device-groups/${groupId}`,
    )
  },
}