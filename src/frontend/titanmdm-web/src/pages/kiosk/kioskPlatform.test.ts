import { describe, expect, it } from 'vitest'
import { resolveKioskPlatform } from './kioskPlatform'

describe('resolveKioskPlatform', () => {
  it('uses the explicit workspace query before stored workspace state', () => {
    expect(resolveKioskPlatform('android', 'windows')).toBe('Android')
    expect(resolveKioskPlatform('windows', 'android')).toBe('Windows')
  })

  it('never defaults to Windows in the Android workspace', () => {
    expect(resolveKioskPlatform(null, 'android')).toBe('Android')
    expect(resolveKioskPlatform(null, null)).toBe('Android')
  })

  it('uses Windows when the active workspace is Windows', () => {
    expect(resolveKioskPlatform(null, 'windows')).toBe('Windows')
  })
})
