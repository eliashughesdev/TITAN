import { describe, expect, it } from 'vitest'

import { DEVICE_ALIAS_MAX_LENGTH, deviceDisplayName, normalizeDeviceAlias } from './deviceAlias'

describe('alias de dispositivo', () => {
  it('recorta y permite borrar el alias', () => {
    expect(normalizeDeviceAlias('  HH-ALMACEN-01 ')).toEqual({ value: 'HH-ALMACEN-01' })
    expect(normalizeDeviceAlias('   ')).toEqual({ value: null })
  })

  it('rechaza alias demasiado largos o con caracteres de control', () => {
    expect(normalizeDeviceAlias('x'.repeat(DEVICE_ALIAS_MAX_LENGTH + 1)).error).toMatch(/Máximo/)
    expect(normalizeDeviceAlias('HH\u0007-01').error).toMatch(/no válidos/)
  })

  it('muestra el alias en lugar del modelo genérico', () => {
    expect(deviceDisplayName({ alias: 'HH-ALMACEN-01', deviceName: 'Honeywell CK65' })).toBe('HH-ALMACEN-01')
    expect(deviceDisplayName({ alias: null, deviceName: 'Honeywell CK65' })).toBe('Honeywell CK65')
  })
})
