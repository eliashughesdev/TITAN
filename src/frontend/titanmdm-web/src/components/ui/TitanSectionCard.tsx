
import type {
  ReactNode,
} from 'react'

import {
  motion,
  useReducedMotion,
} from 'motion/react'

interface TitanSectionCardProps {
  title: string
  description?: string
  icon?: ReactNode
  action?: ReactNode
  children: ReactNode

  elevated?: boolean
  interactive?: boolean
  loading?: boolean
  className?: string
  id?: string
}

export function TitanSectionCard({
  title,
  description,
  icon,
  action,
  children,
  elevated = false,
  interactive = false,
  loading = false,
  className = '',
  id,
}: TitanSectionCardProps) {
  const reduceMotion = useReducedMotion()

  const classes = [
    'titan-section-card',
    'titan-card',
    elevated ? 'titan-card--elevated' : '',
    interactive ? 'titan-card--interactive' : '',
    className,
  ].filter(Boolean).join(' ')

  return (
    <motion.section
      id={id}
      className={classes}
      aria-busy={loading}
      initial={
        reduceMotion
          ? false
          : { opacity: 0, y: 8 }
      }
      animate={{
        opacity: 1,
        y: 0,
      }}
      transition={{
        duration: reduceMotion ? 0 : 0.24,
      }}
    >
      <div className="titan-section-card__header">
        <div className="titan-section-card__title-wrap">
          {icon && (
            <div className="titan-section-card__icon">
              {icon}
            </div>
          )}

          <div>
            <h2 className="titan-section-card__title">
              {title}
            </h2>

            {description && (
              <p className="titan-section-card__description">
                {description}
              </p>
            )}
          </div>
        </div>

        {action && (
          <div className="titan-section-card__action">
            {action}
          </div>
        )}
      </div>

      <div className="titan-section-card__body">
        {loading ? (
          <div
            className="titan-section-loading"
            role="status"
          >
            <div className="titan-skeleton-line" />
            <div className="titan-skeleton-line" />
            <div className="titan-skeleton-line" />
            <span className="sr-only">
              Cargando contenido
            </span>
          </div>
        ) : (
          children
        )}
      </div>
    </motion.section>
  )
}
