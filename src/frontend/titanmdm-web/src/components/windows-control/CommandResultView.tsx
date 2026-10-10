import {
  HardDrive,
  KeyRound,
  RadioTower,
  RefreshCw,
  Shield,
  ShieldCheck,
  Wrench,
} from 'lucide-react'

import type {
  DeviceCommand,
} from '../../api/deviceCommandsApi'

import {
  formatBytes,
  formatDate,
  normalizeApplications,
  normalizeProcesses,
  normalizeServices,
  parseInventory,
  parseSecurity,
  parseUpdate,
  prettyResult,
  safeParseJson,
  scalarFields,
} from './windowsControl.utils'

import {
  InventoryResult,
} from './InventoryResult'

import {
  WindowsTelemetryCard,
} from './sections/WindowsTelemetryCard'

import type {
  SecurityView,
  UpdateView,
  WindowsApplicationItem,
  WindowsProcessItem,
  WindowsServiceItem,
} from './windowsControl.types'

function humanize(
  key: string,
): string {
  return key
    .replace(
      /([a-z0-9])([A-Z])/g,
      '$1 $2',
    )
    .replace(
      /^([A-Z]+)([A-Z][a-z])/g,
      '$1 $2',
    )
    .replace(
      /_/g,
      ' ',
    )
    .replace(
      /\b\w/g,
      letter =>
        letter.toUpperCase(),
    )
}

function TechnicalDetail({
  command,
}: {
  command:
    DeviceCommand
}) {
  if (
    !command.resultJson
  ) {
    return null
  }

  return (
    <details className="windows-command-details">
      <summary>
        Ver detalle técnico
      </summary>

      <pre className="windows-control-json">
        {prettyResult(
          command,
        )}
      </pre>
    </details>
  )
}

