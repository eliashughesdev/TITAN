import { useMemo, useState } from 'react'

import { SoftwareDeploymentResults } from './SoftwareDeploymentResults'
import { WindowsOperationResults } from '../../../components/windows/WindowsOperationResults'
import type { SoftwareDeployment } from '../../../api/applicationsApi'

interface Props {
  deployments: SoftwareDeployment[]
}

const labels: Record<string, string> = {
  Draft: 'Borrador',
  Pending: 'Pendiente',
  Queued: 'En cola',
  Running: 'En ejecución',
  InProgress: 'En ejecución',
  Completed: 'Completado',
  Succeeded: 'Completado',
  Failed: 'Fallido',
  Cancelled: 'Cancelado',
  PartiallyCompleted: 'Parcial',
}

function date(value: string) {
  const parsed = new Date(value)

  return Number.isNaN(parsed.getTime())
    ? 'Sin fecha válida'
    : parsed.toLocaleString()
}

export function SoftwareDeploymentsTab({
  deployments,
}: Props) {
  const [deploymentId, setDeploymentId] = useState('')
  const [packageId, setPackageId] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')

  const statuses = useMemo(
    () =>
      [...new Set(
        deployments.map(item => item.status),
      )].sort(),
    [deployments],
  )

  const visible = useMemo(() => {
    const term = search.trim().toLocaleLowerCase()

    return deployments
      .filter(
        item =>
          (!status || item.status === status) &&
          (!term ||
            [
              item.packageName,
              item.packageVersion,
              item.targetName,
              item.targetType,
            ].some(value =>
              value.toLocaleLowerCase().includes(term),
            )),
      )
      .sort(
        (a, b) =>
          Date.parse(b.createdAtUtc) -
          Date.parse(a.createdAtUtc),
      )
  }, [deployments, search, status])

  return (
    <section aria-label="Despliegues de software">
      <h2>Despliegues de software</h2>

      <p role="note">
        En cola significa enviado. La cantidad de equipos
        corresponde a comandos generados; no confirma
        la instalación. Consulta el resultado del envío
        y el inventario del equipo.
      </p>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: 12,
          margin: '18px 0',
        }}
      >
        <label
          style={{
            display: 'grid',
            gap: 6,
            flex: '1 1 220px',
          }}
        >
          Buscar paquete o destino
          <input
            value={search}
            onChange={event =>
              setSearch(event.target.value)
            }
            placeholder="Nombre, versión o grupo"
          />
        </label>

        <label style={{ display: 'grid', gap: 6 }}>
          Estado
          <select
            value={status}
            onChange={event =>
              setStatus(event.target.value)
            }
          >
            <option value="">Todos los estados</option>

            {statuses.map(value => (
              <option key={value} value={value}>
                {labels[value] ?? value}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          onClick={() => {
            setSearch('')
            setStatus('')
          }}
          disabled={!search && !status}
        >
          Limpiar filtros
        </button>
      </div>

      <p aria-live="polite">
        {visible.length} de {deployments.length} despliegues
      </p>

      <div className="apps-table-wrapper">
        <table className="apps-table">
          <thead>
            <tr>
              <th>Software</th>
              <th>Versión</th>
              <th>Destino</th>
              <th>Tipo</th>
              <th>Equipos en cola</th>
              <th>Estado</th>
              <th>Fecha de envío</th>
              <th>Resultados</th>
            </tr>
          </thead>

          <tbody>
            {visible.length === 0 ? (
              <tr>
                <td colSpan={8} className="apps-empty">
                  {deployments.length
                    ? 'No hay resultados para estos filtros.'
                    : 'Todavía no se han enviado paquetes.'}
                </td>
              </tr>
            ) : (
              visible.map(item => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.packageName}</strong>
                  </td>
                  <td>{item.packageVersion}</td>
                  <td>
                    {item.targetName || item.targetId}
                  </td>
                  <td>
                    {item.targetType === 'Group'
                      ? 'Grupo'
                      : item.targetType === 'Device'
                        ? 'Equipo'
                        : item.targetType}
                  </td>
                  <td>{item.queuedDevices}</td>
                  <td>
                    <span
                      className={`apps-deployment-status ${item.status.toLowerCase()}`}
                    >
                      {labels[item.status] ?? item.status}
                    </span>
                  </td>
                  <td>{date(item.createdAtUtc)}</td>
                  <td>
                    <button
                      type="button"
                      onClick={() =>
                        setDeploymentId(item.id)
                      }
                    >
                      Ver este envío
                    </button>

                    <button
                      type="button"
                      onClick={() =>
                        setPackageId(item.packageId)
                      }
                    >
                      Historial del paquete
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {deploymentId && (
        <>
          <button
            type="button"
            onClick={() => setDeploymentId('')}
          >
            Cerrar envío
          </button>

          <SoftwareDeploymentResults
            key={deploymentId}
            deploymentId={deploymentId}
          />
        </>
      )}

      {packageId && (
        <>
          <button
            type="button"
            onClick={() => setPackageId('')}
          >
            Cerrar resultados
          </button>

          <WindowsOperationResults
            key={packageId}
            packageId={packageId}
          />
        </>
      )}
    </section>
  )
}