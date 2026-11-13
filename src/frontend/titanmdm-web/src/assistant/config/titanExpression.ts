import type { TitanAnimationState } from '../context/TitanAssistantContext'

/** Expresión visible de Titan (ojos, boca y gesto) para cada estado. */
export type TitanExpression =
  | 'idle'
  | 'thinking'
  | 'talking'
  | 'success'
  | 'warning'
  | 'error'
  | 'sleeping'

export function expressionFor(animation: TitanAnimationState): TitanExpression {
  switch (animation) {
    case 'thinking':
    case 'looking':
      return 'thinking'
    case 'talking':
    case 'pointing':
      return 'talking'
    case 'success':
      return 'success'
    case 'warning':
      return 'warning'
    case 'error':
      return 'error'
    case 'sleeping':
    case 'hiding':
      return 'sleeping'
    default:
      return 'idle'
  }
}
