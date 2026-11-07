import { useState } from 'react'
import { Check, Pencil, X } from 'lucide-react'

import { useAuth } from '../../../auth/AuthContext'
import { devicesApi } from '../../../api/devicesApi'
import { DEVICE_ALIAS_MAX_LENGTH, normalizeDeviceAlias } from './deviceAlias'

import './DeviceAliasEditor.css'

interface Props {
  deviceId: string
  alias: string | null | undefined
  onSaved?: (alias: string | null) => void
  compact?: boolean
}

/**
 * Alias operativo del equipo (p. ej. HH-ALMACEN-01). El agente Android lo
 * recibe en su siguiente heartbeat y lo muestra en su cabecera.
 */
export function DeviceAliasEditor({ deviceId, alias, onSaved, compact = false }: Props) {
  const { hasPermission } = useAuth()
  const canEdit = hasPermission('devices.update')
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState(alias ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const start = () => {
    setValue(alias ?? '')
    setError(null)
    setEditing(true)
  }

  const save = async () => {
    const normalized = normalizeDeviceAlias(value)
    if (normalized.error) {
      setError(normalized.error)
      return
    }

    try {
      setSaving(true)
      setError(null)
      const result = await devicesApi.updateAlias(deviceId, normalized.value)
      onSaved?.(result.alias)
      setEditing(false)
    } catch {
      setError('No se pudo guardar el alias.')
    } finally {
      setSaving(false)
    }
  }

  if (!editing) {
    return (
      <span className={compact ? 'device-alias device-alias--compact' : 'device-alias'}>
        {alias ? (
          <span className="device-alias__value" title="Alias del equipo">{alias}</span>
        ) : (
          !compact && <span className="device-alias__empty">Sin alias</span>
        )}
        {canEdit && (
          <button
            type="button"
            className="device-alias__edit"
            onClick={start}
            aria-label={alias ? `Editar alias ${alias}` : 'Asignar alias'}
            title={alias ? 'Editar alias' : 'Asignar alias'}
          >
            <Pencil size={13} />
            {!alias && compact && <span>Alias</span>}
          </button>
        )}
      </span>
    )
  }

  return (
    <span className="device-alias device-alias--editing">
      <input
        value={value}
        maxLength={DEVICE_ALIAS_MAX_LENGTH}
        placeholder="HH-ALMACEN-01"
        autoFocus
        disabled={saving}
        aria-label="Alias del equipo"
        onChange={event => setValue(event.target.value)}
        onKeyDown={event => {
          if (event.key === 'Enter') void save()
          if (event.key === 'Escape') setEditing(false)
        }}
      />
      <button type="button" onClick={() => void save()} disabled={saving} aria-label="Guardar alias">
        <Check size={14} />
      </button>
      <button type="button" onClick={() => setEditing(false)} disabled={saving} aria-label="Cancelar">
        <X size={14} />
      </button>
      {error && <small className="device-alias__error">{error}</small>}
    </span>
  )
}
