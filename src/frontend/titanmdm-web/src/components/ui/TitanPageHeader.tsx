import type {
  ReactNode,
} from 'react'

interface TitanPageHeaderProps {
  eyebrow?: string
  title: string
  description?: string
  icon?: ReactNode
  actions?: ReactNode
}

export function TitanPageHeader({
  eyebrow,
  title,
  description,
  icon,
  actions,
}: TitanPageHeaderProps) {
  return (
    <header className="titan-page-header titan-fade-in">
      <div className="titan-page-header__meta">
        {eyebrow && (
          <span className="titan-page-eyebrow">
            {icon}
            {eyebrow}
          </span>
        )}

        <h1 className="titan-page-title">
          {title}
        </h1>

        {description && (
          <p className="titan-page-description">
            {description}
          </p>
        )}
      </div>

      {actions && (
        <div className="titan-page-actions">
          {actions}
        </div>
      )}
    </header>
  )
}