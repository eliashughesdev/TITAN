import {
  useEffect,
  useState,
} from 'react'
import apiClient from '../../api/apiClient'

interface CategoryResponse {
  items: string[]
  configured: boolean
}

interface Props {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  id?: string
}

export function HelpdeskCategorySelect({
  value,
  onChange,
  disabled = false,
  id = 'helpdesk-category',
}: Props) {
  const [items, setItems] =
    useState<string[]>(['general'])

  const [loading, setLoading] =
    useState(true)

  const [error, setError] =
    useState(false)

  useEffect(() => {
    let active = true

    async function load() {
      try {
        const response =
          await apiClient.get<CategoryResponse>(
            '/helpdesk/categories',
          )

        if (!active) return

        const categories =
          response.data.items?.length
            ? response.data.items
            : ['general']

        setItems(categories)
        setError(false)

        if (
          value &&
          !categories.some(
            (category) =>
              category.toLowerCase() ===
              value.toLowerCase(),
          )
        ) {
          onChange('general')
        }
      } catch {
        if (active) {
          setError(true)
          setItems(['general'])
          onChange('general')
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      active = false
    }
    // El catálogo se carga al montar el formulario;
    // los cambios de selección no generan nuevas peticiones.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="helpdesk-category-select">
      <label htmlFor={id}>
        Categoría
      </label>

      <select
        id={id}
        value={
          items.includes(value)
            ? value
            : 'general'
        }
        disabled={disabled || loading}
        onChange={(event) =>
          onChange(event.target.value)
        }
      >
        {items.map((category) => (
          <option
            key={category}
            value={category}
          >
            {category === 'general'
              ? 'General'
              : category}
          </option>
        ))}
      </select>

      {loading && (
        <small role="status">
          Cargando categorías…
        </small>
      )}

      {error && (
        <small role="alert">
          No se pudo cargar el catálogo;
          se utilizará General.
        </small>
      )}
    </div>
  )
}