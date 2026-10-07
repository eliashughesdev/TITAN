import {
  CheckCircle2,
  Clock3,
  Package,
  RefreshCw,
  RotateCw,
} from 'lucide-react'

import type {
  SendWindowsCommand,
  UpdateView,
} from '../windowsControl.types'

import {
  formatDate,
} from '../windowsControl.utils'

import {
  WindowsTelemetryCard,
} from './WindowsTelemetryCard'

interface Props {
  update:
    UpdateView

  sendCommand:
    SendWindowsCommand
}

export function WindowsUpdateSection({
  update,
  sendCommand,
}: Props) {
  async function installAll() {
    if (
      update.availableUpdates.length ===
      0
    ) {
      window.alert(
        'No hay actualizaciones disponibles.',
      )

      return
    }

    if (
      !window.confirm(
        `TitanMDM instalará ${update.availableUpdates.length} actualización(es).\n\nEl equipo podría requerir reinicio.\n\n¿Continuar?`,
      )
    ) {
      return
    }

    await sendCommand(
      'WINDOWS_UPDATE_INSTALL',
      {
        kbArticleIds: [],
        acceptEula: true,
        downloadOnly: false,
      },
    )
  }

  async function installOne(
    kbArticleIds: string[],
    title: string,
  ) {
    if (
      kbArticleIds.length === 0
    ) {
      window.alert(
        'Esta actualización no expone un KB seleccionable.',
      )

      return
    }

    if (
      !window.confirm(
        `Instalar "${title}"?\n\nKB: ${kbArticleIds.join(', ')}`,
      )
    ) {
      return
    }

    await sendCommand(
      'WINDOWS_UPDATE_INSTALL',
      {
        kbArticleIds,
        acceptEula: true,
        downloadOnly: false,
      },
    )
  }

  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <RefreshCw size={18} />

          <h2>
            Windows Update
          </h2>

          <div className="windows-control-inline-actions">
            <button
              type="button"
              onClick={() =>
                void sendCommand(
                  'WINDOWS_UPDATE_STATUS',
                )
              }
            >
              Consultar
            </button>

            <button
              type="button"
              onClick={() =>
                void sendCommand(
                  'WINDOWS_UPDATE_SCAN',
                )
              }
            >
              Buscar actualizaciones
            </button>

            <button
              type="button"
              disabled={
                update.availableUpdates.length ===
                0
              }
              onClick={() =>
                void installAll()
              }
            >
              Instalar todas
            </button>
          </div>
        </header>

        {!update.available ? (
          <div className="windows-telemetry-empty">
            Ejecuta WINDOWS_UPDATE_STATUS para obtener el estado.
          </div>
        ) : (
          <>
            <div className="windows-telemetry-grid">
              <WindowsTelemetryCard
                title="Servicio"
                value={update.serviceStatus}
                detail="Windows Update Service"
                icon={<RefreshCw size={18} />}
                tone={
                  update.serviceStatus
                    .toLowerCase() ===
                  'running'
                    ? 'success'
                    : 'warning'
                }
              />

              <WindowsTelemetryCard
                title="Consulta"
                value={
                  update.serviceQuerySucceeded
                    ? 'Correcta'
                    : 'Error'
                }
                detail="Consulta al servicio wuauserv"
                icon={<CheckCircle2 size={18} />}
                tone={
                  update.serviceQuerySucceeded
                    ? 'success'
                    : 'danger'
                }
              />

              <WindowsTelemetryCard
                title="Disponibles"
                value={String(
                  update.availableUpdates.length,
                )}
                detail="Actualizaciones pendientes"
                icon={<Package size={18} />}
                tone={
                  update.availableUpdates.length === 0
                    ? 'success'
                    : 'warning'
                }
              />

              <WindowsTelemetryCard
                title="Reinicio"
                value={
                  update.pendingReboot === true
                    ? 'Pendiente'
                    : 'No requerido'
                }
                detail="Estado de reboot"
                icon={<RotateCw size={18} />}
                tone={
                  update.pendingReboot === true
                    ? 'warning'
                    : 'success'
                }
              />

              <WindowsTelemetryCard
                title="Historial"
                value={`${update.history.length} eventos`}
                detail="Historial reciente"
                icon={<Clock3 size={18} />}
                tone={
                  update.historyAvailable
                    ? 'success'
                    : 'warning'
                }
              />
            </div>

            <h3 className="windows-telemetry-section-title">
              Actualizaciones disponibles
            </h3>

            <div className="windows-control-table-wrapper">
              <table className="windows-control-table">
                <thead>
                  <tr>
                    <th>Actualización</th>
                    <th>KB</th>
                    <th>Severidad</th>
                    <th>Descargada</th>
                    <th>Reinicio</th>
                    <th>Acción</th>
                  </tr>
                </thead>

                <tbody>
                  {update.availableUpdates.length === 0 ? (
                    <tr>
                      <td colSpan={6}>
                        No hay actualizaciones pendientes.
                      </td>
                    </tr>
                  ) : (
                    update.availableUpdates.map(
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
                            {item.isDownloaded === true
                              ? 'Sí'
                              : 'No'}
                          </td>

                          <td>
                            {item.rebootRequired === true
                              ? 'Sí'
                              : 'No'}
                          </td>

                          <td>
                            <button
                              type="button"
                              onClick={() =>
                                void installOne(
                                  item.kbArticleIds,
                                  item.title,
                                )
                              }
                            >
                              Instalar
                            </button>
                          </td>
                        </tr>
                      ),
                    )
                  )}
                </tbody>
              </table>
            </div>

            <h3 className="windows-telemetry-section-title">
              Historial reciente
            </h3>

            <div className="windows-update-history">
              <table>
                <thead>
                  <tr>
                    <th>Actualización</th>
                    <th>Fecha</th>
                    <th>Resultado</th>
                    <th>HRESULT</th>
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
                        <td>{item.title}</td>

                        <td>
                          {formatDate(
                            item.date,
                          )}
                        </td>

                        <td>
                          {item.resultCode ?? 'N/D'}
                        </td>

                        <td>
                          {item.hResult ?? 'N/D'}
                        </td>
                      </tr>
                    ),
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </article>
    </section>
  )
}