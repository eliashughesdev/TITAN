import type {
  ReactNode,
} from 'react'

interface TitanSectionCardProps {
  title: string
  description?: string
  icon?: ReactNode
  action?: ReactNode
  children: ReactNode
  elevated?: boolean
  interactive?: boolean
}

export function TitanSectionCard({
  title,
  description,
  icon,
  action,
  children,
  elevated = false,
  interactive = false,
}: TitanSectionCardProps) {
  const classes = [
    'titan-section-card',
    'titan-card',
    elevated
      ? 'titan-card--elevated'
      : '',
    interactive
      ? 'titan-card--interactive'
      : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <section className={classes}>
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

        {action}
      </div>

      <div className="titan-section-card__body">
        {children}
      </div>
    </section>
  )
}