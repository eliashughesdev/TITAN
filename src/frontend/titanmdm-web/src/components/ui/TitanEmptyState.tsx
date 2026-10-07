import type {
  ReactNode,
} from 'react'

interface TitanEmptyStateProps {
  icon?: ReactNode
  title: string
  description?: string
  action?: ReactNode
}

export function TitanEmptyState({
  icon,
  title,
  description,
  action,
}: TitanEmptyStateProps) {
  return (
    <div className="titan-empty-state titan-fade-in">
      {icon && (
        <div className="titan-empty-state__icon">
          {icon}
        </div>
      )}

      <div className="titan-empty-state__title">
        {title}
      </div>

      {description && (
        <div className="titan-empty-state__description">
          {description}
        </div>
      )}

      {action}
    </div>
  )
}