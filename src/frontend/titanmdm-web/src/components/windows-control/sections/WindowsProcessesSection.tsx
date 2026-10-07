import {
  Cpu,
} from 'lucide-react'

import type {
  Dispatch,
  SetStateAction,
} from 'react'

import type {
  SendWindowsCommand,
  WindowsProcessItem,
} from '../windowsControl.types'

import {
  formatBytes,
  formatDate,
} from '../windowsControl.utils'

interface Props {
  processes:
    WindowsProcessItem[]

  filter: string

  setFilter:
    Dispatch<
      SetStateAction<string>
    >

  sendCommand:
    SendWindowsCommand
}

export function WindowsProcessesSection({
  processes,
  filter,
  setFilter,
  sendCommand,
}: Props) {
  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <Cpu size={18} />

          <h2>
            Procesos activos
          </h2>

          <div className="windows-control-inline-actions">
            <input
              className="windows-control-filter"
              type="search"
              placeholder="Buscar proceso..."
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
                  'PROCESS_INVENTORY',
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
                <th>Proceso</th>
                <th>PID</th>
                <th>RAM</th>
                <th>Inicio</th>
                <th>Acción</th>
              </tr>
            </thead>

            <tbody>
              {processes.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    No existen resultados.
                  </td>
                </tr>
              ) : (
                processes.map(
                  process => (
                    <tr key={process.processId}>
                      <td>
                        <strong>
                          {process.name}
                        </strong>
                      </td>

                      <td>
                        {process.processId}
                      </td>

                      <td>
                        {formatBytes(
                          process.workingSetBytes,
                        )}
                      </td>

                      <td>
                        {formatDate(
                          process.startTimeUtc,
                        )}
                      </td>

                      <td>
                        <button
                          type="button"
                          className="windows-row-danger"
                          disabled={
                            process.processId <= 4
                          }
                          onClick={() =>
                            void sendCommand(
                              'PROCESS_TERMINATE',
                              {
                                processId:
                                  process.processId,
                              },
                            )
                          }
                        >
                          Terminar
                        </button>
                      </td>
                    </tr>
                  ),
                )
              )}
            </tbody>
          </table>
        </div>
      </article>
    </section>
  )
}