import type {
  ReactNode,
} from 'react'

interface SettingsFormFieldProps {
  label: string
  htmlFor?: string
  hint?: string
  error?: string
  required?: boolean
  full?: boolean
  className?: string
  children: ReactNode
}

/*
 * Campo de formulario compartido del módulo Configuración.
 *
 * Reutiliza .titan-field-group y .titan-field-label del
 * sistema global. Los controles (input/select/textarea) deben
 * usar las clases .titan-input / .titan-select / .titan-textarea
 * para heredar foco y accesibilidad coherentes.
 */
export function SettingsFormField({
  label,
  htmlFor,
  hint,
  error,
  required = false,
  full = false,
  className = '',
  children,
}: SettingsFormFieldProps) {
  const classes = [
    'titan-field-group',
    'titan-settings-field',
    full
      ? 'titan-settings-field--full'
      : '',
    error
      ? 'titan-settings-field--invalid'
      : '',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <div className={classes}>
      <label
        className="titan-field-label"
        htmlFor={htmlFor}
      >
        {label}

        {required && (
          <span
            className="titan-settings-field__required"
            aria-hidden="true"
          >
            {' *'}
          </span>
        )}
      </label>

      {children}

      {hint && !error && (
        <small
          className="titan-settings-field__hint"
        >
          {hint}
        </small>
      )}

      {error && (
        <small
          className="titan-settings-field__error"
          role="alert"
        >
          {error}
        </small>
      )}
    </div>
  )
}
