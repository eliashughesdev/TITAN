
import type {
  ReactNode,
} from 'react'

import {
  motion,
  useReducedMotion,
} from 'motion/react'

export interface TitanMotionPanelProps {
  children: ReactNode
  className?: string
  delay?: number
  interactive?: boolean
  as?: 'div' | 'section' | 'article'
}

export function TitanMotionPanel({
  children,
  className = '',
  delay = 0,
  interactive = false,
  as = 'div',
}: TitanMotionPanelProps) {
  const reduceMotion = useReducedMotion()

  const Component =
    as === 'section'
      ? motion.section
      : as === 'article'
        ? motion.article
        : motion.div

  return (
    <Component
      className={className}
      initial={
        reduceMotion
          ? false
          : {
              opacity: 0,
              y: 12,
            }
      }
      animate={{
        opacity: 1,
        y: 0,
        scale: 1,
      }}
      whileHover={
        !reduceMotion && interactive
          ? {
              y: -3,
              transition: {
                duration: 0.18,
              },
            }
          : undefined
      }
      transition={{
        duration: reduceMotion
          ? 0
          : 0.28,
        delay: reduceMotion
          ? 0
          : delay,
        ease: 'easeOut',
      }}
    >
      {children}
    </Component>
  )
}
