import type { ReactNode } from 'react'
import { useDeferredValue, useEffect, useMemo, useState } from 'react'

export type InventoryRow = Record<string, unknown>

export type InventoryColumn =
  | [string, string]
  | { key: string; label: string; render?: (row: InventoryRow) => ReactNode }

type ResolvedColumn = { key: string; label: string; render?: (row: InventoryRow) => ReactNode }

const PAGE_SIZES = [25, 50, 100] as const

const okStates = new Set([
  'disponible',
  'activa',
  'activo',
  'recibida',
  'cerrada',
  'completado',
  'completada',
  'devuelto',
  'devuelta',
  'open',
  'bueno',
  'nuevo',
  'true',
])

const infoStates = new Set([
  'asignado',
  'asignada',
  'entregado',
  'entregada',
  'en uso',
  'transito',
])

const maintenanceStates = new Set([
  'mantenimiento',
  'en mantenimiento',
  'reparacion',
  'reservado',
])

const warnStates = new Set([
  'en proceso',
  'pendiente',
  'por vencer',
])

const money = (value: unknown) => {
  const amount = Number(value ?? 0)
  if (!Number.isFinite(amount) || amount === 0) return '—'
  return `RD$ ${amount.toLocaleString('es-DO', { maximumFractionDigits: 0 })}`
}

/* Minusculas y sin tildes: "recepcion" encuentra "Recepción". */
const normalize = (value: string) =>
  value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()

const resolveColumn = (column: InventoryColumn): ResolvedColumn =>
  Array.isArray(column) ? { key: column[0], label: column[1] } : column

const renderBadge = (value: unknown) => {
  const text = String(value ?? '—')
  const normalized = text.toLowerCase()
  const cls = okStates.has(normalized)
    ? 'inventory-badge inventory-badge--ok'
    : infoStates.has(normalized)
      ? 'inventory-badge inventory-badge--info'
      : maintenanceStates.has(normalized)
        ? 'inventory-badge inventory-badge--maintenance'
        : warnStates.has(normalized)
          ? 'inventory-badge inventory-badge--warn'
          : normalized === 'danado' || normalized === 'baja' || normalized === 'malo'
            ? 'inventory-badge inventory-badge--danger'
            : 'inventory-badge'

  const label = text === 'true' ? 'Sí' : text === 'false' ? 'No' : text
  return <span className={cls}>{label}</span>
}

function renderCell(column: ResolvedColumn, row: InventoryRow): ReactNode {
  if (column.render) return column.render(row)

  const value = row[column.key]
  if (['status', 'condition', 'returned', 'found', 'isActive'].includes(column.key)) return renderBadge(value)
  if (column.key === 'purchaseCost') return money(value)
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return value.slice(0, 10)
  return String(value ?? '—')
}

function readHiddenColumns(storageKey: string): Set<string> {
  try {
    const raw = localStorage.getItem(storageKey)
    const parsed: unknown = raw ? JSON.parse(raw) : []
    return new Set(
      Array.isArray(parsed)
        ? parsed.filter((item): item is string => typeof item === 'string' && item !== '__columns__')
        : [],
    )
  } catch {
    return new Set<string>()
  }
}

