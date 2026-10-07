import apiClient
  from './apiClient'

import type {
  AuditFilterOptions,
  AuditListResult,
  AuditQuery,
  AuditSummary,
} from '../types/audit'

function params(
  query:
    AuditQuery = {},
) {
  return {
    search:
      query.search
      ||
      undefined,

    module:
      query.module
      ||
      undefined,

    action:
      query.action
      ||
      undefined,

    status:
      query.status
      ||
      undefined,

    userId:
      query.userId
      ||
      undefined,

    deviceId:
      query.deviceId
      ||
      undefined,

    fromUtc:
      query.fromUtc
      ||
      undefined,

    toUtc:
      query.toUtc
      ||
      undefined,

    page:
      query.page
      ??
      1,

    pageSize:
      query.pageSize
      ??
      50,
  }
}

export const auditApi = {
  async getEvents(
    query:
      AuditQuery = {},
  ): Promise<AuditListResult> {
    const response =
      await apiClient
        .get<AuditListResult>(
          '/audit',
          {
            params:
              params(query),
          },
        )

    return response.data
  },

  async getSummary():
    Promise<AuditSummary> {
    const response =
      await apiClient
        .get<AuditSummary>(
          '/audit/summary',
        )

    return response.data
  },

  async getFilters():
    Promise<AuditFilterOptions> {
    const response =
      await apiClient
        .get<AuditFilterOptions>(
          '/audit/filters',
        )

    return response.data
  },

  async exportCsv(
    query:
      AuditQuery = {},
  ): Promise<void> {
    const response =
      await apiClient.get(
        '/audit/export/csv',
        {
          params:
            params(query),

          responseType:
            'blob',
        },
      )

    const url =
      window.URL
        .createObjectURL(
          response.data,
        )

    const anchor =
      document
        .createElement('a')

    anchor.href =
      url

    anchor.download =
      `titanmdm-audit-${
        new Date()
          .toISOString()
          .slice(0, 10)
      }.csv`

    document.body
      .appendChild(anchor)

    anchor.click()
    anchor.remove()

    window.URL
      .revokeObjectURL(url)
  },
}