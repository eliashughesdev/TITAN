import {
  CheckCircle2,
  RefreshCw,
} from 'lucide-react'

import {
  DeviceSelectionPanel,
} from './components/DeviceSelectionPanel'

import {
  FleetStats,
} from './components/FleetStats'

import {
  GroupBuilder,
} from './components/GroupBuilder'

import {
  GroupCommandCenter,
} from './components/GroupCommandCenter'

import {
  GroupsSidebar,
} from './components/GroupsSidebar'

import {
  SelectedGroupPanel,
} from './components/SelectedGroupPanel'

import {
  useDeviceGroupsController,
} from './hooks/useDeviceGroupsController'

import './DeviceGroupsPage.css'

export function DeviceGroupsPage() {
  const controller =
    useDeviceGroupsController()

  const {
    groups,
    devices,
    selectedGroup,
    selectedDeviceIds,
    filteredDevices,

    search,
    platformFilter,
    statusFilter,

    name,
    description,
    mode,
    dynamicPlatform,
    dynamicStatus,

    editName,
    editDescription,
    editDynamicPlatform,
    editDynamicStatus,
    editingGroup,

    commandType,

    loading,
    working,

    message,
    error,

    selectedWindowsCount,
    stats,

    setSearch,
    setPlatformFilter,
    setStatusFilter,

    setName,
    setDescription,
    setMode,
    setDynamicPlatform,
    setDynamicStatus,

    setEditName,
    setEditDescription,
    setEditDynamicPlatform,
    setEditDynamicStatus,
    setEditingGroup,

    setCommandType,

    loadBase,
    openGroup,
    closeGroup,
    createGroup,
    updateGroup,
    synchronizeMembers,
    sendGroupCommand,
    deleteGroup,

    toggleDevice,
    toggleAllVisible,
    selectWindows,
    selectOnline,
    clearSelection,
  } =
    controller

  return (
    <div className="fleet-page">
      <header className="fleet-header">
        <div>
          <span className="fleet-eyebrow">
            TITANMDM FLEET MANAGEMENT
          </span>

          <h1>
            Grupos y Flota
          </h1>

          <p>
            Organiza dispositivos,
            filtra la flota y ejecuta
            operaciones masivas.
          </p>
        </div>

        <button
          type="button"
          className="fleet-secondary"
          onClick={() =>
            void loadBase()
          }
          disabled={
            loading ||
            working
          }
        >
          <RefreshCw
            size={16}
            className={
              loading
                ?
                  'fleet-spin'
                :
                  undefined
            }
          />

          {loading
            ?
              'Actualizando...'
            :
              'Actualizar'}
        </button>
      </header>

      {message && (
        <div className="fleet-message success">
          <CheckCircle2
            size={17}
          />

          {message}
        </div>
      )}

      {error && (
        <div className="fleet-message error">
          {error}
        </div>
      )}

      <FleetStats
        groupCount={
          groups.length
        }
        deviceCount={
          devices.length
        }
        windowsDevices={
          stats.windowsDevices
        }
        androidDevices={
          stats.androidDevices
        }
        onlineDevices={
          stats.onlineDevices
        }
        compliantDevices={
          stats.compliantDevices
        }
        totalMembers={
          stats.totalMembers
        }
      />

      <div className="fleet-layout">
        <GroupsSidebar
          groups={
            groups
          }
          selectedGroupId={
            selectedGroup
              ?.id
            ??
            null
          }
          onOpenGroup={
            groupId =>
              void openGroup(
                groupId,
              )
          }
        />

        <main className="fleet-main">
          <GroupBuilder
            name={
              name
            }
            description={
              description
            }
            mode={
              mode
            }
            dynamicPlatform={
              dynamicPlatform
            }
            dynamicStatus={
              dynamicStatus
            }
            working={
              working
            }
            onNameChange={
              setName
            }
            onDescriptionChange={
              setDescription
            }
            onModeChange={
              setMode
            }
            onDynamicPlatformChange={
              setDynamicPlatform
            }
            onDynamicStatusChange={
              setDynamicStatus
            }
            onCreate={() =>
              void createGroup()
            }
          />

          {selectedGroup && (
            <SelectedGroupPanel
              group={
                selectedGroup
              }

              editing={
                editingGroup
              }

              editName={
                editName
              }

              editDescription={
                editDescription
              }

              editDynamicPlatform={
                editDynamicPlatform
              }

              editDynamicStatus={
                editDynamicStatus
              }

              working={
                working
              }

              onEditingChange={
                setEditingGroup
              }

              onEditNameChange={
                setEditName
              }

              onEditDescriptionChange={
                setEditDescription
              }

              onEditDynamicPlatformChange={
                setEditDynamicPlatform
              }

              onEditDynamicStatusChange={
                setEditDynamicStatus
              }

              onSave={() =>
                void updateGroup()
              }

              onClose={
                closeGroup
              }
            />
          )}

          <DeviceSelectionPanel
            devices={
              filteredDevices
            }

            selectedDeviceIds={
              selectedDeviceIds
            }

            selectedWindowsCount={
              selectedWindowsCount
            }

            selectedGroup={
              selectedGroup
            }

            search={
              search
            }

            platformFilter={
              platformFilter
            }

            statusFilter={
              statusFilter
            }

            working={
              working
            }

            onSearchChange={
              setSearch
            }

            onPlatformFilterChange={
              setPlatformFilter
            }

            onStatusFilterChange={
              setStatusFilter
            }

            onToggleDevice={
              toggleDevice
            }

            onToggleAllVisible={
              toggleAllVisible
            }

            onSelectWindows={
              selectWindows
            }

            onSelectOnline={
              selectOnline
            }

            onClearSelection={
              clearSelection
            }

            onSynchronizeMembers={() =>
              void synchronizeMembers()
            }
          />

          {selectedGroup && (
            <GroupCommandCenter
              group={
                selectedGroup
              }

              commandType={
                commandType
              }

              working={
                working
              }

              onCommandTypeChange={
                setCommandType
              }

              onExecute={() =>
                void sendGroupCommand()
              }

              onDelete={() =>
                void deleteGroup()
              }
            />
          )}
        </main>
      </div>
    </div>
  )
}