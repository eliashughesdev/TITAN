import {
  ServerCog,
} from 'lucide-react'

import type {
  Dispatch,
  SetStateAction,
} from 'react'

import type {
  SendWindowsCommand,
  WindowsServiceItem,
} from '../windowsControl.types'

interface Props {
  services:
    WindowsServiceItem[]

  filter: string

  setFilter:
    Dispatch<
      SetStateAction<string>
    >

  sendCommand:
    SendWindowsCommand
}

export function WindowsServicesSection({
  services,
  filter,
  setFilter,
  sendCommand,
}: Props) {
  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <ServerCog size={18} />

          <h2>
            Servicios Windows
          </h2>

          <div className="windows-control-inline-actions">
            <input
              className="windows-control-filter"
              type="search"
              placeholder="Buscar servicio..."
              value={filter}
              onChange={
                event =>
                  setFilter(
                    event.target.value,
                  )
              }
            />

            <button
              type="button"
              onClick={() =>
                void sendCommand(
                  'SERVICE_INVENTORY',
                )
              }
            >
              Actualizar
            </button>
          </div>
        </header>

        <div className="windows-control-table-wrapper">
          <table className="windows-control-table">
            <thead>
              <tr>
                <th>Servicio</th>
                <th>Display Name</th>
                <th>Inicio</th>
                <th>Tipo</th>
                <th>Acciones</th>
              </tr>
            </thead>

            <tbody>
              {services.map(
                service => (
                  <tr key={service.name}>
                    <td>
                      <strong>
                        {service.name}
                      </strong>
                    </td>

                    <td>
                      {service.displayName ?? 'N/D'}
                    </td>

                    <td>
                      {service.startType ?? 'N/D'}
                    </td>

                    <td>
                      {service.serviceType ?? 'N/D'}
                    </td>

                    <td>
                      <div className="windows-row-actions">
                        <button
                          type="button"
                          onClick={() =>
                            void sendCommand(
                              'SERVICE_START',
                              {
                                serviceName:
                                  service.name,
                              },
                            )
                          }
                        >
                          Iniciar
                        </button>

                        <button
                          type="button"
                          onClick={() =>
                            void sendCommand(
                              'SERVICE_STOP',
                              {
                                serviceName:
                                  service.name,
                              },
                            )
                          }
                        >
                          Detener
                        </button>

                        <button
                          type="button"
                          onClick={() =>
                            void sendCommand(
                              'SERVICE_RESTART',
                              {
                                serviceName:
                                  service.name,
                              },
                            )
                          }
                        >
                          Reiniciar
                        </button>
                      </div>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      </article>
    </section>
  )
}