import apiClient from './apiClient'

export interface UserListItem {
  id: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  jobTitle: string | null
  departmentId: string | null
  isActive: boolean
  mfaEnabled: boolean
  lastLoginAtUtc: string | null
  createdAtUtc: string
}

export interface UserRoleSummary {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  isActive: boolean
}

export interface UserDetails
  extends UserListItem {
  updatedAtUtc: string
  roles: UserRoleSummary[]
}

export interface CreateUserRequest {
  firstName: string
  lastName: string
  email: string
  password: string
  jobTitle?: string | null
  departmentId?: string | null
  mfaEnabled: boolean
  roleIds?: string[]
}

export interface UpdateUserRequest {
  firstName: string
  lastName: string
  jobTitle?: string | null
  departmentId?: string | null
  mfaEnabled: boolean
}

export interface AssignUserRolesRequest {
  roleIds: string[]
}

export interface UsersQuery {
  search?: string
  active?: boolean
}

function buildQuery(
  query: UsersQuery,
): string {
  const params =
    new URLSearchParams()

  if (
    query.search?.trim()
  ) {
    params.set(
      'search',
      query.search.trim(),
    )
  }

  if (
    typeof query.active ===
    'boolean'
  ) {
    params.set(
      'active',
      String(query.active),
    )
  }

  const value =
    params.toString()

  return value
    ? `?${value}`
    : ''
}

export const usersApi = {
  async getUsers(
    query: UsersQuery = {},
  ): Promise<UserListItem[]> {
    const response =
      await apiClient.get<UserListItem[]>(
        `/users${buildQuery(query)}`,
      )

    return response.data
  },

  async getUser(
    userId: string,
  ): Promise<UserDetails> {
    const response =
      await apiClient.get<UserDetails>(
        `/users/${userId}`,
      )

    return response.data
  },

  async createUser(
    request: CreateUserRequest,
  ): Promise<UserListItem> {
    const response =
      await apiClient.post<UserListItem>(
        '/users',
        request,
      )

    return response.data
  },

  async updateUser(
    userId: string,
    request: UpdateUserRequest,
  ): Promise<UserListItem> {
    const response =
      await apiClient.put<UserListItem>(
        `/users/${userId}`,
        request,
      )

    return response.data
  },

  async activateUser(
    userId: string,
  ): Promise<void> {
    await apiClient.post(
      `/users/${userId}/activate`,
    )
  },

  async deactivateUser(
    userId: string,
  ): Promise<void> {
    await apiClient.post(
      `/users/${userId}/deactivate`,
    )
  },

  async changePassword(
    userId: string,
    newPassword: string,
  ): Promise<void> {
    await apiClient.post(
      `/users/${userId}/password`,
      {
        newPassword,
      },
    )
  },

  async assignRoles(
    userId: string,
    roleIds: string[],
  ): Promise<void> {
    const request:
      AssignUserRolesRequest = {
        roleIds,
      }

    await apiClient.put(
      `/users/${userId}/roles`,
      request,
    )
  },
}