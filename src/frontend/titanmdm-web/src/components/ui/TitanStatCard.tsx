
import type {
  ReactNode,
} from 'react'

import {
  motion,
  useReducedMotion,
} from 'motion/react'

interface TitanStatCardProps {
  icon: ReactNode
  label: string
  value: string | number
  hint?: string

  trend?: {
    value: string
    direction: 'up' | 'down' | 'neutral'
  }

  loading?: boolean
  className?: string
  onClick?: () => void
}

export function TitanStatCard({
  icon,
  label,
  value,
  hint,
  trend,
  loading = false,
  className = '',
  onClick,
}: TitanStatCardProps) {
  const reduceMotion = useReducedMotion()

  const content = (
    <>
      <div className="titan-kpi-card__icon">
        {icon}
      </div>

      <div className="titan-kpi-card__meta">
        <span className="titan-kpi-card__label">
          {label}
        </span>

        {loading ? (
          <span
            className="titan-stat-skeleton"
            aria-label="Cargando indicador"
          />
        ) : (
          <strong className="titan-kpi-card__value">
            {value}
          </strong>
        )}

        {hint && (
          <span className="titan-kpi-card__hint">
            {hint}
          </span>
        )}

        {trend && !loading && (
          <span
            className={
              `titan-stat-trend ` +
              `titan-stat-trend--${trend.direction}`
            }
          >
            {trend.value}
          </span>
        )}
      </div>
    </>
  )

  const classes = [
    'titan-kpi-card',
    'titan-stat-card',
    onClick ? 'titan-stat-card--clickable' : '',
    className,
  ].filter(Boolean).join(' ')

  const animation = {
    initial: reduceMotion
      ? false as const
      : { opacity: 0, y: 8 },
    animate: { opacity: 1, y: 0 },
    transition: {
      duration: reduceMotion ? 0 : 0.22,
    },
  }

  if (onClick) {
    return (
      <motion.button
        type="button"
        className={classes}
        onClick={onClick}
        whileHover={
          reduceMotion ? undefined : { y: -2 }
        }
        {...animation}
      >
        {content}
      </motion.button>
    )
  }

  return (
    <motion.article
      className={classes}
      {...animation}
    >
      {content}
    </motion.article>
  )
}
