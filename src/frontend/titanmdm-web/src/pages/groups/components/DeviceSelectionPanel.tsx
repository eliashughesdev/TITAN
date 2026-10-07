import {
  Laptop,
  Search,
  Smartphone,
  UserPlus,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'

import type {
  DeviceGroupDetails,
} from '../../../api/deviceGroupsApi'
import type {
  DeviceListItem,
} from '../../../types/device'
import type {
  PlatformFilter,
  StatusFilter,
} from '../deviceGroups.types'

interface DeviceSelectionPanelProps {
  devices: DeviceListItem[]
  selectedDeviceIds: Set<string>
  selectedWindowsCount: number
  selectedGroup: DeviceGroupDetails | null
  search: string
  platformFilter: PlatformFilter
  statusFilter: StatusFilter
  working: boolean
  onSearchChange: (value: string) => void
  onPlatformFilterChange:
    (value: PlatformFilter) => void
  onStatusFilterChange:
    (value: StatusFilter) => void
  onToggleDevice: (deviceId: string) => void
  onToggleAllVisible: () => void
  onSelectWindows: () => void
  onSelectOnline: () => void
  onClearSelection: () => void
  onSynchronizeMembers: () => void
}

export function DeviceSelectionPanel({
  devices,
  selectedDeviceIds,
  selectedWindowsCount,
  selectedGroup,
  search,
  platformFilter,
  statusFilter,
  working,
  onSearchChange,
  onPlatformFilterChange,
  onStatusFilterChange,
  onToggleDevice,
  onToggleAllVisible,
  onSelectWindows,
  onSelectOnline,
  onClearSelection,
  onSynchronizeMembers,
}: DeviceSelectionPanelProps) {
  const allVisibleSelected =
    devices.length > 0
    && devices.every(device =>
      selectedDeviceIds.has(device.id),
    )

  return (
    <section className="fleet-devices">
      <div className="fleet-devices-header">
        <div>
          <strong>
            Selección de dispositivos
          </strong>

          <span>
            {selectedDeviceIds.size}{' '}
            seleccionado(s)
            {' · '}
            {selectedWindowsCount}{' '}
            Windows
          </span>
        </div>

        <div className="fleet-search">
          <Search size={16} />

          <input
            value={search}
            onChange={event =>
              onSearchChange(
                event.target.value,
              )
            }
            placeholder="Buscar dispositivo..."
          />
        </div>
      </div>

      <div className="fleet-form-grid">
        <label>
          Plataforma

          <select
            value={platformFilter}
            onChange={event =>
              onPlatformFilterChange(
                event.target.value as PlatformFilter,
              )
            }
          >
            <option value="All">
              Todas
            </option>

            <option value="Windows">
              Windows
            </option>

            <option value="Android">
              Android
            </option>
          </select>
        </label>

        <label>
          Estado

          <select
            value={statusFilter}
            onChange={event =>
              onStatusFilterChange(
                event.target.value as StatusFilter,
              )
            }
          >
            <option value="All">
              Todos
            </option>

            <option value="Online">
              Online
            </option>

            <option value="Offline">
              Offline
            </option>

            <option value="Quarantined">
              Quarantined
            </option>
          </select>
        </label>
      </div>

      <div className="fleet-members-action">
        <button
          type="button"
          className="fleet-secondary"
          onClick={onSelectWindows}
        >
          <Laptop size={16} />
          Seleccionar Windows
        </button>

        <button
          type="button"
          className="fleet-secondary"
          onClick={onSelectOnline}
        >
          <Wifi size={16} />
          Seleccionar Online
        </button>

        <button
          type="button"
          className="fleet-secondary"
          onClick={onClearSelection}
        >
          <X size={16} />
          Limpiar
        </button>
      </div>

      <div className="fleet-table-wrapper">
        <table className="fleet-table">
          <thead>
            <tr>
              <th>
                <input
                  type="checkbox"
                  checked={allVisibleSelected}
                  onChange={onToggleAllVisible}
                />
              </th>

              <th>DISPOSITIVO</th>
              <th>PLATAFORMA</th>
              <th>ESTADO</th>
              <th>CUMPLIMIENTO</th>
              <th>USUARIO</th>
            </tr>
          </thead>

          <tbody>
            {devices.map(device => (
              <tr key={device.id}>
                <td>
                  <input
                    type="checkbox"
                    checked={
                      selectedDeviceIds.has(
                        device.id,
                      )
                    }
                    onChange={() =>
                      onToggleDevice(device.id)
                    }
                  />
                </td>

                <td>
                  <div className="fleet-device">
                    {device.platform ===
                    'Android' ? (
                      <Smartphone size={17} />
                    ) : (
                      <Laptop size={17} />
                    )}

                    <div>
                      <strong>
                        {device.deviceName}
                      </strong>

                      <span>
                        SN:{' '}
                        {device.serialNumber}
                      </span>
                    </div>
                  </div>
                </td>

                <td>{device.platform}</td>

                <td>
                  <span className="fleet-status">
                    {device.status ===
                    'Online' ? (
                      <Wifi size={13} />
                    ) : (
                      <WifiOff size={13} />
                    )}

                    {device.status}
                  </span>
                </td>

                <td>
                  {device.complianceStatus}
                </td>

                <td>
                  {device.assignedUser
                    ?? 'Sin asignar'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selectedGroup
        && !selectedGroup.isDynamic && (
        <div className="fleet-members-action">
          <button
            type="button"
            className="fleet-secondary"
            disabled={working}
            onClick={onSynchronizeMembers}
          >
            <UserPlus size={16} />
            Guardar miembros
          </button>
        </div>
      )}
    </section>
  )
}
