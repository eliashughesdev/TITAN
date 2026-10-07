const ACCESS_TOKEN_KEY = 'titanmdm_access_token'
const REFRESH_TOKEN_KEY = 'titanmdm_refresh_token'

export const tokenStorage = {
  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY)
  },

  getRefreshToken(): string | null {
    return sessionStorage.getItem(REFRESH_TOKEN_KEY)
  },

  setTokens(
    accessToken: string,
    refreshToken: string,
  ): void {
    sessionStorage.setItem(
      ACCESS_TOKEN_KEY,
      accessToken,
    )

    sessionStorage.setItem(
      REFRESH_TOKEN_KEY,
      refreshToken,
    )
  },

  setAccessToken(accessToken: string): void {
    sessionStorage.setItem(
      ACCESS_TOKEN_KEY,
      accessToken,
    )
  },

  clear(): void {
    sessionStorage.removeItem(
      ACCESS_TOKEN_KEY,
    )

    sessionStorage.removeItem(
      REFRESH_TOKEN_KEY,
    )
  },
}