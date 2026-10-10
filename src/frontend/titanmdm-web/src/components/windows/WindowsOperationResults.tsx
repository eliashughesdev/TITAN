import { useEffect, useRef, useState } from 'react'

import { devicesApi } from '../../api/devicesApi'
import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../api/deviceCommandsApi'
import {
  policiesApi,
  type PolicyAssignment,
} from '../../api/policiesApi'

import type { DeviceListItem } from '../../types/device'

import { resultSummary } from '../windows-control/windowsControl.utils'
import { CommandResultView } from '../windows-control/CommandResultView'

interface Props {
  packageId?: string
  policyId?: string
}

const labels: Record<string, string> = {
  Pending: 'Pendiente',
  Queued: 'En cola',
  Dispatching: 'Enviando',
  Sent: 'Enviado',
  Delivered: 'Recibido',
  Executing: 'Ejecutando',
  Success: 'Éxito reportado por el agente',
  Failed: 'Fallido',
  Timeout: 'Tiempo agotado',
  Cancelled: 'Cancelado',
  Applying: 'Aplicando',
  Applied: 'Aplicada',
  Removed: 'Retirada',
}

function belongs(command: DeviceCommand, packageId: string) {
  if (command.commandType !== 'SOFTWARE_INSTALL') {
    return false
  }

  try {
    const payload = JSON.parse(command.payloadJson) as {
      packageId?: string
    }

    return (
      payload.packageId?.toLowerCase() === packageId.toLowerCase()
    )
  } catch {
    return false
  }
}

function date(value: string | null) {
  if (!value) return '—'

  const parsed = new Date(value)

  return Number.isNaN(parsed.getTime())
    ? '—'
    : parsed.toLocaleString()
}

