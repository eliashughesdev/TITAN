import type {
  ReactNode,
} from 'react'

interface TitanDataTableShellProps {
  title: string
  subtitle?: string
  toolbar?: ReactNode
  children: ReactNode
}

export function TitanDataTableShell({
  title,
  subtitle,
  toolbar,
  children,
}: TitanDataTableShellProps) {
  return (
    <section className="titan-table-shell titan-fade-in">
      <div className="titan-table-shell__toolbar">
        <div className="titan-table-shell__toolbar-left">
          <strong className="titan-table-shell__title">
            {title}
          </strong>

          {subtitle && (
            <span className="titan-table-shell__subtitle">
              {subtitle}
            </span>
          )}
        </div>

        {toolbar && (
          <div className="titan-table-shell__toolbar-right">
            {toolbar}
          </div>
        )}
      </div>

      <div className="titan-table-scroll">
        {children}
      </div>
    </section>
  )
}