export function InventoryDataTable({
  rows,
  columns,
  onPick,
  emptyText = 'No hay registros para mostrar.',
  tableKey = 'inventory-table',
  toolbar,
  query: controlledQuery,
  onQueryChange,
}: {
  rows: InventoryRow[]
  columns: InventoryColumn[]
  onPick?: (row: InventoryRow) => void
  emptyText?: string
  tableKey?: string
  /** Acciones extra (filtros rapidos, exportaciones) junto al buscador. */
  toolbar?: ReactNode
  /** Busqueda controlada por la pantalla (p. ej. para exportar con el mismo texto). */
  query?: string
  onQueryChange?: (value: string) => void
}) {
  const storageKey = `${tableKey}.hiddenColumns`
  const [localQuery, setLocalQuery] = useState('')
  const query = controlledQuery ?? localQuery
  const setQuery = (value: string) => {
    if (onQueryChange) onQueryChange(value)
    if (controlledQuery === undefined) setLocalQuery(value)
  }
  const [columnsOpen, setColumnsOpen] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0])
  const [hiddenColumns, setHiddenColumns] = useState<Set<string>>(() => readHiddenColumns(storageKey))

  const resolved = useMemo(() => columns.map(resolveColumn), [columns])

  const visibleColumns = useMemo(
    () => resolved.filter(column => !hiddenColumns.has(column.key)),
    [resolved, hiddenColumns],
  )

  const toggleColumn = (key: string) => {
    setHiddenColumns(prev => {
      const next = new Set(prev)
      if (next.has(key)) {
        next.delete(key)
      } else if (resolved.length - next.size > 1) {
        // Siempre debe quedar al menos una columna visible.
        next.add(key)
      }
      try {
        localStorage.setItem(storageKey, JSON.stringify([...next]))
      } catch {
        // almacenamiento no disponible
      }
      return next
    })
  }

  // Indice de busqueda precalculado: una cadena normalizada por fila, una sola vez por carga.
  const searchIndex = useMemo(
    () => rows.map(row => normalize(Object.values(row).map(value => (value == null || typeof value === 'object' ? '' : String(value))).join(' '))),
    [rows],
  )

  // La escritura nunca espera al filtrado de listas grandes.
  const deferredQuery = useDeferredValue(query)

  const filtered = useMemo(() => {
    const terms = normalize(deferredQuery).split(/\s+/).filter(Boolean)
    if (terms.length === 0) return rows
    // Todos los terminos deben aparecer ("dell ana" encuentra el equipo Dell de Ana).
    return rows.filter((_, index) => terms.every(term => searchIndex[index].includes(term)))
  }, [rows, searchIndex, deferredQuery])

  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize))

  // Al filtrar o cambiar el tamano se vuelve a una pagina valida.
  useEffect(() => {
    setPage(current => Math.min(current, pageCount))
  }, [pageCount])

  useEffect(() => {
    setPage(1)
  }, [deferredQuery, pageSize])

  const pageRows = useMemo(
    () => filtered.slice((page - 1) * pageSize, page * pageSize),
    [filtered, page, pageSize],
  )

  const firstIndex = filtered.length === 0 ? 0 : (page - 1) * pageSize + 1
  const lastIndex = Math.min(page * pageSize, filtered.length)

  return (
    <div className="inventory-table-wrap">
      <div className="inventory-filters">
        <input
          className="inventory-input"
          placeholder="Buscar en esta pantalla..."
          value={query}
          onChange={event => setQuery(event.target.value)}
          aria-label="Buscar en la tabla"
        />
        <button
          type="button"
          className={columnsOpen ? 'inventory-btn inventory-btn--small inventory-btn--active' : 'inventory-btn inventory-btn--small'}
          onClick={() => setColumnsOpen(open => !open)}
          aria-expanded={columnsOpen}
          aria-label="Seleccionar columnas visibles"
        >
          Columnas ({visibleColumns.length}/{resolved.length})
        </button>
        {toolbar}
        <span className="inventory-muted inventory-filters__count">
          {filtered.length} de {rows.length} registro(s)
        </span>
      </div>

      {columnsOpen && (
        <div className="inventory-columns-panel" role="group" aria-label="Columnas visibles">
          {resolved.map(column => (
            <label key={column.key} className="inventory-column-option">
              <input
                type="checkbox"
                checked={!hiddenColumns.has(column.key)}
                onChange={() => toggleColumn(column.key)}
              />
              {column.label}
            </label>
          ))}
        </div>
      )}

      <div className="inventory-table-scroll">
        <table className="inventory-table">
          <thead>
            <tr>
              {visibleColumns.map(column => (
                <th key={column.key} scope="col">{column.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {pageRows.map((row, index) => {
              const key = String(row.id ?? row.assetTag ?? `row-${firstIndex + index}`)
              return (
                <tr
                  key={key}
                  className={onPick ? 'inventory-row inventory-row--clickable' : 'inventory-row'}
                  onClick={onPick ? () => onPick(row) : undefined}
                >
                  {visibleColumns.map(column => (
                    <td key={column.key} data-label={column.label}>{renderCell(column, row)}</td>
                  ))}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {filtered.length === 0 ? (
        <p className="inventory-muted inventory-table-empty">{emptyText}</p>
      ) : (
        <nav className="inventory-pagination" aria-label="Paginación">
          <span className="inventory-muted">
            {firstIndex}–{lastIndex} de {filtered.length}
          </span>
          <label className="inventory-pagination__size">
            Filas
            <select value={pageSize} onChange={event => setPageSize(Number(event.target.value))}>
              {PAGE_SIZES.map(size => (
                <option key={size} value={size}>{size}</option>
              ))}
            </select>
          </label>
          <div className="inventory-pagination__pages">
            <button type="button" className="inventory-btn inventory-btn--small" disabled={page <= 1} onClick={() => setPage(1)} aria-label="Primera página">«</button>
            <button type="button" className="inventory-btn inventory-btn--small" disabled={page <= 1} onClick={() => setPage(p => p - 1)} aria-label="Página anterior">‹</button>
            <span className="inventory-pagination__current">{page} / {pageCount}</span>
            <button type="button" className="inventory-btn inventory-btn--small" disabled={page >= pageCount} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente">›</button>
            <button type="button" className="inventory-btn inventory-btn--small" disabled={page >= pageCount} onClick={() => setPage(pageCount)} aria-label="Última página">»</button>
          </div>
        </nav>
      )}
    </div>
  )
}
