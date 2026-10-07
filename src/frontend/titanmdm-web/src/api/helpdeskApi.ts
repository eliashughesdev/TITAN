import apiClient from './apiClient'

export interface HelpdeskTicketListItem {
  id: string
  number: string
  subject: string
  status: string
  priority: string
  type: string
  category: string
  source: string
  requesterUserId: string
  requesterName: string
  assigneeUserId?: string | null
  assigneeName?: string | null
  deviceId?: string | null
  deviceName?: string | null
  devicePlatform?: string | null
  createdAtUtc: string
  updatedAtUtc: string
  firstResponseDueAtUtc?: string | null
  resolveDueAtUtc?: string | null
  slaBreached: boolean
}

export interface EntraLoginStatus {
  enabled: boolean
  tenantConfigured: boolean
  clientConfigured: boolean
  secretConfigured: boolean
  frontendUrl?: string | null
  callbackUrl?: string | null
  frontendReturnUrl?: string | null
}

export interface HelpdeskComment {
  id: string
  authorUserId: string
  authorName: string
  body: string
  isInternal: boolean
  createdAtUtc: string
}

export interface HelpdeskEvent {
  id: string
  eventType: string
  summary: string
  createdAtUtc: string
}

export interface HelpdeskTicketDetails
  extends HelpdeskTicketListItem {
  description: string

  remoteSessionId?: string | null

  entraUserPrincipalName?: string | null

  comments: HelpdeskComment[]

  timeline: HelpdeskEvent[]
}

export interface HelpdeskTicketListResult {
  items: HelpdeskTicketListItem[]
  total: number
  page: number
  pageSize: number
}

export interface EntraIdSettings {
  isEnabled: boolean
  tenantId?: string | null
  clientId?: string | null
  hasClientSecret: boolean
  allowedGroupIds?: string | null
  syncRequestersOnly: boolean
  lastSyncAtUtc?: string | null
  lastSyncStatus?: string | null
}

export interface EntraDirectoryUser {
  id: string
  entraObjectId: string
  displayName: string
  userPrincipalName: string
  mail?: string | null
  jobTitle?: string | null
  department?: string | null
  linkedTitanUserId?: string | null
  isActive: boolean
}

export const helpdeskApi = {
  async getTickets(
    params: {
      search?: string
      status?: string
      priority?: string
      deviceId?: string
      page?: number
      pageSize?: number
    } = {},
  ) {
    const response =
      await apiClient.get<HelpdeskTicketListResult>(
        '/helpdesk/tickets',
        {
          params,
        },
      )

    return response.data
  },
  async getEntraLoginStatus() {
  const response =
    await apiClient.get<EntraLoginStatus>(
      '/auth/entra/status',
    )

  return response.data
},

  async getTicket(
    id: string,
  ) {
    const response =
      await apiClient.get<HelpdeskTicketDetails>(
        `/helpdesk/tickets/${id}`,
      )

    return response.data
  },

  async createTicket(
    payload: {
      subject: string
      description: string
      type?: string
      priority?: string
      category?: string
      source?: string
      deviceId?: string
      entraObjectId?: string
    },
  ) {
    const response =
      await apiClient.post<HelpdeskTicketDetails>(
        '/helpdesk/tickets',
        payload,
      )

    return response.data
  },

  async addComment(
    id: string,
    body: string,
    isInternal: boolean,
  ) {
    const response =
      await apiClient.post<HelpdeskTicketDetails>(
        `/helpdesk/tickets/${id}/comments`,
        {
          body,
          isInternal,
        },
      )

    return response.data
  },

  async assign(
    id: string,
    assigneeUserId: string,
  ) {
    const response =
      await apiClient.post<HelpdeskTicketDetails>(
        `/helpdesk/tickets/${id}/assign`,
        {
          assigneeUserId,
        },
      )

    return response.data
  },

  async transition(
    id: string,
    status: string,
  ) {
    const response =
      await apiClient.post<HelpdeskTicketDetails>(
        `/helpdesk/tickets/${id}/transition`,
        {
          status,
        },
      )

    return response.data
  },

  async reopen(
    id: string,
    reason: string,
  ) {
    const response =
      await apiClient.post<HelpdeskTicketDetails>(
        `/helpdesk/tickets/${id}/reopen`,
        {
          reason,
        },
      )

    return response.data
  },

  async getEntraSettings() {
    const response =
      await apiClient.get<EntraIdSettings>(
        '/helpdesk/entra/settings',
      )

    return response.data
  },

  async saveEntraSettings(
    payload: {
      isEnabled: boolean
      tenantId: string
      clientId: string
      clientSecret?: string
      allowedGroupIds?: string
      syncRequestersOnly: boolean
    },
  ) {
    const response =
      await apiClient.put<EntraIdSettings>(
        '/helpdesk/entra/settings',
        payload,
      )

    return response.data
  },

  async syncEntra() {
    const response =
      await apiClient.post(
        '/helpdesk/entra/sync',
      )

    return response.data
  },

  async searchEntraUsers(
    search?: string,
  ) {
    const response =
      await apiClient.get<
        EntraDirectoryUser[]
      >(
        '/helpdesk/entra/users',
        {
          params: {
            search,
          },
        },
      )

    return response.data
  },
}