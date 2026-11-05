import { useEffect, useState } from 'react'
import apiClient from '../../api/apiClient'

export type TypeaheadResult<T = unknown> = {
  id: string
  name: string
  subtitle?: string
  raw: T
}

export function TypeaheadSearch<T = any>({
  url,
  onSelect,
  map,
  placeholder = 'Buscar...',
  loadingText = 'Buscando...',
  emptyText = 'No se encontraron coincidencias',
}: {
  url: string
  onSelect: (result: TypeaheadResult<T>) => void
  map?: (item: any) => TypeaheadResult<T>
  placeholder?: string
  loadingText?: string
  emptyText?: string
}) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<TypeaheadResult<T>[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    const text = query.trim()
    if (!text) {
      setResults([])
      setLoading(false)
      return
    }

    let active = true
    setLoading(true)
    const timer = window.setTimeout(() => {
      apiClient
        .get<any[]>(url, { params: { q: text, limit: 15 } })
        .then(response => {
          if (!active) return
          const items = Array.isArray(response.data) ? response.data : []
          setResults(
            items.map(item =>
              map
                ? map(item)
                : {
                    id: String(item.id),
                    name: String(item.name ?? item.assetTag ?? item.code ?? item.id),
                    subtitle: [item.assetTag, item.serialNumber, item.code, item.email]
                      .filter(Boolean)
                      .join(' · '),
                    raw: item as T,
                  },
            ),
          )
        })
        .catch(() => {
          if (active) setResults([])
        })
        .finally(() => {
          if (active) setLoading(false)
        })
    }, 250)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [query, url, map])

  return (
    <div className="typeahead-wrapper">
      <input
        type="text"
        className="inventory-input"
        placeholder={placeholder}
        value={query}
        onChange={event => setQuery(event.target.value)}
      />

      {loading && <span className="inventory-muted">{loadingText}</span>}

      {!loading && results.length > 0 && (
        <div className="typeahead-results">
          {results.map(result => (
            <button
              key={result.id}
              type="button"
              className="typeahead-item"
              onClick={() => {
                onSelect(result)
                setResults([])
                setQuery('')
              }}
            >
              <strong>{result.name}</strong>
              {result.subtitle && <small>{result.subtitle}</small>}
            </button>
          ))}
        </div>
      )}

      {!loading && query.trim() && results.length === 0 && (
        <span className="inventory-muted">{emptyText}</span>
      )}
    </div>
  )
}
