import type {
  ReactNode,
} from 'react'

type SettingsStatusTone =
  | 'success'
  | 'error'
  | 'warning'
  | 'info'
  | 'neutral'
  | 'loading'

interface SettingsStatusProps {
  tone?: SettingsStatusTone
  title?: string
  icon?: ReactNode
  children?: ReactNode
  role?: 'status' | 'alert'
}

const noticeTone:
  Record<
    SettingsStatusTone,
    string
  > = {
    success:
      'titan-notice--success',

    error:
      'titan-notice--danger',

    warning:
      'titan-notice--warning',

    info:
      'titan-notice--info',

    neutral:
      'titan-notice--neutral',

    loading:
      'titan-notice--info ' +
      'titan-settings-status--loading',
  }

/*
 * Estado compartido (éxito, error, aviso, carga) para el
 * módulo Configuración. Reutiliza .titan-notice del sistema
 * global y mantiene role/aria correctos.
 */
export function SettingsStatus({
  tone = 'info',
  title,
  icon,
  children,
  role,
}: SettingsStatusProps) {
  const isAlert =
    role
    ?? (tone === 'error'
      ? 'alert'
      : 'status')

  return (
    <div
      role={isAlert}
      className={
        'titan-notice ' +
        `titan-settings-status ${
          noticeTone[tone]
        }`
      }
    >
      {icon && (
        <span
          className="titan-settings-status__icon"
          aria-hidden="true"
        >
          {icon}
        </span>
      )}

      <div
        className="titan-settings-status__body"
      >
        {title && (
          <strong>
            {title}
          </strong>
        )}

        {children}
      </div>
    </div>
  )
}
