import {
  Cpu,
  Database,
  HardDrive,
  Monitor,
  Network,
  Server,
  UserRound,
} from 'lucide-react'

import type {
  DeviceCommandSnapshot,
} from '../../../../types/device'

import {
  formatBytes,
  formatDate,
  formatSpeed,
  getInventoryDevice,
  getNetworkAdapters,
  getStorageUsage,
  parseJson,
} from './deviceOperational.utils'

interface Props {
  inventory:
    DeviceCommandSnapshot | undefined

  network:
    DeviceCommandSnapshot | undefined
}

function Value({
  label,
  value,
}: {
  label: string
  value:
    string |
    number |
    null |
    undefined
}) {
  return (
    <div className="op-value">
      <span>
        {label}
      </span>

      <strong>
        {value === null
        ||
        value === undefined
        ||
        value === ''
          ? 'N/D'
          : String(value)}
      </strong>
    </div>
  )
}

export function DeviceInventoryView({
  inventory,
  network,
}: Props) {
  const inventoryRaw =
    parseJson(
      inventory?.resultJson,
    )

  const networkRaw =
    parseJson(
      network?.resultJson,
    )

  const device =
    getInventoryDevice(
      inventoryRaw,
    )

  const adapters =
    getNetworkAdapters(
      networkRaw,
    ).length > 0
      ? getNetworkAdapters(
          networkRaw,
        )
      : getNetworkAdapters(
          inventoryRaw,
        )

  if (!device) {
    return (
      <div className="op-empty-state">
        <HardDrive
          size={28}
        />

        <strong>
          Inventario no disponible
        </strong>

        <span>
          Ejecuta “Actualizar inventario”
          para obtener información del
          endpoint.
        </span>
      </div>
    )
  }

  const storage =
    getStorageUsage(
      device.systemDriveTotalBytes,
      device.systemDriveFreeBytes,
    )

  return (
    <div className="op-inventory-view">
      <div className="op-section-title">
        <div>
          <span>
            INVENTARIO WINDOWS
          </span>

          <h3>
            Resumen del endpoint
          </h3>
        </div>

        <small>
          Actualizado{' '}
          {formatDate(
            inventory
              ?.completedAtUtc
            ??
            inventory
              ?.createdAtUtc,
          )}
        </small>
      </div>

      <div className="op-kpi-grid">
        <article>
          <Monitor
            size={20}
          />

          <span>
            Equipo
          </span>

          <strong>
            {device.computerName
            ||
            'N/D'}
          </strong>
        </article>

        <article>
          <UserRound
            size={20}
          />

          <span>
            Usuario
          </span>

          <strong>
            {device.domainName
              ? `${device.domainName}\\${device.userName}`
              : device.userName
                ||
                'N/D'}
          </strong>
        </article>

        <article>
          <Cpu
            size={20}
          />

          <span>
            Procesadores lógicos
          </span>

          <strong>
            {device.processorCount
            ??
            'N/D'}
          </strong>
        </article>

        <article>
          <Server
            size={20}
          />

          <span>
            Agente
          </span>

          <strong>
            {device.agentVersion
            ||
            'N/D'}
          </strong>
        </article>
      </div>

      <div className="op-two-columns">
        <article className="op-panel">
          <header>
            <Monitor
              size={18}
            />

            <div>
              <strong>
                Sistema operativo
              </strong>

              <span>
                Información de Windows
              </span>
            </div>
          </header>

          <div className="op-values-grid">
            <Value
              label="Producto"
              value={
                device.productName
                ||
                device.operatingSystem
              }
            />

            <Value
              label="Versión"
              value={
                device.displayVersion
                ||
                device
                  .operatingSystemVersion
              }
            />

            <Value
              label="Build"
              value={
                device.currentBuild
              }
            />

            <Value
              label="Arquitectura"
              value={
                device.osArchitecture
              }
            />

            <Value
              label="Arquitectura proceso"
              value={
                device.processArchitecture
              }
            />

            <Value
              label=".NET"
              value={
                device.framework
              }
            />

            <Value
              label="Instalación"
              value={
                formatDate(
                  device.installDateUtc,
                )
              }
            />

            <Value
              label="64 bits"
              value={
                device
                  .is64BitOperatingSystem
                === null
                  ? 'N/D'
                  : device
                      .is64BitOperatingSystem
                    ? 'Sí'
                    : 'No'
              }
            />
          </div>
        </article>

        <article className="op-panel">
          <header>
            <Database
              size={18}
            />

            <div>
              <strong>
                Almacenamiento
              </strong>

              <span>
                Disco del sistema
              </span>
            </div>
          </header>

          <div className="op-storage">
            <div
              className="op-storage-donut"
              style={{
                '--storage-percent':
                  `${storage.percent}%`,
              } as React.CSSProperties}
            >
              <strong>
                {storage.percent}%
              </strong>

              <span>
                usado
              </span>
            </div>

            <div className="op-storage-detail">
              <strong>
                {device.systemDrive
                ||
                'Disco sistema'}
              </strong>

              <div className="op-storage-bar">
                <span
                  style={{
                    width:
                      `${storage.percent}%`,
                  }}
                />
              </div>

              <div>
                <span>
                  Usado
                </span>

                <strong>
                  {formatBytes(
                    storage.used,
                  )}
                </strong>
              </div>

              <div>
                <span>
                  Libre
                </span>

                <strong>
                  {formatBytes(
                    device
                      .systemDriveFreeBytes,
                  )}
                </strong>
              </div>

              <div>
                <span>
                  Total
                </span>

                <strong>
                  {formatBytes(
                    device
                      .systemDriveTotalBytes,
                  )}
                </strong>
              </div>
            </div>
          </div>
        </article>
      </div>

      <article className="op-panel">
        <header>
          <Network
            size={18}
          />

          <div>
            <strong>
              Interfaces de red
            </strong>

            <span>
              Adaptadores detectados
            </span>
          </div>
        </header>

        {adapters.length ===
        0 ? (
          <div className="op-panel-empty">
            Sin información de red.
          </div>
        ) : (
          <div className="op-network-grid">
            {adapters.map(
              (
                adapter,
                index,
              ) => (
                <div
                  className="op-network-card"
                  key={
                    `${adapter.name}-${index}`
                  }
                >
                  <div className="op-network-heading">
                    <div>
                      <strong>
                        {adapter.name}
                      </strong>

                      <span>
                        {adapter.description}
                      </span>
                    </div>

                    <span
                      className={
                        adapter
                          .operationalStatus
                          .toLowerCase()
                        ===
                        'up'
                          ? 'op-badge success'
                          : 'op-badge neutral'
                      }
                    >
                      {adapter
                        .operationalStatus
                      ||
                      'Unknown'}
                    </span>
                  </div>

                  <div className="op-values-grid">
                    <Value
                      label="Tipo"
                      value={
                        adapter
                          .interfaceType
                      }
                    />

                    <Value
                      label="MAC"
                      value={
                        adapter.macAddress
                      }
                    />

                    <Value
                      label="Velocidad"
                      value={
                        formatSpeed(
                          adapter.speed,
                        )
                      }
                    />

                    <Value
                      label="IP"
                      value={
                        adapter
                          .ipAddresses
                          .join(', ')
                      }
                    />

                    <Value
                      label="Gateway"
                      value={
                        adapter
                          .gateways
                          .join(', ')
                      }
                    />

                    <Value
                      label="DNS"
                      value={
                        adapter
                          .dnsServers
                          .join(', ')
                      }
                    />
                  </div>
                </div>
              ),
            )}
          </div>
        )}
      </article>

      <details className="op-technical-details">
        <summary>
          Ver información técnica
        </summary>

        <pre>
          {JSON.stringify(
            inventoryRaw,
            null,
            2,
          )}
        </pre>
      </details>
    </div>
  )
}