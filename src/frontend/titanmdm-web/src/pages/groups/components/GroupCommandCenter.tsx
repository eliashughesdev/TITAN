import {
  AlertTriangle,
  LoaderCircle,
  Send,
  ShieldCheck,
  Trash2,
} from 'lucide-react'

import type {
  DeviceGroupDetails,
} from '../../../api/deviceGroupsApi'

import {
  COMMAND_OPTIONS,
} from '../deviceGroups.constants'

interface GroupCommandCenterProps {
  group: DeviceGroupDetails
  commandType: string
  working: boolean

  onCommandTypeChange:
    (value: string) => void

  onExecute:
    () => void

  onDelete:
    () => void
}

export function GroupCommandCenter({
  group,
  commandType,
  working,
  onCommandTypeChange,
  onExecute,
  onDelete,
}: GroupCommandCenterProps) {
  const command =
    COMMAND_OPTIONS.find(
      option =>
        option.value ===
        commandType,
    )

  const totalTargets =
    group.members.length

  const windowsTargets =
    group.members.filter(
      member =>
        member.platform ===
        'Windows',
    ).length

  const incompatibleTargets =
    command?.windowsOnly
      ?
        totalTargets -
        windowsTargets
      :
        0

  const hasTargets =
    totalTargets >
    0

  const isCompatible =
    incompatibleTargets ===
    0

  const canExecute =
    hasTargets
    &&
    isCompatible
    &&
    !working

  return (
    <section className="fleet-command-center">
      <div className="fleet-command-info">
        <span>
          OPERACIONES MASIVAS
        </span>

        <strong>
          Command Center
        </strong>

        <small>
          {totalTargets}{' '}
          destino(s)
          {' · '}
          {windowsTargets}{' '}
          Windows
        </small>
      </div>

      <div className="fleet-command-select">
        <label htmlFor="group-command">
          Acción
        </label>

        <select
          id="group-command"
          value={commandType}
          disabled={working}
          onChange={event =>
            onCommandTypeChange(
              event.target.value,
            )
          }
        >
          {COMMAND_OPTIONS.map(
            option => (
              <option
                key={
                  option.value
                }
                value={
                  option.value
                }
              >
                {
                  option.label
                }

                {
                  option.windowsOnly
                    ?
                      ' · Windows'
                    :
                      ''
                }
              </option>
            ),
          )}
        </select>
      </div>

      <div className="fleet-command-validation">
        {!hasTargets ? (
          <span className="fleet-command-warning">
            <AlertTriangle
              size={15}
            />

            Grupo sin dispositivos
          </span>
        ) : !isCompatible ? (
          <span className="fleet-command-warning">
            <AlertTriangle
              size={15}
            />

            {
              incompatibleTargets
            }{' '}
            destino(s) incompatible(s)
          </span>
        ) : (
          <span className="fleet-command-ready">
            <ShieldCheck
              size={15}
            />

            Listo para ejecutar
          </span>
        )}
      </div>

      <div className="fleet-command-actions">
        <button
          type="button"
          className="fleet-primary"
          disabled={
            !canExecute
          }
          onClick={
            onExecute
          }
        >
          {working ? (
            <LoaderCircle
              size={16}
              className="fleet-spin"
            />
          ) : (
            <Send
              size={16}
            />
          )}

          {working
            ?
              'Ejecutando...'
            :
              `Ejecutar en ${totalTargets}`}
        </button>

        <button
          type="button"
          className="fleet-danger"
          disabled={working}
          onClick={
            onDelete
          }
        >
          <Trash2
            size={16}
          />

          Eliminar grupo
        </button>
      </div>
    </section>
  )
}