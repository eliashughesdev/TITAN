import apiClient from './apiClient'

export interface PermissionItem {
  id: string
  code: string
  name: string
  module: string
  description: string | null
}

export interface PermissionModule {
  module: string
  permissions: PermissionItem[]
}

export interface PermissionsResponse {
  total: number
  modules: PermissionModule[]
}

export interface RoleListItem {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
  userCount: number
  permissionCount: number
}

export interface RoleUserSummary {
  id: string
  firstName: string
  lastName: string
  email: string
  isActive: boolean
}

export interface RoleDetails {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
  permissions: PermissionItem[]
  users: RoleUserSummary[]
}

export interface CreateRoleRequest {
  name: string
  description?: string | null
  permissionIds?: string[]
}

export interface UpdateRoleRequest {
  name: string
  description?: string | null
}

export const rolesApi = {
  async getRoles(
    active?: boolean,
  ): Promise<RoleListItem[]> {
    const suffix =
      typeof active === 'boolean'
        ? `?active=${active}`
        : ''

    const response =
      await apiClient.get<RoleListItem[]>(
        `/roles${suffix}`,
      )

    return response.data
  },

  async getRole(
    roleId: string,
  ): Promise<RoleDetails> {
    const response =
      await apiClient.get<RoleDetails>(
        `/roles/${roleId}`,
      )

    return response.data
  },

  async getPermissions():
    Promise<PermissionsResponse> {
    const response =
      await apiClient.get<PermissionsResponse>(
        '/roles/permissions',
      )

    return response.data
  },

  async createRole(
    request: CreateRoleRequest,
  ): Promise<RoleListItem> {
    const response =
      await apiClient.post<RoleListItem>(
        '/roles',
        request,
      )

    return response.data
  },

  async updateRole(
    roleId: string,
    request: UpdateRoleRequest,
  ): Promise<RoleListItem> {
    const response =
      await apiClient.put<RoleListItem>(
        `/roles/${roleId}`,
        request,
      )

    return response.data
  },

  async replacePermissions(
    roleId: string,
    permissionIds: string[],
  ): Promise<void> {
    await apiClient.put(
      `/roles/${roleId}/permissions`,
      {
        permissionIds,
      },
    )
  },

  async activateRole(
    roleId: string,
  ): Promise<void> {
    await apiClient.post(
      `/roles/${roleId}/activate`,
    )
  },

  async deactivateRole(
    roleId: string,
  ): Promise<void> {
    await apiClient.post(
      `/roles/${roleId}/deactivate`,
    )
  },
}