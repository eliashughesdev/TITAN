import { useEffect, useState } from 'react'
import apiClient from '../../../api/apiClient'

interface Row {
  id: string
  deviceId: string
  deviceName: string
  status: string
  updatedAtUtc: string
  completedAtUtc: string | null
  errorCode: string | null
  errorMessage: string | null
  resultJson: string | null
}

interface Result {
  items: Row[]
  totalCount: number
  totalPages: number
  queuedDevices: number
  correlationAvailable: boolean
  summary: {
    status: string
    count: number
  }[]
}

const labels: Record<string, string> = {
  Pending: 'Pendiente',
  Queued: 'En cola',
  Dispatching: 'Enviando',
  Sent: 'Enviado',
  Delivered: 'Recibido',
  Executing: 'Ejecutando',
  Success: 'Éxito reportado',
  Failed: 'Fallido',
  Timeout: 'Tiempo agotado',
  Cancelled: 'Cancelado',
}

export function SoftwareDeploymentResults({
  deploymentId,
}: {
  deploymentId: string
}) {
  const [data, setData] = useState<Result | null>(null)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [refresh, setRefresh] = useState(0)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    let active = true

    setLoading(true)
    setError('')

    void apiClient
      .get<Result>(
        `/software-packages/deployments/${deploymentId}/results`,
        {
          params: {
            page,
            pageSize: 50,
          },
        },
      )
      .then(response => {
        if (active) setData(response.data)
      })
      .catch(() => {
        if (active) {
          setData(null)
          setError(
            'No se pudieron consultar los resultados del despliegue.',
          )
        }
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [deploymentId, page, refresh])

  return (
    <section className="wmc">
      <header>
        <div>
          <h2>Resultado del despliegue</h2>
          <p>
            Solo comandos asociados a este envío.
            Enviado y recibido no significan instalado.
          </p>
        </div>

        <button
          type="button"
          disabled={loading}
          onClick={() => setRefresh(value => value + 1)}
        >
          Actualizar
        </button>
      </header>

      {error && (
        <p role="alert" className="wmc-error">{error}</p>
      )}

      {loading && <p role="status">Consultando…</p>}

      {data && (
        <>
          {!data.correlationAvailable ? (
            <p className="wmc-notice">
              Este envío no tiene comandos con identificador
              de despliegue. Los envíos antiguos no permiten
              reconstruir una asociación exacta; consulta su
              historial por paquete y equipo. Los nuevos envíos
              usarán la asociación añadida en esta actualización.
            </p>
          ) : (
            <>
              <p>
                {data.totalCount} comandos asociados ·{' '}
                {data.queuedDevices} equipos contabilizados
                al crear el envío.
              </p>

              <div className="wmc-actions">
                {data.summary.map(item => (
                  <span key={item.status}>
                    {labels[item.status] ?? item.status}:
                    {' '}{item.count}
                  </span>
                ))}
              </div>
            </>
          )}

          <div className="wmc-scroll">
            <table>
              <thead>
                <tr>
                  <th>Equipo</th>
                  <th>Estado</th>
                  <th>Actualización</th>
                  <th>Error</th>
                  <th>Resultado</th>
                </tr>
              </thead>

              <tbody>
                {data.items.map(item => (
                  <tr key={item.id}>
                    <td>{item.deviceName}</td>
                    <td>
                      {labels[item.status] ?? item.status}
                    </td>
                    <td>
                      {new Date(
                        item.updatedAtUtc,
                      ).toLocaleString()}
                    </td>
                    <td>
                      {item.errorMessage ||
                        item.errorCode ||
                        '—'}
                    </td>
                    <td>
                      {item.resultJson ? (
                        <details>
                          <summary>Ver resultado</summary>
                          <pre>{item.resultJson}</pre>
                        </details>
                      ) : (
                        'Pendiente de resultado'
                      )}
                    </td>
                  </tr>
                ))}

                {!data.items.length && (
                  <tr>
                    <td colSpan={5}>
                      Sin comandos correlacionados.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="wmc-actions">
            <button
              type="button"
              disabled={page <= 1 || loading}
              onClick={() => setPage(value => value - 1)}
            >
              Anterior
            </button>

            <span>
              Página {page} de{' '}
              {Math.max(1, data.totalPages)}
            </span>

            <button
              type="button"
              disabled={
                loading || page >= data.totalPages
              }
              onClick={() => setPage(value => value + 1)}
            >
              Siguiente
            </button>
          </div>
        </>
      )}
    </section>
  )
}