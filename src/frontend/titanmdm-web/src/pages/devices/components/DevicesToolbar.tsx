import {
  Search,
} from 'lucide-react'

import type {
  DeviceManagedFilter,
  DeviceSortDirection,
} from '../hooks/useDevicesInventory'

interface Props {
  search: string

  status: string

  compliance: string

  managedFilter:
    DeviceManagedFilter

  sortBy: string

  sortDirection:
    DeviceSortDirection

  globalPlatform:
    string

  effectivePlatform:
    string

  isGlobalWorkspace:
    boolean

  onSearchChange:
    (value: string) => void

  onStatusChange:
    (value: string) => void

  onComplianceChange:
    (value: string) => void

  onManagedFilterChange:
    (
      value:
        DeviceManagedFilter,
    ) => void

  onSortByChange:
    (value: string) => void

  onSortDirectionChange:
    (
      value:
        DeviceSortDirection,
    ) => void

  onPlatformChange:
    (value: string) => void
}

export function DevicesToolbar({
  search,

  status,

  compliance,

  managedFilter,

  sortBy,

  sortDirection,

  globalPlatform,

  effectivePlatform,

  isGlobalWorkspace,

  onSearchChange,
  onStatusChange,
  onComplianceChange,
  onManagedFilterChange,
  onSortByChange,
  onSortDirectionChange,
  onPlatformChange,
}: Props) {
  return (
    <div className="devices-toolbar">
      <div className="devices-search">
        <Search
          size={17}
        />

        <input
          type="search"
          placeholder="Buscar por nombre, serial, usuario, IP..."
          value={
            search
          }
          onChange={
            event =>
              onSearchChange(
                event.target.value,
              )
          }
        />
      </div>

      {isGlobalWorkspace ? (
        <select
          value={
            globalPlatform
          }
          onChange={
            event =>
              onPlatformChange(
                event.target.value,
              )
          }
        >
          <option value="">
            Todas las plataformas
          </option>

          <option value="Android">
            Android
          </option>

          <option value="Windows">
            Windows
          </option>
        </select>
      ) : (
        <select
          value={
            effectivePlatform
          }
          disabled
        >
          <option
            value={
              effectivePlatform
            }
          >
            {effectivePlatform}
          </option>
        </select>
      )}

      <select
        value={
          status
        }
        onChange={
          event =>
            onStatusChange(
              event.target.value,
            )
        }
      >
        <option value="">
          Todos los estados
        </option>

        <option value="Online">
          En línea
        </option>

        <option value="Offline">
          Fuera de línea
        </option>

        <option value="Pending">
          Pendiente
        </option>

        <option value="Enrolling">
          Inscribiendo
        </option>

        <option value="Locked">
          Bloqueado
        </option>

        <option value="Quarantined">
          Cuarentena
        </option>

        <option value="Retired">
          Retirado
        </option>

        <option value="Wiped">
          Borrado
        </option>
      </select>

      <select
        value={
          compliance
        }
        onChange={
          event =>
            onComplianceChange(
              event.target.value,
            )
        }
      >
        <option value="">
          Todo cumplimiento
        </option>

        <option value="Compliant">
          Conforme
        </option>

        <option value="NonCompliant">
          No conforme
        </option>

        <option value="Unknown">
          Desconocido
        </option>

        <option value="Evaluating">
          Evaluando
        </option>

        <option value="Quarantined">
          Cuarentena
        </option>
      </select>

      <select
        value={
          managedFilter
        }
        onChange={
          event =>
            onManagedFilterChange(
              event.target
                .value as
                DeviceManagedFilter,
            )
        }
      >
        <option value="">
          Gestión: todos
        </option>

        <option value="managed">
          Administrados
        </option>

        <option value="unmanaged">
          No administrados
        </option>
      </select>

      <select
        value={
          sortBy
        }
        onChange={
          event =>
            onSortByChange(
              event.target.value,
            )
        }
      >
        <option value="lastSeen">
          Último contacto
        </option>

        <option value="name">
          Nombre
        </option>

        <option value="platform">
          Plataforma
        </option>

        <option value="status">
          Estado
        </option>

        <option value="compliance">
          Cumplimiento
        </option>

        <option value="enrolled">
          Inscripción
        </option>
      </select>

      <select
        value={
          sortDirection
        }
        onChange={
          event =>
            onSortDirectionChange(
              event.target
                .value as
                DeviceSortDirection,
            )
        }
      >
        <option value="desc">
          Descendente
        </option>

        <option value="asc">
          Ascendente
        </option>
      </select>
    </div>
  )
}