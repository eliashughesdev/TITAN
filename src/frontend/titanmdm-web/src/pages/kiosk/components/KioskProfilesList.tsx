import { useMemo, useState } from 'react'
import {
  CheckCircle2,
  FileEdit,
  Archive,
  PauseCircle,
  PanelsTopLeft,
} from 'lucide-react'

import { WindowsOperationResults } from '../../../components/windows/WindowsOperationResults'
import type { Policy } from '../../../api/policiesApi'

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
}

const icons = {
  Draft: FileEdit,
  Active: CheckCircle2,
  Disabled: PauseCircle,
  Archived: Archive,
}

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
      .filter(
        profile =>
          (!status || profile.status === status) &&
          (!term ||
            `${profile.name} ${profile.description ?? ''}`
              .toLocaleLowerCase()
              .includes(term)),
      )
      .sort((a, b) => a.name.localeCompare(b.name))
  }, [profiles, search, status])

  const showWindowsResults =
    selectedId &&
    profiles.some(
      profile =>
        profile.id === selectedId &&
        profile.platform === 'Windows',
    )

  return (
    <section
      className="kiosk-profiles"
      aria-busy={loading}
    >
      <div className="kiosk-section-title">
        <div>
          <PanelsTopLeft size={18} />
          <strong>Perfiles de kiosk</strong>
        </div>
      </div>

      <p>
        Kiosk restringe el equipo a las aplicaciones autorizadas
        para su puesto. Guardar un perfil no lo aplica: debes
        activarlo, asignarlo y revisar el resultado del agente.
        Activo no significa aplicado en todos los equipos.
      </p>

      <div
        style={{
          display: 'grid',
          gap: 10,
          margin: '16px 0',
        }}
      >
        <label style={{ display: 'grid', gap: 6 }}>
          Buscar perfil

          <input
            value={search}
            onChange={event => setSearch(event.target.value)}
            placeholder="Nombre o descripción"
          />
        </label>

        <label style={{ display: 'grid', gap: 6 }}>
          Estado

          <select
            value={status}
            onChange={event => setStatus(event.target.value)}
          >
            <option value="">Todos</option>

            {Object.entries(labels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <p aria-live="polite">
        {visible.length} de {profiles.length} perfiles
      </p>

      {loading ? (
        <div className="kiosk-empty" role="status">
          Cargando perfiles…
        </div>
      ) : visible.length === 0 ? (
        <div className="kiosk-empty">
          {profiles.length
            ? 'No hay perfiles para estos filtros.'
            : 'Crea el primer perfil para definir las aplicaciones permitidas.'}
        </div>
      ) : (
        visible.map(profile => {
          const Icon = icons[profile.status] ?? PanelsTopLeft

          return (
            <button
              type="button"
              key={profile.id}
              className={
                profile.id === selectedId
                  ? 'kiosk-profile selected'
                  : 'kiosk-profile'
              }
              aria-pressed={profile.id === selectedId}
              onClick={() => onSelect(profile)}
            >
              <div className="kiosk-profile-icon">
                <PanelsTopLeft size={20} />
              </div>

              <div>
                <strong>{profile.name}</strong>

                <span>
                  {profile.description || 'Sin descripción'}
                </span>

                <small>
                  {profile.platform}
                  {' · Versión '}
                  {profile.currentVersion}
                  {' · '}
                  {profile.assignedDevices}
                  {' equipos asignados'}
                </small>
              </div>

              <div className="kiosk-profile-status">
                <Icon size={14} />
                {labels[profile.status] ?? profile.status}
              </div>
            </button>
          )
        })
      )}

      {showWindowsResults && (
        <WindowsOperationResults
          key={selectedId}
          policyId={selectedId}
        />
      )}
    </section>
  )
}