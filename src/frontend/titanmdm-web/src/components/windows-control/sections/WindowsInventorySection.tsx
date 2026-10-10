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
  formatBytes,
  formatDate,
  latestByType,
  parseInventory,
  prettyResult,
} from '../windowsControl.utils'

interface Props {
  commands:
    DeviceCommand[]

  sendCommand:
    SendWindowsCommand
}

interface Attribute {
  label: string
  value:
    string
    | number
    | null
    | undefined
}

function deviceAttributes(
  inventory: ReturnType<
    typeof parseInventory
  >,
): Attribute[] {
  const device =
    inventory.device

  if (!device) {
    return []
  }

  return [
    {
      label: 'Nombre del equipo',
      value: device.computerName,
    },
    {
      label: 'Usuario',
      value: device.userName,
    },
    {
      label: 'Dominio',
      value: device.domainName,
    },
    {
      label: 'Fabricante',
      value: device.manufacturer,
    },
    {
      label: 'Modelo',
      value: device.model,
    },
    {
      label: 'Número de serie',
      value: device.serialNumber,
    },
    {
      label: 'Procesador',
      value: device.cpuName,
    },
    {
      label: 'Memoria RAM',
      value: device.totalMemoryBytes
        ? formatBytes(
            device.totalMemoryBytes,
          )
        : null,
    },
    {
      label: 'Edición de Windows',
      value: device.productName,
    },
    {
      label: 'Versión',
      value: device.operatingSystemVersion,
    },
    {
      label: 'Build',
      value: device.currentBuild,
    },
    {
      label: 'Versión publicada',
      value: device.displayVersion,
    },
    {
      label: 'Arquitectura',
      value: device.osArchitecture,
    },
    {
      label: 'Agente instalado',
      value: device.agentVersion,
    },
    {
      label: 'Instalado el',
      value: device.installDateUtc
        ? formatDate(
            device.installDateUtc,
          )
        : null,
    },
    {
      label: 'Disco del sistema',
      value: device.systemDrive,
    },
    {
      label: 'Espacio libre en sistema',
      value: device.systemDriveFreeBytes
        ? formatBytes(
            device.systemDriveFreeBytes,
          )
        : null,
    },
  ]
}

export function WindowsInventorySection({
  commands,
  sendCommand,
}: Props) {
  const inventory = parseInventory(
    latestByType(
      commands,
      'DEVICE_INVENTORY',
    ),
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
          <>
            {inventory.device && (
              <div className="windows-control-fields">
                {deviceAttributes(
                  inventory,
                ).map(
                  attribute => (
                    <div key={attribute.label}>
                      <span>
                        {attribute.label}
                      </span>

                      <strong>
                        {attribute.value
                          ?? 'N/D'}
                      </strong>
                    </div>
                  ),
                )}
              </div>
            )}

            {inventory.disks.length > 0 && (
              <div className="windows-control-table-wrapper">
                <table className="windows-control-table">
                  <thead>
                    <tr>
                      <th>
                        Disco
                      </th>

                      <th>
                        Etiqueta
                      </th>

                      <th>
                        Sistema de archivos
                      </th>

                      <th>
                        Capacidad total
                      </th>

                      <th>
                        Espacio libre
                      </th>
                    </tr>
                  </thead>

                  <tbody>
                    {inventory.disks.map(
                      disk => (
                        <tr key={disk.name}>
                          <td>
                            <strong>
                              {disk.name}
                            </strong>
                          </td>

                          <td>
                            {disk.volumeLabel
                              ?? 'N/D'}
                          </td>

                          <td>
                            {disk.fileSystem
                              ?? 'N/D'}
                          </td>

                          <td>
                            {formatBytes(
                              disk.totalBytes,
                            )}
                          </td>

                          <td>
                            {formatBytes(
                              disk.freeBytes,
                            )}
                          </td>
                        </tr>
                      ),
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {inventory.network.length > 0 && (
              <div className="windows-control-table-wrapper">
                <table className="windows-control-table">
                  <thead>
                    <tr>
                      <th>
                        Adaptador
                      </th>

                      <th>
                        Tipo
                      </th>

                      <th>
                        Estado
                      </th>

                      <th>
                        MAC
                      </th>

                      <th>
                        Direcciones IP
                      </th>
                    </tr>
                  </thead>

                  <tbody>
                    {inventory.network.map(
                      adapter => (
                        <tr key={adapter.macAddress ?? adapter.name}>
                          <td>
                            <strong>
                              {adapter.name}
                            </strong>
                          </td>

                          <td>
                            {adapter.interfaceType
                              ?? 'N/D'}
                          </td>

                          <td>
                            {adapter.operationalStatus
                              ?? 'N/D'}
                          </td>

                          <td>
                            {adapter.macAddress
                              ?? 'N/D'}
                          </td>

                          <td>
                            {
                              adapter
                                .ipAddresses
                                .length
                              > 0
                                ? adapter
                                    .ipAddresses
                                    .join(', ')
                                : 'Sin dirección'
                            }
                          </td>
                        </tr>
                      ),
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {inventory.collectedAtUtc && (
              <div className="windows-command-item__meta">
                <span>
                  Recopilado:{' '}
                  {formatDate(
                    inventory.collectedAtUtc,
                  )}
                </span>
              </div>
            )}

            <details className="windows-command-details">
              <summary>
                Ver detalle técnico
              </summary>

              <pre className="windows-control-json">
                {prettyResult(
                  latestByType(
                    commands,
                    'DEVICE_INVENTORY',
                  ),
                )}
              </pre>
            </details>
          </>
        )}
      </article>
    </section>
  )
}