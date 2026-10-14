import type {
  ReactNode,
} from 'react'

import {
  TitanSectionCard,
} from '../ui/TitanSectionCard'

interface SettingsSectionProps {
  title: string
  description?: string
  icon?: ReactNode
  action?: ReactNode
  footer?: ReactNode
  children: ReactNode
  loading?: boolean
  elevated?: boolean
  id?: string
  className?: string
}

/*
 * Sección compartida del módulo Configuración.
 *
 * Compone TitanSectionCard (sistema global titan-theme.css)
 * y añade la identidad neutral de Configuración mediante la
 * clase .titan-settings-section. No duplica CSS.
 */
export function SettingsSection({
  title,
  description,
  icon,
  action,
  footer,
  children,
  loading = false,
  elevated = false,
  id,
  className = '',
}: SettingsSectionProps) {
  return (
    <TitanSectionCard
      id={id}
      title={title}
      description={description}
      icon={icon}
      action={action}
      loading={loading}
      elevated={elevated}
      className={
        'titan-settings-section ' +
        className
      }
    >
      {children}

      {footer && (
        <div
          className="titan-settings-section__footer"
        >
          {footer}
        </div>
      )}
    </TitanSectionCard>
  )
}
