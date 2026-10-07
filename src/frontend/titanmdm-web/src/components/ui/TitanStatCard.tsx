import type {
  ReactNode,
} from 'react'

interface TitanStatCardProps {
  icon: ReactNode
  label: string
  value: string | number
  hint?: string
}

export function TitanStatCard({
  icon,
  label,
  value,
  hint,
}: TitanStatCardProps) {
  return (
    <article className="titan-kpi-card titan-slide-up">
      <div className="titan-kpi-card__icon">
        {icon}
      </div>

      <div className="titan-kpi-card__meta">
        <span className="titan-kpi-card__label">
          {label}
        </span>

        <strong className="titan-kpi-card__value">
          {value}
        </strong>

        {hint && (
          <span className="titan-kpi-card__hint">
            {hint}
          </span>
        )}
      </div>
    </article>
  )
}