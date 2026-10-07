import {
  AppWindow,
  Download,
  RefreshCw,
  Search,
  Trash2,
  X,
} from 'lucide-react'

import {
  useMemo,
  useState,
} from 'react'

import type {
  DeviceCommandSnapshot,
} from '../../../../types/device'

import {
  extractExecutable,
  extractProductCode,
  getApplications,
  parseJson,
  type WindowsApplication,
} from './deviceOperational.utils'

interface Props {
  snapshot:
    DeviceCommandSnapshot | undefined

  busy:
    boolean

  onRefresh:
    () => Promise<void>

  onExecute:
    (
      commandType: string,
      payloadJson?: string,
    ) => Promise<void>
}

export function DeviceSoftwarePanel({
  snapshot,
  busy,
  onRefresh,
  onExecute,
}: Props) {
  const [
    search,
    setSearch,
  ] =
    useState('')

  const [
    selected,
    setSelected,
  ] =
    useState<
      WindowsApplication | null
    >(null)

  const [
    showInstall,
    setShowInstall,
  ] =
    useState(false)

  const [
    packagePath,
    setPackagePath,
  ] =
    useState('')

  const [
    expectedSha256,
    setExpectedSha256,
  ] =
    useState('')

  const [
    installArguments,
    setInstallArguments,
  ] =
    useState('')

  const apps =
    useMemo(
      () =>
        getApplications(
          parseJson(
            snapshot?.resultJson,
          ),
        ),
      [
        snapshot,
      ],
    )

  const filtered =
    useMemo(
      () => {
        const value =
          search
            .trim()
            .toLowerCase()

        if (!value) {
          return apps
        }

        return apps.filter(
          app =>
            app.name
              .toLowerCase()
              .includes(value)
            ||
            app.publisher
              .toLowerCase()
              .includes(value)
            ||
            app.version
              .toLowerCase()
              .includes(value),
        )
      },
      [
        apps,
        search,
      ],
    )

  async function uninstall() {
    if (!selected) {
      return
    }

    const productCode =
      extractProductCode(
        selected.uninstallString,
      )

    const executable =
      extractExecutable(
        selected.uninstallString,
      )

    if (
      !productCode
      &&
      !executable
    ) {
      window.alert(
        'TitanMDM no pudo determinar un método seguro de desinstalación para esta aplicación.',
      )

      return
    }

    const confirmed =
      window.confirm(
        `Desinstalar "${selected.name}" de este dispositivo?\n\nEsta acción modificará el endpoint.`,
      )

    if (!confirmed) {
      return
    }

    await onExecute(
      'SOFTWARE_UNINSTALL',
      JSON.stringify({
        productCode:
          productCode
          ??
          null,

        uninstallExecutable:
          productCode
            ? null
            : executable,

        arguments:
          null,

        timeoutSeconds:
          1800,
      }),
    )

    setSelected(null)

    await onRefresh()
  }

  async function install() {
    if (
      !packagePath.trim()
      ||
      !expectedSha256.trim()
    ) {
      window.alert(
        'PackagePath y SHA-256 son obligatorios.',
      )

      return
    }

    await onExecute(
      'SOFTWARE_INSTALL',
      JSON.stringify({
        packagePath:
          packagePath.trim(),

        expectedSha256:
          expectedSha256.trim(),

        arguments:
          installArguments.trim()
          ||
          null,

        timeoutSeconds:
          1800,
      }),
    )

    setShowInstall(false)
    setPackagePath('')
    setExpectedSha256('')
    setInstallArguments('')

    await onRefresh()
  }

  return (
    <article className="op-panel op-software">
      <header>
        <AppWindow
          size={18}
        />

        <div>
          <strong>
            Software instalado
          </strong>

          <span>
            {apps.length}{' '}
            aplicación
            {apps.length === 1
              ? ''
              : 'es'}{' '}
            detectada
            {apps.length === 1
              ? ''
              : 's'}
          </span>
        </div>

        <div className="op-panel-actions">
          <button
            type="button"
            disabled={busy}
            onClick={() =>
              void onRefresh()
            }
          >
            <RefreshCw
              size={14}
            />

            Actualizar
          </button>

          <button
            type="button"
            className="primary"
            disabled={busy}
            onClick={() =>
              setShowInstall(
                true,
              )
            }
          >
            <Download
              size={14}
            />

            Instalar
          </button>
        </div>
      </header>

      <div className="op-search">
        <Search
          size={16}
        />

        <input
          value={search}
          placeholder="Buscar aplicación, versión o fabricante..."
          onChange={
            event =>
              setSearch(
                event.target.value,
              )
          }
        />
      </div>

      {apps.length === 0 ? (
        <div className="op-empty-state">
          <AppWindow
            size={28}
          />

          <strong>
            Sin inventario de software
          </strong>

          <span>
            Ejecuta APP_INVENTORY para
            obtener las aplicaciones
            instaladas.
          </span>
        </div>
      ) : (
        <div className="op-table-wrap">
          <table className="op-table">
            <thead>
              <tr>
                <th>
                  Aplicación
                </th>

                <th>
                  Versión
                </th>

                <th>
                  Fabricante
                </th>

                <th>
                  Ubicación
                </th>

                <th />
              </tr>
            </thead>

            <tbody>
              {filtered.map(
                (
                  app,
                  index,
                ) => (
                  <tr
                    key={
                      `${app.name}-${app.version}-${index}`
                    }
                  >
                    <td>
                      <strong>
                        {app.name}
                      </strong>
                    </td>

                    <td>
                      {app.version
                      ||
                      'N/D'}
                    </td>

                    <td>
                      {app.publisher
                      ||
                      'N/D'}
                    </td>

                    <td
                      className="op-table-muted"
                    >
                      {app.installLocation
                      ||
                      'N/D'}
                    </td>

                    <td>
                      <button
                        type="button"
                        className="op-row-action"
                        onClick={() =>
                          setSelected(
                            app,
                          )
                        }
                      >
                        Administrar
                      </button>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}

      {selected && (
        <div className="op-modal-backdrop">
          <div className="op-modal">
            <header>
              <div>
                <span>
                  SOFTWARE
                </span>

                <h3>
                  {selected.name}
                </h3>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelected(null)
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="op-modal-grid">
              <div>
                <span>
                  Versión
                </span>

                <strong>
                  {selected.version
                  ||
                  'N/D'}
                </strong>
              </div>

              <div>
                <span>
                  Fabricante
                </span>

                <strong>
                  {selected.publisher
                  ||
                  'N/D'}
                </strong>
              </div>

              <div className="wide">
                <span>
                  Ubicación
                </span>

                <strong>
                  {selected.installLocation
                  ||
                  'N/D'}
                </strong>
              </div>

              <div className="wide">
                <span>
                  Método de desinstalación detectado
                </span>

                <code>
                  {extractProductCode(
                    selected
                      .uninstallString,
                  )
                  ??
                  extractExecutable(
                    selected
                      .uninstallString,
                  )
                  ??
                  'No disponible'}
                </code>
              </div>
            </div>

            <footer>
              <button
                type="button"
                onClick={() =>
                  setSelected(null)
                }
              >
                Cancelar
              </button>

              <button
                type="button"
                className="danger"
                disabled={busy}
                onClick={() =>
                  void uninstall()
                }
              >
                <Trash2
                  size={15}
                />

                Desinstalar
              </button>
            </footer>
          </div>
        </div>
      )}

      {showInstall && (
        <div className="op-modal-backdrop">
          <div className="op-modal">
            <header>
              <div>
                <span>
                  SOFTWARE DEPLOYMENT
                </span>

                <h3>
                  Instalar paquete
                </h3>
              </div>

              <button
                type="button"
                onClick={() =>
                  setShowInstall(
                    false,
                  )
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="op-form">
              <label>
                <span>
                  Ruta local del paquete
                </span>

                <input
                  value={
                    packagePath
                  }
                  placeholder="C:\Packages\App.msi"
                  onChange={
                    event =>
                      setPackagePath(
                        event
                          .target
                          .value,
                      )
                  }
                />
              </label>

              <label>
                <span>
                  SHA-256 autorizado
                </span>

                <input
                  value={
                    expectedSha256
                  }
                  placeholder="SHA256..."
                  onChange={
                    event =>
                      setExpectedSha256(
                        event
                          .target
                          .value,
                      )
                  }
                />
              </label>

              <label>
                <span>
                  Argumentos opcionales
                </span>

                <input
                  value={
                    installArguments
                  }
                  placeholder="/quiet /norestart"
                  onChange={
                    event =>
                      setInstallArguments(
                        event
                          .target
                          .value,
                      )
                  }
                />
              </label>
            </div>

            <footer>
              <button
                type="button"
                onClick={() =>
                  setShowInstall(
                    false,
                  )
                }
              >
                Cancelar
              </button>

              <button
                type="button"
                className="primary"
                disabled={busy}
                onClick={() =>
                  void install()
                }
              >
                <Download
                  size={15}
                />

                Instalar
              </button>
            </footer>
          </div>
        </div>
      )}
    </article>
  )
}