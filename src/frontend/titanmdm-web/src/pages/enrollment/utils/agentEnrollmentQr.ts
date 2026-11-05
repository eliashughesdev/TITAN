/*
 * QR de inscripcion interna del TitanMDM Android Agent (sin Android
 * Enterprise ni DUNS). Debe coincidir con EnrollmentPayloadParser.kt.
 */
export const AGENT_ENROLLMENT_SCHEME = 'titanmdm://enroll'

export function normalizeServerUrl(raw: string): string | null {
  const value = raw.trim()
  if (!value) return null
  try {
    const url = new URL(value)
    if (url.protocol !== 'https:' && url.protocol !== 'http:') return null
    if (url.username || url.password || url.search || url.hash) return null
    return `${url.origin}${url.pathname.replace(/\/+$/, '')}/`
  } catch {
    return null
  }
}

/** localhost / 127.x no son alcanzables desde el handheld. */
export function isLoopbackServer(serverUrl: string): boolean {
  const host = new URL(serverUrl).hostname
  return host === 'localhost' || host === '::1' || host === '[::1]' || host.startsWith('127.')
}

export function buildAgentEnrollmentPayload(serverUrl: string, token: string): string {
  const params = new URLSearchParams({ v: '1', server: serverUrl, token: token.trim() })
  return `${AGENT_ENROLLMENT_SCHEME}?${params.toString()}`
}
