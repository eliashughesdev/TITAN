export interface AuthenticatedUser {
  id: string
  organizationId: string
  firstName: string
  lastName: string
  email: string
  roles: string[]
  permissions: string[]
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  user: AuthenticatedUser
}

export interface RefreshRequest {
  refreshToken: string
}

export interface AuthState {
  user: AuthenticatedUser | null
  isAuthenticated: boolean
  isLoading: boolean
}