import type { LucideIcon } from 'lucide-react'

interface ModulePlaceholderProps {
  title: string
  description: string
  icon: LucideIcon
}

export function ModulePlaceholder({
  title,
  description,
  icon: Icon,
}: ModulePlaceholderProps) {
  return (
    <div className="module-placeholder">
      <div className="module-placeholder-icon">
        <Icon size={26} />
      </div>

      <div>
        <span className="module-eyebrow">
          TitanMDM Enterprise
        </span>

        <h1>{title}</h1>

        <p>{description}</p>
      </div>

      <div className="module-placeholder-status">
        <span className="status-dot" />
        Módulo preparado
      </div>
    </div>
  )
}