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
  prettyResult,
} from '../windowsControl.utils'

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

        <pre className="windows-control-json">
          {prettyResult(
            latestByType(
              commands,
              'DEVICE_INVENTORY',
            ),
          )}
        </pre>
      </article>
    </section>
  )
}