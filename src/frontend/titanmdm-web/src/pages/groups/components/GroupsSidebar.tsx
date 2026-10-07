import {
  ChevronRight,
  UsersRound,
} from 'lucide-react'

import type {
  DeviceGroup,
} from '../../../api/deviceGroupsApi'

interface GroupsSidebarProps {
  groups: DeviceGroup[]
  selectedGroupId: string | null
  onOpenGroup: (groupId: string) => void
}

export function GroupsSidebar({
  groups,
  selectedGroupId,
  onOpenGroup,
}: GroupsSidebarProps) {
  return (
    <aside className="fleet-groups">
      <div className="fleet-panel-title">
        <div>
          <UsersRound size={18} />
          <strong>Grupos</strong>
        </div>

        <span>{groups.length}</span>
      </div>

      {groups.length === 0 ? (
        <div className="fleet-empty">
          No existen grupos.
        </div>
      ) : (
        groups.map(group => (
          <button
            type="button"
            key={group.id}
            className={
              selectedGroupId === group.id
                ? 'fleet-group selected'
                : 'fleet-group'
            }
            onClick={() =>
              onOpenGroup(group.id)
            }
          >
            <div>
              <strong>{group.name}</strong>

              <span>
                {group.isDynamic
                  ? 'Dinámico'
                  : 'Estático'}
                {' · '}
                {group.deviceCount}{' '}
                dispositivo(s)
              </span>
            </div>

            <ChevronRight size={16} />
          </button>
        ))
      )}
    </aside>
  )
}
