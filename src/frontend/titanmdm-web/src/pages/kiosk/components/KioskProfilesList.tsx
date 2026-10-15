import { useMemo, useState } from 'react'
import { PanelsTopLeft, Search } from 'lucide-react'

import type { Policy } from '../../../api/policiesApi'
import {
  TitanEmptyState,
  TitanStatusBadge,
} from '../../../components/ui'
import { WindowsOperationResults } from '../../../components/windows/WindowsOperationResults'

interface Props {
  loading: boolean
  profiles: Policy[]
  selectedId: string
  onSelect: (policy: Policy) => void
}

const labels = {
  Draft: 'Borrador',
  Active: 'Activo',
  Disabled: 'Deshabilitado',
  Archived: 'Archivado',
} as const

const tones = {
  Draft: 'neutral',
  Active: 'success',
  Disabled: 'warning',
  Archived: 'danger',
} as const

export function KioskProfilesList({
  loading,
  profiles,
  selectedId,
  onSelect,
}: Props) {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')

  const visible = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()

    return profiles
      .filter((profile) =>
        (!status || profile.status === status) &&
        (!term || `${profile.name} ${profile.description ?? ''}`
          .toLocaleLowerCase()
          .includes(term)),
      )
      .sort((a, b) => a.name.localeCompare(b.name))
  }, [profiles, search, status])

  const showWindowsResults = profiles.some(
    (profile) =>
      profile.id === selectedId && profile.platform === 'Windows',
  )

  return (
    <div className="kiosk-profiles-content" aria-busy={loading}>
      <div className="kiosk-profile-filters">
        <label>
          <span>Buscar perfil</span>
          <div className="kiosk-search-input">
            <Search size={15} />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Nombre o descripción"
            />
          </div>
        </label>

        <label>
          <span>Estado</span>
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value)}
          >
            <option value="">Todos</option>
            {Object.entries(labels).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
        </label>
      </div>

      <span className="kiosk-profile-count" aria-live="polite">
        {visible.length} de {profiles.length} perfiles
      </span>

      {loading ? (
        <div className="kiosk-loading-list" role="status">
          <span />
          <span />
          <span />
          Cargando perfiles
        </div>
      ) : visible.length === 0 ? (
        <TitanEmptyState
          icon={<PanelsTopLeft size={28} />}
          title={profiles.length ? 'Sin coincidencias' : 'No hay perfiles Kiosk'}
          description={profiles.length
            ? 'Ajusta la búsqueda o el filtro de estado.'
            : 'Crea el primer perfil para definir una experiencia dedicada.'}
        />
      ) : (
        <div className="kiosk-profile-list">
          {visible.map((profile) => (
            <button
              type="button"
              key={profile.id}
              className={profile.id === selectedId
                ? 'kiosk-profile selected'
                : 'kiosk-profile'}
              aria-pressed={profile.id === selectedId}
              onClick={() => onSelect(profile)}
            >
              <div className="kiosk-profile-icon">
                <PanelsTopLeft size={19} />
              </div>
              <div className="kiosk-profile-copy">
                <strong>{profile.name}</strong>
                <span>{profile.description || 'Sin descripción'}</span>
                <small>
                  Versión {profile.currentVersion} · {profile.assignedDevices} asignados
                </small>
              </div>
              <TitanStatusBadge
                label={labels[profile.status]}
                tone={tones[profile.status]}
              />
            </button>
          ))}
        </div>
      )}

      {showWindowsResults && (
        <WindowsOperationResults key={selectedId} policyId={selectedId} />
      )}
    </div>
  )
}
