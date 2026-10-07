import type {
  ReactNode,
} from 'react'

interface Props {
  title: string
  value: string
  detail: string

  icon: ReactNode

  tone:
    | 'success'
    | 'warning'
    | 'danger'
    | 'unknown'
}

export function WindowsTelemetryCard({
  title,
  value,
  detail,
  icon,
  tone,
}: Props) {
  return (
    <article
      className={
        `windows-telemetry-card windows-telemetry-card--${tone}`
      }
    >
      <div className="windows-telemetry-card__header">
        {icon}

        <span>
          {title}
        </span>

        <i
          className={
            `windows-telemetry-state windows-telemetry-state--${tone}`
          }
        />
      </div>

      <strong>
        {value}
      </strong>

      <small>
        {detail}
      </small>
    </article>
  )
}