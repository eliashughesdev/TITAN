import apiClient from './apiClient'

export interface Site {
  id: string
  code: string
  name: string
  description: string | null
  address: string | null
  city: string | null
  province: string | null
  country: string | null
  timeZoneId: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface SiteLocation {
  id: string
  siteId: string
  name: string
  description: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface SiteOperationalSummary {
  siteId: string
  siteCode: string
  siteName: string
  isActive: boolean
  generatedAtUtc: string

  devices: {
    total: number
    online: number
    offline: number
    managed: number
    windows: number
    android: number
    compliant: number
    nonCompliant: number
    quarantined: number
  }

  helpdesk: {
    total: number
    new: number
    open: number
    inProgress: number
    pendingUser: number
    resolved: number
    closed: number
    unassigned: number
    slaBreached: number
  }

  security: {
    evaluatedDevices: number
    averageComplianceScore: number
    criticalRisk: number
    highRisk: number
  }
}

export interface SaveSiteRequest {
  code: string
  name: string
  description: string | null
  address: string | null
  city: string | null
  province: string | null
  country: string | null
  timeZoneId: string | null
}

export interface SaveSiteLocationRequest {
  name: string
  description: string | null
}

export const sitesApi = {
  async getSites(
    includeInactive = true,
  ): Promise<Site[]> {
    const response =
      await apiClient.get<Site[]>(
        '/sites',
        {
          params: {
            includeInactive,
          },
        },
      )

    return response.data
  },

  async getSite(
    siteId: string,
  ): Promise<Site> {
    const response =
      await apiClient.get<Site>(
        `/sites/${siteId}`,
      )

    return response.data
  },

  async createSite(
    request: SaveSiteRequest,
  ): Promise<Site> {
    const response =
      await apiClient.post<Site>(
        '/sites',
        request,
      )

    return response.data
  },

  async updateSite(
    siteId: string,
    request: SaveSiteRequest,
  ): Promise<Site> {
    const response =
      await apiClient.put<Site>(
        `/sites/${siteId}`,
        request,
      )

    return response.data
  },

  async activateSite(
    siteId: string,
  ): Promise<void> {
    await apiClient.post(
      `/sites/${siteId}/activate`,
    )
  },

  async deactivateSite(
    siteId: string,
  ): Promise<void> {
    await apiClient.post(
      `/sites/${siteId}/deactivate`,
    )
  },

  async getLocations(
    siteId: string,
  ): Promise<SiteLocation[]> {
    const response =
      await apiClient.get<SiteLocation[]>(
        `/sites/${siteId}/locations`,
      )

    return response.data
  },

  async createLocation(
    siteId: string,
    request: SaveSiteLocationRequest,
  ): Promise<SiteLocation> {
    const response =
      await apiClient.post<SiteLocation>(
        `/sites/${siteId}/locations`,
        request,
      )

    return response.data
  },

  async updateLocation(
    siteId: string,
    locationId: string,
    request: SaveSiteLocationRequest,
  ): Promise<SiteLocation> {
    const response =
      await apiClient.put<SiteLocation>(
        `/sites/${siteId}/locations/${locationId}`,
        request,
      )

    return response.data
  },

  async activateLocation(
    siteId: string,
    locationId: string,
  ): Promise<void> {
    await apiClient.post(
      `/sites/${siteId}/locations/${locationId}/activate`,
    )
  },

  async deactivateLocation(
    siteId: string,
    locationId: string,
  ): Promise<void> {
    await apiClient.post(
      `/sites/${siteId}/locations/${locationId}/deactivate`,
    )
  },

  async getSummary(
    siteId: string,
  ): Promise<SiteOperationalSummary> {
    const response =
      await apiClient.get<SiteOperationalSummary>(
        `/sites/${siteId}/operations/summary`,
      )

    return response.data
  },
}