export function WindowsOperationResults({
  packageId,
  policyId,
}: Props) {
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [search, setSearch] = useState('')
  const [deviceId, setDeviceId] = useState('')

  const [commands, setCommands] = useState<DeviceCommand[]>([])
  const [assignments, setAssignments] = useState<PolicyAssignment[]>([])
  const [detail, setDetail] = useState<DeviceCommand | null>(null)

  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [error, setError] = useState('')
  const [refresh, setRefresh] = useState(0)

  const detailVersion = useRef(0)

  useEffect(() => {
    detailVersion.current++
    setDetail(null)
    setDetailLoading(false)
  }, [packageId, policyId, deviceId])

  useEffect(() => {
    if (!packageId) return

    let active = true

    const timer = window.setTimeout(() => {
      void devicesApi
        .getDevices({
          platform: 'Windows',
          search,
          page: 1,
          pageSize: 100,
        })
        .then(result => {
          if (active) setDevices(result.items)
        })
        .catch(() => {
          if (active) {
            setError('No se pudo cargar el listado de equipos.')
          }
        })
    }, 300)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [packageId, search])

  useEffect(() => {
    let active = true

    setCommands([])
    setAssignments([])
    setError('')

    if (!policyId && (!packageId || !deviceId)) {
      setLoading(false)
      return
    }

    setLoading(true)

    void (async () => {
      try {
        if (policyId) {
          const result = await policiesApi.getAssignments(policyId)

          if (active) setAssignments(result)
        } else if (packageId && deviceId) {
          const result = await deviceCommandsApi.getForDevice(deviceId)

          if (active) {
            setCommands(
              result.items.filter(item => belongs(item, packageId)),
            )
          }
        }
      } catch {
        if (active) {
          setError(
            'No se pudieron consultar los resultados. Verifica tu acceso y la conexión con el backend.',
          )
        }
      } finally {
        if (active) setLoading(false)
      }
    })()

    return () => {
      active = false
    }
  }, [packageId, policyId, deviceId, refresh])

  async function openCommand(id: string) {
    const version = ++detailVersion.current

    setDetail(null)
    setDetailLoading(true)

    try {
      const result = await deviceCommandsApi.getById(id)

      if (version === detailVersion.current) {
        setDetail(result)
      }
    } catch {
      if (version === detailVersion.current) {
        setError('No se pudo consultar el comando.')
      }
    } finally {
      if (version === detailVersion.current) {
        setDetailLoading(false)
      }
    }
  }

  return (
    <section
      aria-busy={loading}
      style={{
        marginTop: 20,
        padding: 20,
        border: '1px solid #dbe3ef',
        borderRadius: 16,
        background: '#fff',
      }}
    >
      <h3>Resultados por equipo</h3>

      {packageId && (
        <>
          <p>
            Historial del paquete, no de un despliegue concreto.
            Se revisan los últimos 50 comandos del equipo seleccionado.
          </p>

          <div
            style={{
              display: 'grid',
              gap: 10,
              marginBottom: 16,
            }}
          >
            <label>
              Buscar equipo (hasta 100 resultados)

              <input
                style={{ display: 'block', width: '100%' }}
                value={search}
                onChange={event => setSearch(event.target.value)}
                placeholder="Nombre del equipo"
              />
            </label>

            <label>
              Equipo Windows

              <select
                style={{ display: 'block', width: '100%' }}
                value={deviceId}
                onChange={event => setDeviceId(event.target.value)}
              >
                <option value="">Selecciona un equipo</option>

                {deviceId &&
                  !devices.some(item => item.id === deviceId) && (
                    <option value={deviceId}>
                      Equipo seleccionado
                    </option>
                  )}

                {devices.map(item => (
                  <option key={item.id} value={item.id}>
                    {item.deviceName}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </>
      )}

      <button
        type="button"
        disabled={loading || (!policyId && !deviceId)}
        onClick={() => setRefresh(value => value + 1)}
      >
        Actualizar resultados
      </button>

      {error && (
        <p role="alert" style={{ color: '#b91c1c' }}>
          {error}
        </p>
      )}

      {loading ? (
        <p role="status">Consultando resultados…</p>
      ) : (
        <div style={{ overflowX: 'auto', marginTop: 16 }}>
          <table className="apps-table">
            <thead>
              <tr>
                <th>Equipo / comando</th>
                <th>Estado</th>
                <th>Última actualización</th>
                <th>Error</th>
                <th>Detalle</th>
              </tr>
            </thead>

            <tbody>
              {policyId
                ? assignments.map(item => (
                    <tr key={item.id}>
                      <td>
                        {item.deviceName}
                        <small style={{ display: 'block' }}>
                          Versión {item.policyVersion}
                        </small>
                      </td>

                      <td>{labels[item.status] ?? item.status}</td>
                      <td>{date(item.updatedAtUtc)}</td>
                      <td>{item.errorMessage || '—'}</td>

                      <td>
                        {item.commandId ? (
                          <button
                            type="button"
                            onClick={() =>
                              void openCommand(item.commandId!)
                            }
                          >
                            Ver comando
                          </button>
                        ) : (
                          'Sin comando asociado'
                        )}
                      </td>
                    </tr>
                  ))
                : commands.map(item => (
                    <tr key={item.id}>
                      <td>{item.id.slice(0, 8)}</td>
                      <td>{labels[item.status] ?? item.status}</td>
                      <td>{date(item.updatedAtUtc)}</td>

                      <td>
                        {item.errorMessage || item.errorCode || '—'}
                      </td>

                      <td>
                        <button
                          type="button"
                          onClick={() => void openCommand(item.id)}
                        >
                          Ver resultado
                        </button>
                      </td>
                    </tr>
                  ))}

              {(policyId
                ? assignments.length === 0
                : commands.length === 0) && (
                <tr>
                  <td colSpan={5}>
                    {packageId && !deviceId
                      ? 'Selecciona un equipo para consultar.'
                      : 'No se encontraron registros en esta consulta.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {detailLoading && (
        <p role="status">Cargando comando…</p>
      )}

      {detail && (
        <div
          style={{
            marginTop: 16,
            padding: 16,
            background: '#f8fafc',
            borderRadius: 12,
          }}
        >
          <strong>Comando {detail.id}</strong>

          <p>
            {labels[detail.status] ?? detail.status}
            {' · Finalizado: '}
            {date(detail.completedAtUtc)}
          </p>

          {detail.errorMessage && (
            <p role="alert">{detail.errorMessage}</p>
          )}

          {(() => {
            const summary = resultSummary(detail)

            return summary ? (
              <p
                style={{
                  marginTop: 8,
                  padding: '9px 11px',
                  borderRadius: 8,
                  background: '#eff8ff',
                  color: '#175cd3',
                  border: '1px solid #d0e3ff',
                  fontSize: 13,
                }}
              >
                {summary}
              </p>
            ) : null
          })()}

          <CommandResultView command={detail} />

          <button
            type="button"
            onClick={() => setDetail(null)}
          >
            Cerrar detalle
          </button>
        </div>
      )}
    </section>
  )
}