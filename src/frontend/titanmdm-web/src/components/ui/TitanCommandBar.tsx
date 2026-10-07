import type {
  ReactNode,
} from 'react'

interface TitanCommandBarProps {
  eyebrow?: string

  title: string

  hint?: string

  controls?: ReactNode

  actions?: ReactNode
}

export function TitanCommandBar({
  eyebrow = 'COMMAND BAR',
  title,
  hint,
  controls,
  actions,
}: TitanCommandBarProps) {
  return (
    <section
      className="
        titan-command-bar
        titan-slide-up
      "
    >
      <div className="titan-command-bar__meta">
        <span className="titan-command-bar__eyebrow">
          {eyebrow}
        </span>

        <strong className="titan-command-bar__title">
          {title}
        </strong>

        {hint && (
          <span className="titan-command-bar__hint">
            {hint}
          </span>
        )}
      </div>

      <div className="titan-command-bar__controls">
        {controls}
      </div>

      <div className="titan-command-bar__actions">
        {actions}
      </div>
    </section>
  )
}