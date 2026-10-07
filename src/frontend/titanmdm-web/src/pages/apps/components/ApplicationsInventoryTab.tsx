import {
  AppWindow,
  Search,
} from 'lucide-react'

import type {
  ApplicationSummary,
} from '../../../api/applicationsApi'

interface Props {
  applications:
    ApplicationSummary[]

  search: string

  filter:
    'all' |
    'user' |
    'system'

  loading: boolean

  onSearchChange:
    (value: string) => void

  onFilterChange:
    (
      value:
        'all' |
        'user' |
        'system',
    ) => void
}

export function ApplicationsInventoryTab({
  applications,
  search,
  filter,
  loading,
  onSearchChange,
  onFilterChange,
}: Props) {
  return (
    <>
      <div className="apps-toolbar">
        <label className="apps-search">
          <Search
            size={17}
          />

          <input
            value={search}
            placeholder="Buscar aplicación o package..."
            onChange={
              event =>
                onSearchChange(
                  event.target.value,
                )
            }
          />
        </label>

        <select
          value={filter}
          onChange={
            event =>
              onFilterChange(
                event.target.value as
                  | 'all'
                  | 'user'
                  | 'system',
              )
          }
        >
          <option value="all">
            Todas
          </option>

          <option value="user">
            Aplicaciones de usuario
          </option>

          <option value="system">
            Sistema
          </option>
        </select>
      </div>

      <div className="apps-table-wrapper">
        <table className="apps-table">
          <thead>
            <tr>
              <th>
                Aplicación
              </th>

              <th>
                Package
              </th>

              <th>
                Versión
              </th>

              <th>
                Tipo
              </th>

              <th>
                Dispositivos
              </th>

              <th>
                Habilitadas
              </th>

              <th>
                Última detección
              </th>
            </tr>
          </thead>

          <tbody>
            {!loading &&
            applications.length ===
              0 && (
              <tr>
                <td
                  colSpan={7}
                  className="apps-empty"
                >
                  No existe inventario
                  para mostrar.
                </td>
              </tr>
            )}

            {applications.map(
              application => (
                <tr
                  key={
                    `${application.packageName}-${application.versionCode}`
                  }
                >
                  <td>
                    <div className="app-name-cell">
                      <div className="app-icon">
                        <AppWindow
                          size={17}
                        />
                      </div>

                      <div>
                        <strong>
                          {
                            application
                              .applicationName
                          }
                        </strong>

                        <span>
                          {application
                            .versionName
                          ??
                          'Sin versión'}
                        </span>
                      </div>
                    </div>
                  </td>

                  <td className="package-cell">
                    {
                      application
                        .packageName
                    }
                  </td>

                  <td>
                    {application
                      .versionName
                    ??
                    'N/D'}
                  </td>

                  <td>
                    <span
                      className={
                        application
                          .isSystemApp
                          ? 'app-badge system'
                          : 'app-badge user'
                      }
                    >
                      {application
                        .isSystemApp
                        ? 'Sistema'
                        : 'Usuario'}
                    </span>
                  </td>

                  <td>
                    {
                      application
                        .deviceCount
                    }
                  </td>

                  <td>
                    {
                      application
                        .enabledCount
                    }
                  </td>

                  <td>
                    {new Date(
                      application
                        .lastSeenAtUtc,
                    )
                      .toLocaleString()}
                  </td>
                </tr>
              ),
            )}
          </tbody>
        </table>
      </div>
    </>
  )
}