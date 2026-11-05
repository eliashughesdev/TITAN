/*
 * P1 RBAC - Lectura de claims del JWT emitido por
 * JwtTokenService (backend). Solo se usa para sincronizar
 * la UI; la autorizacion real siempre la decide el API.
 */

const ROLE_CLAIM_URIS = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
]

export interface TitanJwtClaims {
  permissions: string[]
  roles: string[]
  securityVersion: string | null
  expiresAtMs: number | null
}

type RawClaims = Record<string, unknown>

function decodeBase64Url(segment: string): string {
  const base64 = segment.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const binary = atob(padded)
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))

  return new TextDecoder().decode(bytes)
}

function asStringArray(value: unknown): string[] {
  if (typeof value === 'string') {
    return value.trim() ? [value.trim()] : []
  }

  if (Array.isArray(value)) {
    return value
      .filter((item): item is string => typeof item === 'string')
      .map((item) => item.trim())
      .filter(Boolean)
  }

  return []
}

export function decodeJwtClaims(token: string | null | undefined): TitanJwtClaims | null {
  if (!token) {
    return null
  }

  const parts = token.split('.')

  if (parts.length < 2) {
    return null
  }

  let raw: RawClaims

  try {
    const parsed: unknown = JSON.parse(decodeBase64Url(parts[1]))

    if (!parsed || typeof parsed !== 'object') {
      return null
    }

    raw = parsed as RawClaims
  } catch {
    return null
  }

  const roles = ROLE_CLAIM_URIS.flatMap((claim) => asStringArray(raw[claim]))
  const exp = typeof raw.exp === 'number' ? raw.exp * 1000 : null
  const securityVersion =
    typeof raw.security_version === 'string' || typeof raw.security_version === 'number'
      ? String(raw.security_version)
      : null

  return {
    permissions: Array.from(new Set(asStringArray(raw.permission))).sort(),
    roles: Array.from(new Set(roles)).sort(),
    securityVersion,
    expiresAtMs: exp,
  }
}

/* Comparacion de conjuntos independiente del orden. */
export function sameSet(left: readonly string[], right: readonly string[]): boolean {
  if (left.length !== right.length) {
    return false
  }

  const set = new Set(left)
  return right.every((item) => set.has(item))
}

export const AUTH_EVENTS = {
  sessionExpired: 'titanmdm:session-expired',
  tokenRefreshed: 'titanmdm:token-refreshed',
  permissionsStale: 'titanmdm:permissions-stale',
} as const
