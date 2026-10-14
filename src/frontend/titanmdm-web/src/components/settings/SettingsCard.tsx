import type {
  ReactNode,
} from 'react'

import {
  ArrowRight,
} from 'lucide-react'

interface SettingsCardProps {
  title: string
  description?: string
  icon?: ReactNode
  badge?: string
  status?: ReactNode
  onClick?: () => void
  href?: string
  actionLabel?: string
  disabled?: boolean
}

/*
 * Tarjeta navegable compartida del módulo Configuración.
 *
 * Mantiene el contrato visual .titan-settings__card existente
 * y admite acción real (onClick), enlace (href) o estado
 * estático (sin acción). No crea botones decorativos: si no
 * hay onClick/href la tarjeta deja de ser interactiva.
 */
export function SettingsCard({
  title,
  description,
  icon,
  badge,
  status,
  onClick,
  href,
  actionLabel,
  disabled = false,
}: SettingsCardProps) {
  const interactive =
    Boolean(onClick)
    || Boolean(href)

  const content = (
    <>
      <div
        className="titan-settings__card-top"
      >
        {icon && (
          <span
            className="titan-settings__icon"
          >
            {icon}
          </span>
        )}

        {badge && (
          <span
            className="titan-settings__badge"
          >
            {badge}
          </span>
        )}
      </div>

      <div
        className="titan-settings__card-content"
      >
        <h3>
          {title}
        </h3>

        {description && (
          <p>
            {description}
          </p>
        )}
      </div>

      {status && (
        <div
          className="titan-settings-card__status"
        >
          {status}
        </div>
      )}

      {interactive && (
        <div
          className="titan-settings__card-action"
        >
          <span>
            {actionLabel ?? 'Configurar'}
          </span>

          <ArrowRight
            size={16}
          />
        </div>
      )}
    </>
  )

  const classes =
    'titan-settings__card ' +
    'titan-settings-card'

  if (href) {
    return (
      <a
        href={href}
        className={classes}
      >
        {content}
      </a>
    )
  }

  if (onClick) {
    return (
      <button
        type="button"
        className={classes}
        disabled={disabled}
        onClick={onClick}
      >
        {content}
      </button>
    )
  }

  return (
    <article
      className={classes}
    >
      {content}
    </article>
  )
}
