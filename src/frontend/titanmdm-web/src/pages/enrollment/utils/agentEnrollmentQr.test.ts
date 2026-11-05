import { describe, expect, it } from 'vitest'

import { buildAgentEnrollmentPayload, isLoopbackServer, normalizeServerUrl } from './agentEnrollmentQr'

describe('agentEnrollmentQr', () => {
  it('normaliza la URL del servidor con barra final', () => {
    expect(normalizeServerUrl(' http://192.168.1.20:8020 ')).toBe('http://192.168.1.20:8020/')
    expect(normalizeServerUrl('https://mdm.cisa.local/titan/')).toBe('https://mdm.cisa.local/titan/')
    expect(normalizeServerUrl('ftp://x')).toBeNull()
    expect(normalizeServerUrl('https://u:p@x')).toBeNull()
    expect(normalizeServerUrl('no-url')).toBeNull()
  })

  it('detecta servidores no alcanzables desde el dispositivo', () => {
    expect(isLoopbackServer('http://localhost:3020/')).toBe(true)
    expect(isLoopbackServer('http://127.0.0.1:8020/')).toBe(true)
    expect(isLoopbackServer('http://192.168.1.20:8020/')).toBe(false)
  })

  it('arma el payload que entiende el agente Android', () => {
    const payload = buildAgentEnrollmentPayload('http://192.168.1.20:8020/', 'ABCDEF0123456789')
    expect(payload).toBe('titanmdm://enroll?v=1&server=http%3A%2F%2F192.168.1.20%3A8020%2F&token=ABCDEF0123456789')
  })
})
