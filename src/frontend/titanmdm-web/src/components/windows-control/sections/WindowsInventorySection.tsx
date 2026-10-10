import {
  Boxes,
} from 'lucide-react'

import type {
  DeviceCommand,
} from '../../../api/deviceCommandsApi'

import type {
  SendWindowsCommand,
} from '../windowsControl.types'

import {
  latestByType,
  parseInventory,
} from '../windowsControl.utils'

import {
  InventoryResult,
} from '../InventoryResult'

interface Props {
  commands:
    DeviceCommand[]

  sendCommand:
    SendWindowsCommand
}

export function WindowsInventorySection({
  commands,
  sendCommand,
}: Props) {
  const latest =
    latestByType(
      commands,
      'DEVICE_INVENTORY',
    )

  const inventory =
    parseInventory(
      latest,
    )

  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <Boxes size={18} />

          <h2>
            Inventario completo
          </h2>

          <button
            type="button"
            onClick={() =>
              void sendCommand(
                'DEVICE_INVENTORY',
              )
            }
          >
            Actualizar
          </button>
        </header>

        {!inventory.available ? (
          <p className="windows-control-fields">
            Todavía no existe información para esta consulta. Presiona
            {' '}
            <strong>
              Actualizar
            </strong>
            {' '}
            para recopilar el inventario del equipo.
          </p>
        ) : (
          <InventoryResult
            inventory={inventory}
            technical={latest}
          />
        )}
      </article>
    </section>
  )
}