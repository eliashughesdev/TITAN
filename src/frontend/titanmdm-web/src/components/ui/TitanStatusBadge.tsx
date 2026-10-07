import type {
  ReactNode,
} from 'react'

type TitanBadgeTone =
  | 'neutral'
  | 'primary'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'

interface TitanStatusBadgeProps {
  label: string
  tone?: TitanBadgeTone
  icon?: ReactNode
}

export function TitanStatusBadge({
  label,
  tone = 'neutral',
  icon,
}: TitanStatusBadgeProps) {
  return (
    <span
      className={`titan-badge titan-badge--${tone}`}
    >
      {icon}
      {label}
    </span>
  )
}