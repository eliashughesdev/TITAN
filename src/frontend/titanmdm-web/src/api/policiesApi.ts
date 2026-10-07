import apiClient from './apiClient'

export type PolicyPlatform =
  | 'Windows'
  | 'Android'

export type PolicyStatus =
  | 'Draft'
  | 'Active'
  | 'Disabled'
  | 'Archived'

export type AndroidPolicyPublicationStatus =
  | 'Pending'
  | 'Publishing'
  | 'Published'
  | 'Failed'
  | 'Deleted'

export interface Policy {
  id: string
  organizationId: string
  name: string
  description: string | null
  platform: PolicyPlatform
  status: PolicyStatus
  currentVersion: number
  assignedDevices: number
  createdByUserId: string
  createdAtUtc: string
  updatedAtUtc: string
  activatedAtUtc: string | null
  archivedAtUtc: string | null
}

export interface PolicyDetails
  extends Policy {
  configurationJson: string
  appliedDevices: number
  failedDevices: number
}

export interface PolicyAssignment {
  id: string
  policyId: string
  deviceId: string
  deviceName: string
  policyVersion: number
  status: string
  commandId: string | null
  assignedAtUtc: string
  updatedAtUtc: string
  appliedAtUtc: string | null
  errorMessage: string | null
}

export interface CreatePolicyRequest {
  name: string
  description?: string | null
  platform: PolicyPlatform
  configurationJson: string
}

export interface UpdatePolicyRequest {
  name: string
  description?: string | null
  configurationJson: string
}

// ============================================================
// ANDROID ENTERPRISE POLICY PUBLICATION
// ============================================================

export interface AndroidPolicyPublication {
  id: string
  policyId: string
  policyVersionId: string
  policyVersion: number

  googlePolicyId: string
  googlePolicyName: string | null

  status: AndroidPolicyPublicationStatus

  compiledPolicyJson: string | null
  googleResponseJson: string | null

  errorCode: string | null
  errorMessage: string | null

  createdAtUtc: string
  updatedAtUtc: string
  publishedAtUtc: string | null
  lastAttemptAtUtc: string | null
  deletedAtUtc: string | null
}

export interface AndroidPolicyPublishResult {
  policyId: string
  policyVersionId: string
  policyVersion: number

  publicationId: string

  googlePolicyId: string
  googlePolicyName: string

  status: string

  warnings: string[]

  publishedAtUtc: string
}

export interface AndroidPolicyRemoteVerificationResult {
  policyId: string
  policyVersion: number

  googlePolicyId: string
  googlePolicyName: string

  existsInGoogle: boolean

  googlePolicyJson: string
}

export interface AndroidPolicyAssignment {
  assignmentId: string
  policyId: string
  deviceId: string
  androidDeviceId: string

  policyVersion: number

  googleDeviceName: string
  googlePolicyName: string

  assignmentStatus: string

  assignedAtUtc: string
  updatedAtUtc: string
  appliedAtUtc: string | null

  appliedPolicyName: string | null
  appliedPolicyVersion: number | null
  appliedPolicyState: string | null
  lastPolicySyncTimeUtc: string | null

  errorMessage: string | null
}

// ============================================================
// POLICIES API
// ============================================================

export const policiesApi = {
  async getAll(
    platform?: string,
    status?: string,
  ): Promise<Policy[]> {
    const response =
      await apiClient.get<Policy[]>(
        '/policies',
        {
          params: {
            platform:
              platform || undefined,

            status:
              status || undefined,
          },
        },
      )

    return response.data
  },

  async getById(
    policyId: string,
  ): Promise<PolicyDetails> {
    const response =
      await apiClient.get<PolicyDetails>(
        `/policies/${policyId}`,
      )

    return response.data
  },

  async create(
    request: CreatePolicyRequest,
  ): Promise<PolicyDetails> {
    const response =
      await apiClient.post<PolicyDetails>(
        '/policies',
        request,
      )

    return response.data
  },

  async update(
    policyId: string,
    request: UpdatePolicyRequest,
  ): Promise<PolicyDetails> {
    const response =
      await apiClient.put<PolicyDetails>(
        `/policies/${policyId}`,
        request,
      )

    return response.data
  },

  async activate(
    policyId: string,
  ): Promise<void> {
    await apiClient.post(
      `/policies/${policyId}/activate`,
    )
  },

  async disable(
    policyId: string,
  ): Promise<void> {
    await apiClient.post(
      `/policies/${policyId}/disable`,
    )
  },

  async archive(
    policyId: string,
  ): Promise<void> {
    await apiClient.post(
      `/policies/${policyId}/archive`,
    )
  },

  async assign(
    policyId: string,
    deviceIds: string[],
  ): Promise<PolicyAssignment[]> {
    const response =
      await apiClient.post<
        PolicyAssignment[]
      >(
        `/policies/${policyId}/assign`,
        {
          deviceIds,
        },
      )

    return response.data
  },

  async getAssignments(
    policyId: string,
  ): Promise<PolicyAssignment[]> {
    const response =
      await apiClient.get<
        PolicyAssignment[]
      >(
        `/policies/${policyId}/assignments`,
      )

    return response.data
  },

  // ==========================================================
  // ANDROID ENTERPRISE
  // ==========================================================

  async publishAndroid(
    policyId: string,
  ): Promise<AndroidPolicyPublishResult> {
    const response =
      await apiClient.post<
        AndroidPolicyPublishResult
      >(
        `/policies/${policyId}/android/publish`,
      )

    return response.data
  },

  async getAndroidPublication(
    policyId: string,
  ): Promise<AndroidPolicyPublication | null> {
    try {
      const response =
        await apiClient.get<
          AndroidPolicyPublication
        >(
          `/policies/${policyId}/android/publication`,
        )

      return response.data
    } catch (error: unknown) {
      if (
        typeof error === 'object' &&
        error !== null &&
        'response' in error
      ) {
        const status =
          (
            error as {
              response?: {
                status?: number
              }
            }
          ).response?.status

        if (status === 404) {
          return null
        }
      }

      throw error
    }
  },

  async verifyAndroidPublication(
    policyId: string,
  ): Promise<AndroidPolicyRemoteVerificationResult> {
    const response =
      await apiClient.get<
        AndroidPolicyRemoteVerificationResult
      >(
        `/policies/${policyId}/android/verify`,
      )

    return response.data
  },
  async assignAndroid(
  policyId: string,
  deviceId: string,
): Promise<AndroidPolicyAssignment> {
  const response =
    await apiClient.post<AndroidPolicyAssignment>(
      `/policies/${policyId}/android/assign`,
      {
        deviceId,
      },
    )

  return response.data
},

async getAndroidAssignment(
  policyId: string,
  deviceId: string,
): Promise<AndroidPolicyAssignment | null> {
  try {
    const response =
      await apiClient.get<AndroidPolicyAssignment>(
        `/policies/${policyId}/android/assignments/${deviceId}`,
      )

    return response.data
  } catch (error: unknown) {
    if (
      typeof error === 'object' &&
      error !== null &&
      'response' in error
    ) {
      const status =
        (
          error as {
            response?: {
              status?: number
            }
          }
        ).response?.status

      if (status === 404) {
        return null
      }
    }

    throw error
  }
},
}