import apiClient from './apiClient'

export type DeviceCommandStatus =
  | 'Pending'
  | 'Queued'
  | 'Dispatching'
  | 'Sent'
  | 'Delivered'
  | 'Executing'
  | 'Success'
  | 'Failed'
  | 'Timeout'
  | 'Cancelled'

export interface DeviceCommand {
  id: string
  organizationId: string
  deviceId: string
  commandType: string
  payloadJson: string
  status: DeviceCommandStatus
  createdByUserId: string
  createdAtUtc: string
  updatedAtUtc: string
  expiresAtUtc: string
  queuedAtUtc: string | null
  sentAtUtc: string | null
  deliveredAtUtc: string | null
  startedAtUtc: string | null
  completedAtUtc: string | null
  resultJson: string | null
  errorCode: string | null
  errorMessage: string | null
  deliveryAttempts: number
}

export interface DeviceCommandListResult {
  items: DeviceCommand[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateDeviceCommandRequest {
  deviceId: string
  commandType: string
  payloadJson?: string
  expirationMinutes?: number
}

export const deviceCommandsApi = {
  async create(
    request: CreateDeviceCommandRequest,
  ): Promise<DeviceCommand> {
    const response =
      await apiClient.post<DeviceCommand>(
        '/device-commands',
        {
          deviceId: request.deviceId,
          commandType: request.commandType,
          payloadJson:
            request.payloadJson ?? '{}',
          expirationMinutes:
            request.expirationMinutes ?? 30,
        },
      )

    return response.data
  },

  async getForDevice(
    deviceId: string,
  ): Promise<DeviceCommandListResult> {
    const response =
      await apiClient.get<DeviceCommandListResult>(
        '/device-commands',
        {
          params: {
            deviceId,
            page: 1,
            pageSize: 50,
          },
        },
      )

    return response.data
  },

  async getById(
    commandId: string,
  ): Promise<DeviceCommand> {
    const response =
      await apiClient.get<DeviceCommand>(
        `/device-commands/${commandId}`,
      )

    return response.data
  },

  async cancel(
    commandId: string,
  ): Promise<void> {
    await apiClient.post(
      `/device-commands/${commandId}/cancel`,
    )
  },
}