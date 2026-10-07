import {
  Laptop,
  Network,
  ScanLine,
} from 'lucide-react'

import type {
  DeviceDetails,
} from '../../../types/device'

import type {
  CommandDefinition,
  SendWindowsCommand,
} from '../windowsControl.types'

import {
  formatDate,
} from '../windowsControl.utils'

interface Props {
  device: DeviceDetails

  definitions:
    CommandDefinition[]

  sendingCommand:
    string | null

  sendCommand:
    SendWindowsCommand
}

export function WindowsOverviewSection({
  device,
  definitions,
  sendingCommand,
  sendCommand,
}: Props) {
  return (
    <section className="windows-control-grid">
      <article className="windows-control-card">
        <header>
          <Laptop size={18} />

          <h2>
            Endpoint
          </h2>
        </header>

        <div className="windows-control-fields">
          <div>
            <span>Equipo</span>
            <strong>{device.deviceName}</strong>
          </div>

          <div>
            <span>Sistema</span>
            <strong>
              {device.operatingSystem ?? 'N/D'}
            </strong>
          </div>

          <div>
            <span>Versión</span>
            <strong>
              {device.operatingSystemVersion ?? 'N/D'}
            </strong>
          </div>

          <div>
            <span>Agent</span>
            <strong>
              {device.agentVersion ?? 'N/D'}
            </strong>
          </div>

          <div>
            <span>IP</span>
            <strong>
              {device.ipAddress ?? 'N/D'}
            </strong>
          </div>

          <div>
            <span>Último heartbeat</span>

            <strong>
              {formatDate(
                device.lastSeenAtUtc,
              )}
            </strong>
          </div>
        </div>
      </article>

      <article className="windows-control-card">
        <header>
          <Network size={18} />

          <h2>
            Identidad
          </h2>
        </header>

        <div className="windows-control-fields">
          <div>
            <span>Serial</span>
            <strong>{device.serialNumber}</strong>
          </div>

          <div>
            <span>MAC</span>
            <strong>
              {device.macAddress ?? 'N/D'}
            </strong>
          </div>

          <div>
            <span>Usuario</span>
            <strong>
              {device.assignedUser ?? 'Sin asignar'}
            </strong>
          </div>

          <div>
            <span>Departamento</span>
            <strong>
              {device.department ?? 'Sin departamento'}
            </strong>
          </div>
        </div>
      </article>

      <article className="windows-control-card windows-control-card--wide">
        <header>
          <ScanLine size={18} />

          <h2>
            Recolección inmediata
          </h2>
        </header>

        <div className="windows-control-command-grid">
          {definitions.map(
            definition => (
              <button
                key={definition.type}
                type="button"
                disabled={
                  sendingCommand !== null
                }
                onClick={() =>
                  void sendCommand(
                    definition.type,
                  )
                }
              >
                {definition.icon}

                <div>
                  <strong>
                    {definition.label}
                  </strong>

                  <span>
                    {definition.description}
                  </span>
                </div>
              </button>
            ),
          )}
        </div>
      </article>
    </section>
  )
}