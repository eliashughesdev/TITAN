export interface AuditEvent {
  id: string
  eventType: string
  module: string
  action: string
  status: string

  actorUserId: string | null
  actorName: string
  actorEmail: string

  deviceId: string | null
  deviceName: string
  platform: string

  createdAtUtc: string
  completedAtUtc: string | null

  durationMilliseconds:
    number | null

  deliveryAttempts: number

  errorCode: string | null
  errorMessage: string | null

  payloadJson: string | null
  resultJson: string | null
}

export interface AuditListResult {
  items: AuditEvent[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AuditSummary {
  totalEvents: number
  successfulEvents: number
  failedEvents: number
  activeEvents: number
  eventsLast24Hours: number
  uniqueActors: number
  uniqueDevices: number
}

export interface AuditFilterOptions {
  modules: string[]
  actions: string[]
  statuses: string[]
}

export interface AuditQuery {
  search?: string
  module?: string
  action?: string
  status?: string

  userId?: string
  deviceId?: string

  fromUtc?: string
  toUtc?: string

  page?: number
  pageSize?: number
}