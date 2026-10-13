import {
  useEffect,
  useState,
} from 'react'

import {
  RefreshCw,
  Search,
  UserRoundCog,
} from 'lucide-react'

import {
  sitesApi,
  type Site,
  type SiteAssignmentUser,
  type SiteLocation,
} from '../../api/sitesApi'

import {
  retryOpenRouting,
  type RoutingQueueResult,
} from '../../api/helpdeskRoutingApi'

import {
  useAuth,
} from '../../auth/AuthContext'

import './SiteUserAssignmentsPanel.css'

type UserKind =
  'all' |
  'technicians' |
  'requesters'

interface Props {
  site: Site
  locations: SiteLocation[]
}

function messageFrom(
  error: unknown,
) {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
  ) {
    const response = (
      error as {
        response?: {
          data?: {
            message?: string
          }
        }
      }
    ).response

    if (response?.data?.message) {
      return response.data.message
    }
  }

  return error instanceof Error
    ? error.message
    : 'No fue posible completar la operación.'
}

export function SiteUserAssignmentsPanel({
  site,
  locations,
}: Props) {
  const {
    hasPermission,
  } = useAuth()

  const canView =
    hasPermission('users.view')

  const canManage =
    hasPermission('users.manage')
    && hasPermission('sites.manage')

  const canProcessRouting = [
    'helpdesk.routing.manage',
    'helpdesk.technicians.manage',
    'helpdesk.groups.manage',
    'helpdesk.admin.access',
    'helpdesk.manage',
    'settings.manage',
  ].some(hasPermission)

  const [users, setUsers] =
    useState<SiteAssignmentUser[]>([])
  const [selected, setSelected] =
    useState<string[]>([])
  const [search, setSearch] =
    useState('')
  const [kind, setKind] =
    useState<UserKind>('all')
  const [unassignedOnly, setUnassignedOnly] =
    useState(false)
  const [locationId, setLocationId] =
    useState('')
  const [busy, setBusy] =
    useState(false)
  const [error, setError] =
    useState('')
  const [queue, setQueue] =
    useState<RoutingQueueResult | null>(null)

  async function loadUsers() {
    try {
      setBusy(true)
      setError('')
      setUsers(
        await sitesApi.getAssignmentUsers(),
      )
    }
    catch (exception) {
      setError(messageFrom(exception))
    }
    finally {
      setBusy(false)
    }
  }

  useEffect(
    () => {
      const timeoutId = window.setTimeout(
        () => {
          setSelected([])
          setLocationId('')
          setQueue(null)

          if (canView) {
            void loadUsers()
          }
        },
        0,
      )

      return () => window.clearTimeout(timeoutId)
    },
    [site.id, canView],
  )

  if (!canView) {
    return null
  }

  const normalizedSearch =
    search.trim().toLocaleLowerCase()

  const visibleUsers = users.filter(
    user => {
      if (
        kind === 'technicians'
        && !user.isTechnician
      ) {
        return false
      }

      if (
        kind === 'requesters'
        && user.isTechnician
      ) {
        return false
      }

      if (unassignedOnly && user.siteId) {
        return false
      }

      if (!normalizedSearch) {
        return true
      }

      return [
        user.fullName,
        user.email,
        ...user.groups,
      ].some(value =>
        value.toLocaleLowerCase().includes(
          normalizedSearch,
        ),
      )
    },
  )

  async function assign(
    remove: boolean,
  ) {
    if (!selected.length) {
      return
    }

    try {
      setBusy(true)
      setError('')
      setQueue(null)

      await sitesApi.assignUsers(
        selected,
        remove ? null : site.id,
        remove || !locationId
          ? null
          : locationId,
      )

      setSelected([])
      await loadUsers()
    }
    catch (exception) {
      setError(messageFrom(exception))
    }
    finally {
      setBusy(false)
    }
  }

  async function runRouting(
    dryRun: boolean,
  ) {
    try {
      setBusy(true)
      setError('')
      setQueue(
        await retryOpenRouting(50, dryRun),
      )
    }
    catch (exception) {
      setError(messageFrom(exception))
    }
    finally {
      setBusy(false)
    }
  }

  return (
    <section className="site-user-assignments">
      <header>
        <div>
          <h3>
            <UserRoundCog size={17} />
            Usuarios y técnicos
          </h3>

          <p>
            La localidad física no cambia roles,
            permisos ni cobertura de grupos.
          </p>
        </div>

        <button
          type="button"
          disabled={busy}
          onClick={() => void loadUsers()}
        >
          <RefreshCw size={14} />
          Actualizar
        </button>
      </header>

      <div className="site-user-assignments__filters">
        <label>
          <Search size={15} />
          <input
            value={search}
            onChange={event =>
              setSearch(event.target.value)
            }
            placeholder="Nombre, correo o grupo"
          />
        </label>

        <select
          value={kind}
          onChange={event =>
            setKind(event.target.value as UserKind)
          }
        >
          <option value="all">Todos</option>
          <option value="technicians">Técnicos</option>
          <option value="requesters">Solicitantes</option>
        </select>

        <label className="site-user-assignments__check">
          <input
            type="checkbox"
            checked={unassignedOnly}
            onChange={event =>
              setUnassignedOnly(event.target.checked)
            }
          />
          Sin localidad
        </label>
      </div>

      {error && (
        <p className="site-user-assignments__error">
          {error}
        </p>
      )}

      <div className="site-user-assignments__list">
        {visibleUsers.map(user => (
          <label key={user.id}>
            <input
              type="checkbox"
              disabled={!canManage || busy}
              checked={selected.includes(user.id)}
              onChange={event =>
                setSelected(current =>
                  event.target.checked
                    ? [...current, user.id]
                    : current.filter(id => id !== user.id),
                )
              }
            />

            <span>
              <strong>{user.fullName}</strong>
              <small>
                {user.email}
                {user.groups.length
                  ? ` · ${user.groups.join(', ')}`
                  : ' · Solicitante'}
              </small>
            </span>

            <em>
              {user.siteId === site.id
                ? user.siteLocationName ?? site.name
                : user.siteName ?? 'Sin localidad'}
            </em>
          </label>
        ))}

        {!busy && !visibleUsers.length && (
          <p>No hay usuarios que coincidan con los filtros.</p>
        )}
      </div>

      {canManage && (
        <div className="site-user-assignments__actions">
          <select
            value={locationId}
            disabled={busy}
            onChange={event =>
              setLocationId(event.target.value)
            }
          >
            <option value="">Sin sublocalidad</option>
            {locations
              .filter(location => location.isActive)
              .map(location => (
                <option
                  key={location.id}
                  value={location.id}
                >
                  {location.name}
                </option>
              ))}
          </select>

          <button
            type="button"
            disabled={busy || !selected.length}
            onClick={() => void assign(false)}
          >
            Asignar {selected.length || ''}
          </button>

          <button
            type="button"
            disabled={busy || !selected.length}
            onClick={() => void assign(true)}
          >
            Quitar localidad
          </button>
        </div>
      )}

      {canProcessRouting && (
        <div className="site-user-assignments__routing">
          <div>
            <strong>Tickets existentes</strong>
            <small>
              Simula antes de reprocesar tickets abiertos sin asignar.
            </small>
          </div>

          <button
            type="button"
            disabled={busy}
            onClick={() => void runRouting(true)}
          >
            Simular
          </button>

          <button
            type="button"
            disabled={busy || queue?.dryRun !== true}
            onClick={() => void runRouting(false)}
          >
            Reprocesar
          </button>
        </div>
      )}

      {queue && (
        <p className="site-user-assignments__result">
          {queue.dryRun ? 'Simulación' : 'Ejecución'}:
          {' '}{queue.considered} evaluados,
          {' '}{queue.assigned} asignados,
          {' '}{queue.skipped} omitidos,
          {' '}{queue.failed} fallidos.
        </p>
      )}
    </section>
  )
}
