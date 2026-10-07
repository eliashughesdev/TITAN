import apiClient
  from './apiClient'

import type {
  ReportsOverview,
} from '../types/reports'

export const reportsApi = {
  async getOverview():
    Promise<ReportsOverview> {
    const response =
      await apiClient
        .get<ReportsOverview>(
          '/reports/overview',
        )

    return response.data
  },

  async exportDevicesCsv():
    Promise<void> {
    const response =
      await apiClient.get(
        '/reports/devices/export/csv',
        {
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
      `titanmdm-devices-${
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