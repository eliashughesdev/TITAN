import apiClient from './apiClient'

export type NotificationSeverity = 'info' | 'warning' | 'critical'

export interface NotificationItem {
  id: string
  category: string
  severity: NotificationSeverity
  title: string
  message: string
  link?: string | null
  occurredAtUtc: string
}

export interface NotificationFeed {
  items: NotificationItem[]
  unavailableSources: string[]
  generatedAtUtc: string
}

export const notificationsApi = {
  feed: () =>
    apiClient.get<NotificationFeed>('/notifications').then(response => response.data),
}
