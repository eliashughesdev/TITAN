import type {
  TitanModuleDefinition,
  TitanModuleId,
} from '../../config/moduleRegistry'

/**
 * Colores de Titan según el módulo activo. El cuerpo (azul cobalto y
 * plata) no cambia; las luces LED (ojos, escudo, articulaciones y
 * acentos de la interfaz) adoptan el color identificativo del módulo.
 */
export interface TitanTheme {
  /** Color del módulo: acentos de botones y bordes. */
  accent: string
  /** Versión luminosa del color: LED de ojos, escudo y juntas. */
  led: string
  /** Halo del LED (con transparencia). */
  glow: string
  /** Nombre visible del módulo. */
  label: string
}

/** Azul LED de Titan fuera de cualquier módulo. */
export const DEFAULT_TITAN_THEME: TitanTheme = {
  accent: '#2f6bff',
  led: '#5cc8ff',
  glow: 'rgba(92, 200, 255, 0.55)',
  label: 'Inicio',
}

function parseHex(color: string): [number, number, number] | null {
  const match = /^#?([0-9a-f]{6})$/i.exec(color.trim())
  if (!match) return null
  const value = Number.parseInt(match[1], 16)
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255]
}

function toHex([r, g, b]: [number, number, number]): string {
  return `#${[r, g, b]
    .map(channel => Math.round(channel).toString(16).padStart(2, '0'))
    .join('')}`
}

function mix(rgb: [number, number, number], amount: number): [number, number, number] {
  return rgb.map(c => c + (255 - c) * amount) as [number, number, number]
}

/** Luminancia relativa (WCAG) de 0 (negro) a 1 (blanco). */
function luminance([r, g, b]: [number, number, number]): number {
  const linear = (c: number) => {
    const v = c / 255
    return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)
}

/** Brillo mínimo para que un LED se perciba encendido sobre el visor oscuro. */
const MIN_LED_LUMINANCE = 0.32

/**
 * Mezcla con blanco: un LED se ve más claro que el color de marca. Sin
 * cantidad explícita, aclara lo justo para que colores oscuros (pizarra
 * de Administración, azul de Windows) sigan viéndose encendidos.
 */
export function ledTone(color: string, amount?: number): string {
  const rgb = parseHex(color)
  if (!rgb) return DEFAULT_TITAN_THEME.led
  if (amount !== undefined) return toHex(mix(rgb, amount))

  for (const step of [0.32, 0.42, 0.52, 0.62]) {
    const candidate = mix(rgb, step)
    if (luminance(candidate) >= MIN_LED_LUMINANCE) return toHex(candidate)
  }
  return toHex(mix(rgb, 0.7))
}

export function glowTone(color: string, alpha = 0.55): string {
  const rgb = parseHex(color)
  if (!rgb) return DEFAULT_TITAN_THEME.glow
  return `rgba(${rgb[0]}, ${rgb[1]}, ${rgb[2]}, ${alpha})`
}

export function resolveTitanTheme(
  module: Pick<TitanModuleDefinition, 'theme' | 'shortTitle'> | null | undefined,
): TitanTheme {
  const primary = module?.theme.primary
  if (!primary || !parseHex(primary)) return DEFAULT_TITAN_THEME

  const led = ledTone(primary)
  return {
    accent: primary,
    led,
    glow: glowTone(led),
    label: module.shortTitle,
  }
}

/**
 * Titan no aparece en Ponches (allí asiste Fiorella), ni por la ruta
 * ni cuando Ponches es el espacio de trabajo activo.
 */
export function isTitanExcluded(
  pathname: string,
  workspaceId: TitanModuleId | null | undefined,
): boolean {
  return (
    workspaceId === 'ponches' ||
    pathname === '/ponches' ||
    pathname.startsWith('/ponches/')
  )
}
