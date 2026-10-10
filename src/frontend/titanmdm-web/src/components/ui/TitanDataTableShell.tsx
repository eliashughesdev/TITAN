
import type {
  ReactNode,
} from 'react'

import {
  motion,
  useReducedMotion,
} from 'motion/react'

import {
  TableProperties,
} from 'lucide-react'

interface TitanDataTableShellProps {
  title: string
  subtitle?: string
  toolbar?: ReactNode
  children: ReactNode

  loading?: boolean
  empty?: boolean
  emptyMessage?: string
  className?: string
}

export function TitanDataTableShell({
  title,
  subtitle,
  toolbar,
  children,
  loading = false,
  empty = false,
  emptyMessage = 'No hay registros disponibles.',
  className = '',
}: TitanDataTableShellProps) {
  const reduceMotion = useReducedMotion()

  return (
    <motion.section
      className={[
        'titan-table-shell',
        'titan-enterprise-table',
        className,
      ].filter(Boolean).join(' ')}
      aria-busy={loading}
      initial={
        reduceMotion
          ? false
          : { opacity: 0, y: 6 }
      }
      animate={{
        opacity: 1,
        y: 0,
      }}
      transition={{
        duration: reduceMotion ? 0 : 0.2,
      }}
    >
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

      {loading ? (
        <div
          className="titan-table-loading"
          role="status"
        >
          {Array.from({ length: 5 }, (_, index) => (
            <div
              className="titan-skeleton-line"
              key={index}
            />
          ))}

          <span className="sr-only">
            Cargando registros
          </span>
        </div>
      ) : empty ? (
        <div className="titan-table-empty">
          <TableProperties
            size={30}
            aria-hidden="true"
          />
          <strong>
            Sin registros
          </strong>
          <p>
            {emptyMessage}
          </p>
        </div>
      ) : (
        <div className="titan-table-scroll">
          {children}
        </div>
      )}
    </motion.section>
  )
}
