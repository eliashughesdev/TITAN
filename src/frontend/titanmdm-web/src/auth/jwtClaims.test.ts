import { describe, expect, it } from 'vitest'

import { decodeJwtClaims, sameSet } from './jwtClaims'

function token(payload: Record<string, unknown>): string {
  const encode = (value: unknown) => {
    const bytes = new TextEncoder().encode(JSON.stringify(value))
    const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join('')

    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  }

  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`
}

describe('decodeJwtClaims', () => {
  it('lee permisos multiples, roles .NET y expiracion', () => {
    const claims = decodeJwtClaims(
      token({
        permission: ['devices.view', 'tickets.comment', 'devices.view'],
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'SuperAdmin',
        security_version: '3',
        exp: 1_800_000_000,
      }),
    )

    expect(claims?.permissions).toEqual(['devices.view', 'tickets.comment'])
    expect(claims?.roles).toEqual(['SuperAdmin'])
    expect(claims?.securityVersion).toBe('3')
    expect(claims?.expiresAtMs).toBe(1_800_000_000_000)
  })

  it('acepta un unico permiso como string', () => {
    expect(decodeJwtClaims(token({ permission: 'dashboard.view' }))?.permissions).toEqual([
      'dashboard.view',
    ])
  })

  it('decodifica caracteres UTF-8', () => {
    expect(decodeJwtClaims(token({ permission: 'inventario.recepción' }))?.permissions).toEqual([
      'inventario.recepción',
    ])
  })

  it('devuelve null con tokens invalidos', () => {
    expect(decodeJwtClaims(null)).toBeNull()
    expect(decodeJwtClaims('no-es-jwt')).toBeNull()
    expect(decodeJwtClaims('a.###.c')).toBeNull()
  })
})

describe('sameSet', () => {
  it('ignora el orden', () => {
    expect(sameSet(['a', 'b'], ['b', 'a'])).toBe(true)
    expect(sameSet(['a'], ['a', 'b'])).toBe(false)
  })
})
