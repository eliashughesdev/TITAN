
import type { ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'

import './HelpdeskExperience.css'

interface HelpdeskExperienceProps {
  children: ReactNode
  variant?: 'console' | 'portal'
}

export function HelpdeskExperience({
  children,
  variant = 'console',
}: HelpdeskExperienceProps) {
  const location = useLocation()
  const reducedMotion = useReducedMotion()

  return (
    <motion.div
      key={location.pathname}
      className={[
        'titan-helpdesk-experience',
        'min-w-0 w-full',
        'hd-experience--' + variant,
      ].join(' ')}
      initial={
        reducedMotion
          ? false
          : { opacity: 0, y: 10 }
      }
      animate={{ opacity: 1, y: 0 }}
      exit={
        reducedMotion
          ? undefined
          : { opacity: 0, y: -5 }
      }
      transition={{
        duration: reducedMotion ? 0 : 0.24,
        ease: [0.22, 1, 0.36, 1],
      }}
    >
      <div
        className="hd-experience__ambient"
        aria-hidden="true"
      />

      <div className="hd-experience__content">
        {children}
      </div>
    </motion.div>
  )
}