function SecurityResult({
  command,
}: {
  command: DeviceCommand
}) {
  const security:
    SecurityView =
    parseSecurity(
      command,
    )

  if (
    !security.available
  ) {
    return (
      <p className="windows-control-fields">
        Sin información de seguridad.
      </p>
    )
  }

  return (
    <>
      <div className="windows-telemetry-grid">
        <WindowsTelemetryCard
          title="Microsoft Defender"
          value={
            security.defenderRealTime === true
              ? 'Protegido'
              : security.defenderAvailable
                ? 'Revisar'
                : 'No disponible'
          }
          detail={
            security.defenderVersion
              ? `Firmas ${security.defenderVersion}`
              : 'Protección en tiempo real'
          }
          icon={
            <ShieldCheck size={18} />
          }
          tone={
            security.defenderRealTime === true
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="Firewall"
          value={
            security.firewallEnabledProfiles !== null
              ? `${security.firewallEnabledProfiles} perfiles`
              : 'N/D'
          }
          detail="Perfiles de Windows Firewall"
          icon={<Shield size={18} />}
          tone={
            (
              security.firewallEnabledProfiles ??
              0
            ) > 0
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="BitLocker"
          value={
            security.bitLockerProtected === true
              ? 'Protegido'
              : security.bitLockerAvailable
                ? 'Revisar'
                : 'N/D'
          }
          detail="Protección de volúmenes"
          icon={<HardDrive size={18} />}
          tone={
            security.bitLockerProtected === true
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="TPM"
          value={
            security.tpmPresent === true
              ? security.tpmReady === true
                ? 'Listo'
                : 'Presente'
              : 'No disponible'
          }
          detail="Trusted Platform Module"
          icon={<KeyRound size={18} />}
          tone={
            security.tpmReady === true
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="Secure Boot"
          value={
            security.secureBoot === true
              ? 'Activo'
              : security.secureBoot === false
                ? 'Inactivo'
                : 'N/D'
          }
          detail="Arranque seguro UEFI"
          icon={<ShieldCheck size={18} />}
          tone={
            security.secureBoot === true
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="UAC"
          value={
            security.uacEnabled === true
              ? 'Activo'
              : 'Inactivo'
          }
          detail="User Account Control"
          icon={<Shield size={18} />}
          tone={
            security.uacEnabled === true
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="Reinicio pendiente"
          value={
            security.pendingReboot === true
              ? 'Sí'
              : 'No'
          }
          detail="Windows requiere reinicio"
          icon={<RefreshCw size={18} />}
          tone={
            security.pendingReboot === true
              ? 'warning'
              : 'success'
          }
        />

        <WindowsTelemetryCard
          title="Remote Desktop"
          value={
            security.remoteDesktopEnabled === true
              ? 'Habilitado'
              : 'Deshabilitado'
          }
          detail="Configuración RDP"
          icon={<RadioTower size={18} />}
          tone={
            security.remoteDesktopEnabled === true
              ? 'warning'
              : 'success'
          }
        />
      </div>

      <TechnicalDetail command={command} />
    </>
  )
}

function UpdateResult({
  command,
}: {
  command: DeviceCommand
}) {
  const update:
    UpdateView =
    parseUpdate(
      command,
    )

  const serviceReady =
    update.serviceStatus ===
      'Running'
    ||
    update.serviceStatus ===
      'Running (Auto)'

  return (
    <>
      <div className="windows-telemetry-grid">
        <WindowsTelemetryCard
          title="Servicio"
          value={
            update.serviceQuerySucceeded
              ? update.serviceStatus
              : 'Sin consulta'
          }
          detail="Estado del servicio Windows Update"
          icon={<Wrench size={18} />}
          tone={
            update.serviceQuerySucceeded &&
            serviceReady
              ? 'success'
              : 'warning'
          }
        />

        <WindowsTelemetryCard
          title="Disponibles"
          value={
            update.availableUpdatesAvailable
              ? String(
                  update
                    .availableUpdates
                    .length,
                )
              : 'N/D'
          }
          detail="Actualizaciones pendientes"
          icon={<RefreshCw size={18} />}
          tone={
            update.availableUpdatesAvailable &&
            update.availableUpdates.length > 0
              ? 'warning'
              : 'success'
          }
        />

        <WindowsTelemetryCard
          title="Reinicio"
          value={
            update.pendingReboot === true
              ? 'Pendiente'
              : 'No'
          }
          detail="Windows requiere reinicio"
          icon={<RefreshCw size={18} />}
          tone={
            update.pendingReboot === true
              ? 'warning'
              : 'success'
          }
        />

        <WindowsTelemetryCard
          title="Historial"
          value={
            update.historyAvailable
              ? String(
                  update
                    .history
                    .length,
                )
              : 'N/D'
          }
          detail="Entradas registradas"
          icon={<RefreshCw size={18} />}
          tone="unknown"
        />
      </div>

      {update.availableUpdates.length > 0 && (
        <div className="windows-control-table-wrapper">
          <table className="windows-control-table">
            <thead>
              <tr>
                <th>Actualización</th>
                <th>KB</th>
                <th>Severidad</th>
                <th>Reinicio</th>
              </tr>
            </thead>

            <tbody>
              {update.availableUpdates.map(
                (
                  item,
                  index,
                ) => (
                  <tr
                    key={`${item.title}-${index}`}
                  >
                    <td>
                      <strong>
                        {item.title}
                      </strong>
                    </td>

                    <td>
                      {item.kbArticleIds.length > 0
                        ? item.kbArticleIds
                            .map(
                              kb =>
                                `KB${kb}`,
                            )
                            .join(', ')
                        : 'N/D'}
                    </td>

                    <td>
                      {item.severity ?? 'N/D'}
                    </td>

                    <td>
                      {item.rebootRequired === true
                        ? 'Sí'
                        : 'No'}
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}

      {update.history.length > 0 && (
        <div className="windows-control-table-wrapper">
          <table className="windows-control-table">
            <thead>
              <tr>
                <th>Actualización</th>
                <th>Fecha</th>
                <th>Resultado</th>
              </tr>
            </thead>

            <tbody>
              {update.history.map(
                (
                  item,
                  index,
                ) => (
                  <tr
                    key={`${item.title}-${index}`}
                  >
                    <td>
                      <strong>
                        {item.title}
                      </strong>
                    </td>

                    <td>
                      {item.date
                        ? formatDate(
                            item.date,
                          )
                        : 'N/D'}
                    </td>

                    <td>
                      {item.hResult
                        ? `HRESULT ${item.hResult}`
                        : item.resultCode
                          ? `Código ${item.resultCode}`
                          : 'Sin detalle'}
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}

      <TechnicalDetail command={command} />
    </>
  )
}

function ProcessResult({
  command,
}: {
  command: DeviceCommand
}) {
  const processes:
    WindowsProcessItem[] =
    normalizeProcesses(
      command,
    )

  if (
    processes.length === 0
  ) {
    return (
      <p className="windows-control-fields">
        Sin procesos reportados.
      </p>
    )
  }

  return (
    <>
      <div className="windows-control-table-wrapper">
        <table className="windows-control-table">
          <thead>
            <tr>
              <th>Proceso</th>
              <th>PID</th>
              <th>RAM</th>
              <th>Inicio</th>
            </tr>
          </thead>

          <tbody>
            {processes.map(
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
                    {process.startTimeUtc
                      ? formatDate(
                          process.startTimeUtc,
                        )
                      : 'N/D'}
                  </td>
                </tr>
              ),
            )}
          </tbody>
        </table>
      </div>

      <TechnicalDetail command={command} />
    </>
  )
}

function ServiceResult({
  command,
}: {
  command: DeviceCommand
}) {
  const services:
    WindowsServiceItem[] =
    normalizeServices(
      command,
    )

  if (
    services.length === 0
  ) {
    return (
      <p className="windows-control-fields">
        Sin servicios reportados.
      </p>
    )
  }

  return (
    <>
      <div className="windows-control-table-wrapper">
        <table className="windows-control-table">
          <thead>
            <tr>
              <th>Servicio</th>
              <th>Display Name</th>
              <th>Inicio</th>
              <th>Tipo</th>
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
                </tr>
              ),
            )}
          </tbody>
        </table>
      </div>

      <TechnicalDetail command={command} />
    </>
  )
}

function ApplicationResult({
  command,
}: {
  command: DeviceCommand
}) {
  const applications:
    WindowsApplicationItem[] =
    normalizeApplications(
      command,
    )

  if (
    applications.length === 0
  ) {
    return (
      <p className="windows-control-fields">
        Sin aplicaciones reportadas.
      </p>
    )
  }

  return (
    <>
      <div className="windows-control-table-wrapper">
        <table className="windows-control-table">
          <thead>
            <tr>
              <th>Aplicación</th>
              <th>Versión</th>
              <th>Publisher</th>
            </tr>
          </thead>

          <tbody>
            {applications.map(
              (application, index) => (
                <tr key={`${application.name}-${index}`}>
                  <td>
                    <strong>
                      {application.name}
                    </strong>
                  </td>

                  <td>
                    {application.version ?? 'N/D'}
                  </td>

                  <td>
                    {application.publisher ?? 'N/D'}
                  </td>
                </tr>
              ),
            )}
          </tbody>
        </table>
      </div>

      <TechnicalDetail command={command} />
    </>
  )
}

function GenericResult({
  command,
}: {
  command: DeviceCommand
}) {
  const fields =
    scalarFields(
      safeParseJson(command.resultJson),
    )

  if (
    fields.length === 0
  ) {
    return (
      <p className="windows-control-fields">
        El comando se ejecutó sin un resultado estructurado.
      </p>
    )
  }

  return (
    <>
      <div className="windows-control-fields">
        {fields.map(
          field => (
            <div key={field.label}>
              <span>
                {humanize(
                  field.label,
                )}
              </span>

              <strong>
                {field.value}
              </strong>
            </div>
          ),
        )}
      </div>

      <TechnicalDetail command={command} />
    </>
  )
}

interface Props {
  command:
    DeviceCommand | null
}

export function CommandResultView({
  command,
}: Props) {
  if (!command) {
    return (
      <p className="windows-control-fields">
        Sin resultado aún.
      </p>
    )
  }

  if (
    command.status ===
      'Failed'
    ||
    command.status ===
      'Timeout'
  ) {
    return (
      <div>
        <p className="windows-command-item__error">
          {command.errorMessage ??
            command.errorCode ??
            'El comando no se completó.'}
        </p>

        <TechnicalDetail command={command} />
      </div>
    )
  }

  if (
    command.status !==
      'Success'
  ) {
    return (
      <p className="windows-control-fields">
        Comando en estado{' '}
        {command.status}
        , esperando el resultado.
      </p>
    )
  }

  switch (
    command.commandType
  ) {
    case 'DEVICE_INVENTORY':
      return (
        <InventoryResult
          inventory={parseInventory(command)}
          technical={command}
        />
      )

    case 'SECURITY_STATUS':
      return (
        <SecurityResult command={command} />
      )

    case 'WINDOWS_UPDATE_STATUS':
      return (
        <UpdateResult command={command} />
      )

    case 'PROCESS_INVENTORY':
      return (
        <ProcessResult command={command} />
      )

    case 'SERVICE_INVENTORY':
      return (
        <ServiceResult command={command} />
      )

    case 'APP_INVENTORY':
      return (
        <ApplicationResult command={command} />
      )

    default:
      return (
        <GenericResult command={command} />
      )
  }
}

export function CommandResultRemember({
  command,
}: Props) {
  if (!command) {
    return null
  }

  if (
    command.status ===
      'Failed'
    ||
    command.status ===
      'Timeout'
  ) {
    return (
      <small className="windows-command-item__meta">
        {command.errorMessage ??
          command.errorCode}
      </small>
    )
  }

  if (
    command.status !==
      'Success'
  ) {
    return null
  }

  const fields =
    scalarFields(
      safeParseJson(
        command.resultJson,
      ),
    )

  return (
    <small className="windows-command-item__meta">
      {fields
        .slice(0, 3)
        .map(
          field =>
            `${humanize(field.label)}: ${field.value}`,
        )
        .join('  •  ')}
    </small>
  )
}