import {
  Package,
  XCircle,
} from 'lucide-react'

import type {
  Dispatch,
  SetStateAction,
} from 'react'

import type {
  SendWindowsCommand,
  UninstallTarget,
  WindowsApplicationItem,
} from '../windowsControl.types'

import {
  extractUninstallTarget,
} from '../windowsControl.utils'

interface Props {
  applications:
    WindowsApplicationItem[]

  filter: string

  setFilter:
    Dispatch<
      SetStateAction<string>
    >

  packagePath: string

  setPackagePath:
    Dispatch<
      SetStateAction<string>
    >

  packageSha256: string

  setPackageSha256:
    Dispatch<
      SetStateAction<string>
    >

  packageArguments: string

  setPackageArguments:
    Dispatch<
      SetStateAction<string>
    >

  uninstallTarget:
    UninstallTarget

  setUninstallTarget:
    Dispatch<
      SetStateAction<
        UninstallTarget
      >
    >

  sendCommand:
    SendWindowsCommand
}

export function WindowsSoftwareSection({
  applications,
  filter,
  setFilter,
  packagePath,
  setPackagePath,
  packageSha256,
  setPackageSha256,
  packageArguments,
  setPackageArguments,
  uninstallTarget,
  setUninstallTarget,
  sendCommand,
}: Props) {
  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <Package size={18} />

          <h2>
            Software instalado
          </h2>

          <div className="windows-control-inline-actions">
            <input
              className="windows-control-filter"
              type="search"
              placeholder="Buscar software..."
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
                  'APP_INVENTORY',
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
                <th>Aplicación</th>
                <th>Versión</th>
                <th>Publisher</th>
                <th>Ubicación</th>
                <th>Acción</th>
              </tr>
            </thead>

            <tbody>
              {applications.map(
                (
                  application,
                  index,
                ) => (
                  <tr
                    key={
                      `${application.name}-${index}`
                    }
                  >
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

                    <td>
                      {application.installLocation ?? 'N/D'}
                    </td>

                    <td>
                      <button
                        type="button"
                        className="windows-row-danger"
                        disabled={
                          !application.uninstallString
                        }
                        onClick={() =>
                          setUninstallTarget(
                            extractUninstallTarget(
                              application,
                            ),
                          )
                        }
                      >
                        Desinstalar
                      </button>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      </article>

      <article className="windows-control-card windows-control-card--wide">
        <header>
          <Package size={18} />

          <h2>
            Instalar paquete local
          </h2>
        </header>

        <div className="windows-control-form-stack">
          <input
            value={packagePath}
            onChange={
              event =>
                setPackagePath(
                  event.target.value,
                )
            }
            placeholder="Ruta local del paquete"
          />

          <input
            value={packageSha256}
            onChange={
              event =>
                setPackageSha256(
                  event.target.value,
                )
            }
            placeholder="SHA-256 esperado"
          />

          <input
            value={packageArguments}
            onChange={
              event =>
                setPackageArguments(
                  event.target.value,
                )
            }
            placeholder="Argumentos opcionales"
          />

          <button
            type="button"
            disabled={
              !packagePath.trim()
              ||
              !packageSha256.trim()
            }
            onClick={() =>
              void sendCommand(
                'SOFTWARE_INSTALL',
                {
                  packagePath:
                    packagePath.trim(),

                  expectedSha256:
                    packageSha256.trim(),

                  arguments:
                    packageArguments.trim()
                    ||
                    null,

                  timeoutSeconds:
                    1800,
                },
              )
            }
          >
            Instalar software
          </button>
        </div>
      </article>

      <article className="windows-control-card windows-control-card--wide">
        <header>
          <XCircle size={18} />

          <h2>
            Desinstalar software
          </h2>
        </header>

        <div className="windows-control-form-stack">
          <input
            value={uninstallTarget.name}
            readOnly
            placeholder="Aplicación seleccionada"
          />

          <input
            value={uninstallTarget.productCode}
            onChange={
              event =>
                setUninstallTarget(
                  current => ({
                    ...current,
                    productCode:
                      event.target.value,
                  }),
                )
            }
            placeholder="MSI ProductCode"
          />

          <input
            value={uninstallTarget.executable}
            onChange={
              event =>
                setUninstallTarget(
                  current => ({
                    ...current,
                    executable:
                      event.target.value,
                  }),
                )
            }
            placeholder="Uninstall executable"
          />

          <input
            value={uninstallTarget.arguments}
            onChange={
              event =>
                setUninstallTarget(
                  current => ({
                    ...current,
                    arguments:
                      event.target.value,
                  }),
                )
            }
            placeholder="Argumentos"
          />

          <button
            type="button"
            className="windows-row-danger"
            disabled={
              !uninstallTarget.productCode.trim()
              &&
              !uninstallTarget.executable.trim()
            }
            onClick={() =>
              void sendCommand(
                'SOFTWARE_UNINSTALL',
                {
                  productCode:
                    uninstallTarget.productCode.trim()
                    ||
                    null,

                  uninstallExecutable:
                    uninstallTarget.executable.trim()
                    ||
                    null,

                  arguments:
                    uninstallTarget.arguments.trim()
                    ||
                    null,

                  timeoutSeconds:
                    1800,
                },
              )
            }
          >
            Confirmar desinstalación
          </button>
        </div>
      </article>
    </section>
  )
}