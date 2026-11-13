import { describe, expect, it } from 'vitest'

import { getTitanModule, titanModules } from '../../config/moduleRegistry'
import {
  DEFAULT_TITAN_THEME,
  isTitanExcluded,
  ledTone,
  resolveTitanTheme,
} from './titanTheme'

describe('titanTheme', () => {
  it('usa el azul LED de Titan fuera de cualquier módulo', () => {
    expect(resolveTitanTheme(null)).toEqual(DEFAULT_TITAN_THEME)
    expect(resolveTitanTheme(undefined)).toEqual(DEFAULT_TITAN_THEME)
  })

  it('adopta el color identificativo de cada módulo', () => {
    for (const module of titanModules.filter(m => m.id !== 'ponches')) {
      const theme = resolveTitanTheme(module)
      expect(theme.accent).toBe(module.theme.primary)
      expect(theme.label).toBe(module.shortTitle)
      expect(theme.led).toMatch(/^#[0-9a-f]{6}$/)
      expect(theme.glow).toMatch(/^rgba\(/)
    }
  })

  it('cada módulo tiene un LED distinto', () => {
    const leds = titanModules
      .filter(m => m.id !== 'ponches')
      .map(m => resolveTitanTheme(m).led)
    expect(new Set(leds).size).toBe(leds.length)
  })

  it('el LED es una versión más luminosa del color del módulo', () => {
    expect(ledTone('#000000', 0.5)).toBe('#808080')
    expect(resolveTitanTheme(getTitanModule('android')).led).toBe(ledTone('#16a34a'))
  })

  it('los LED de colores oscuros se aclaran hasta verse encendidos', () => {
    const lum = (hex: string) => {
      const [r, g, b] = [1, 3, 5].map(i => Number.parseInt(hex.slice(i, i + 2), 16) / 255)
        .map(v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
      return 0.2126 * r + 0.7152 * g + 0.0722 * b
    }
    for (const module of titanModules) {
      expect(lum(resolveTitanTheme(module).led)).toBeGreaterThanOrEqual(0.32)
    }
  })

  it('un color inválido no rompe a Titan', () => {
    const broken = { shortTitle: 'X', theme: { primary: 'var(--x)', primaryDark: '', soft: '', border: '', gradient: '' } }
    expect(resolveTitanTheme(broken)).toEqual(DEFAULT_TITAN_THEME)
  })

  it('se excluye de Ponches por ruta o por espacio de trabajo', () => {
    expect(isTitanExcluded('/ponches', null)).toBe(true)
    expect(isTitanExcluded('/ponches/records', 'android')).toBe(true)
    expect(isTitanExcluded('/dashboard', 'ponches')).toBe(true)
    expect(isTitanExcluded('/ponchesx', null)).toBe(false)
    expect(isTitanExcluded('/devices', 'android')).toBe(false)
    expect(isTitanExcluded('/inventory', 'inventory')).toBe(false)
  })
})
