import type {
  ReactNode,
} from 'react'

interface SettingsPageHeaderProps {
  eyebrow?: string
  title: string
  description?: string
  icon?: ReactNode
  status?: ReactNode
  actions?: ReactNode
}

/*
 * Encabezado empresarial compartido del módulo Configuración.
 *
 * Reutiliza las clases globales .titan-page-header,
 * .titan-page-title y .titan-page-description del sistema
 * titan-theme.css y sólo añade una identidad NEUTRAL para
 * Configuración (sin verde Android ni azul de plataforma).
 */
export function SettingsPageHeader({
  eyebrow,
  title,
  description,
  icon,
  status,
  actions,
}: SettingsPageHeaderProps) {
  return (
    <header
      className={
        'titan-page-header ' +
        'titan-settings-header ' +
        'titan-fade-in'
      }
    >
      <div
        className="titan-page-header__meta"
      >
        {eyebrow && (
          <span
            className={
              'titan-page-eyebrow ' +
              'titan-settings-header__eyebrow'
            }
          >
            {icon}
            {eyebrow}
          </span>
        )}

        <h1
          className="titan-page-title"
        >
          {title}
        </h1>

        {description && (
          <p
            className="titan-page-description"
          >
            {description}
          </p>
        )}
      </div>

      {(status || actions) && (
        <div
          className={
            'titan-page-actions ' +
            'titan-settings-header__actions'
          }
        >
          {status}

          {actions}
        </div>
      )}
    </header>
  )
}
