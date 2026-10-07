import {
  Info,
  RotateCw,
} from 'lucide-react'

import type {
  DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  formatDeviceDate,
  getCommandStatusClass,
} from '../utils/deviceDetail.utils'

interface Props {
  commands:
    DeviceCommand[]

  isAndroid:
    boolean

  onRefresh:
    () => void
}

export function DeviceCommandsPanel({
  commands,
  isAndroid,
  onRefresh,
}: Props) {
  return (
    <section className="device-detail-card device-detail-card--wide">
      <header className="device-command-header">
        <div>
          <h2>
            Historial de comandos
          </h2>

          <p>
            Seguimiento de acciones
            enviadas al dispositivo.
          </p>
        </div>

        <button
          type="button"
          className="device-action-secondary"
          onClick={onRefresh}
        >
          <RotateCw
            size={15}
          />

          Actualizar
        </button>
      </header>

      {isAndroid && (
        <div className="device-detail-notice">
          <Info
            size={17}
          />

          <span>
            Motor TitanMDM Agent activo.
            PING y DEVICE_INFO se ejecutan
            directamente mediante el agente
            Android.
          </span>
        </div>
      )}

      <div className="device-command-table-wrapper">
        <table className="device-command-table">
          <thead>
            <tr>
              <th>
                Comando
              </th>

              <th>
                Estado
              </th>

              <th>
                Creado
              </th>

              <th>
                Finalizado
              </th>

              <th>
                Intentos
              </th>

              <th>
                Resultado
              </th>
            </tr>
          </thead>

          <tbody>
            {commands.length ===
            0 ? (
              <tr>
                <td
                  colSpan={6}
                  className="device-command-empty"
                >
                  No se han enviado
                  comandos a este
                  dispositivo.
                </td>
              </tr>
            ) : (
              commands.map(
                command => (
                  <tr
                    key={
                      command.id
                    }
                  >
                    <td>
                      <strong>
                        {
                          command
                            .commandType
                        }
                      </strong>
                    </td>

                    <td>
                      <span
                        className={
                          `command-status ` +
                          `command-status--${getCommandStatusClass(
                            command.status,
                          )}`
                        }
                      >
                        {
                          command
                            .status
                        }
                      </span>
                    </td>

                    <td>
                      {formatDeviceDate(
                        command
                          .createdAtUtc,
                      )}
                    </td>

                    <td>
                      {formatDeviceDate(
                        command
                          .completedAtUtc,
                      )}
                    </td>

                    <td>
                      {
                        command
                          .deliveryAttempts
                      }
                    </td>

                    <td className="command-result">
                      {command
                        .errorMessage
                      ??
                      command
                        .resultJson
                      ??
                      '—'}
                    </td>
                  </tr>
                ),
              )
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}