import type { TitanLoginRobotMode } from './TitanLoginRobot'

export type TitanLoginAnimationState =
  | 'boot'
  | 'idle'
  | 'greeting'
  | 'attention'
  | 'email-focus'
  | 'password-focus'
  | 'authenticating'
  | 'success'
  | 'failure'
  | 'exit'
  | 'reduced-motion'

export interface TitanLoginAnimationDefinition {
  durationMs: number
  loop: boolean
  fallback: TitanLoginAnimationState
  spriteUrl: string | null
}

/*
 * Contrato para el arte definitivo. Los recursos permanecen en
 * null de forma intencional: el robot CSS actual es el fallback y
 * no se declara una hoja de sprites que todavía no existe.
 */
export const TITAN_LOGIN_ANIMATION_MANIFEST:
  Record<TitanLoginAnimationState, TitanLoginAnimationDefinition> = {
    boot: { durationMs: 900, loop: false, fallback: 'idle', spriteUrl: null },
    idle: { durationMs: 2400, loop: true, fallback: 'idle', spriteUrl: null },
    greeting: { durationMs: 700, loop: false, fallback: 'idle', spriteUrl: null },
    attention: { durationMs: 420, loop: false, fallback: 'idle', spriteUrl: null },
    'email-focus': { durationMs: 520, loop: true, fallback: 'idle', spriteUrl: null },
    'password-focus': { durationMs: 520, loop: true, fallback: 'idle', spriteUrl: null },
    authenticating: { durationMs: 900, loop: true, fallback: 'attention', spriteUrl: null },
    success: { durationMs: 620, loop: false, fallback: 'exit', spriteUrl: null },
    failure: { durationMs: 600, loop: false, fallback: 'idle', spriteUrl: null },
    exit: { durationMs: 1230, loop: false, fallback: 'idle', spriteUrl: null },
    'reduced-motion': { durationMs: 0, loop: false, fallback: 'idle', spriteUrl: null },
  }

export function resolveLoginAnimationState(
  mode: TitanLoginRobotMode,
  options: {
    booting: boolean
    greeting: boolean
    attention: boolean
    reducedMotion: boolean
  },
): TitanLoginAnimationState {
  if (options.reducedMotion) {
    return 'reduced-motion'
  }

  if (options.booting) {
    return 'boot'
  }

  if (options.greeting && mode === 'idle') {
    return 'greeting'
  }

  if (options.attention) {
    return 'attention'
  }

  switch (mode) {
    case 'email':
      return 'email-focus'
    case 'password':
      return 'password-focus'
    case 'authenticating':
      return 'authenticating'
    case 'success':
      return 'success'
    case 'error':
      return 'failure'
    case 'launch':
      return 'exit'
    default:
      return 'idle'
  }
}

export function preloadLoginAnimationAssets(): void {
  if (typeof Image === 'undefined') {
    return
  }

  const urls = new Set(
    Object.values(TITAN_LOGIN_ANIMATION_MANIFEST)
      .map(definition => definition.spriteUrl)
      .filter((url): url is string => Boolean(url)),
  )

  for (const url of urls) {
    const image = new Image()
    image.decoding = 'async'
    image.src = url
  }
}
