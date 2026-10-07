import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react'
import { Link } from 'react-router-dom'
import axios from 'axios'

import apiClient from '../../api/apiClient'
import { devicesApi } from '../../api/devicesApi'
import {
  applicationsApi,
  type DeviceApplication,
} from '../../api/applicationsApi'
import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../api/deviceCommandsApi'
import { useAuth } from '../../auth/AuthContext'
import type {
  DeviceListItem,
  DeviceListResult,
} from '../../types/device'

import './WindowsModuleCompletion.css'

type Mode = 'applications' | 'security' | 'reports'

interface Filters {
  search: string
  platform: string
  status: string
  compliance: string
  department: string
  from: string
  to: string
}

const initial: Filters = {
  search: '',
  platform: 'Windows',
  status: '',
  compliance: '',
  department: '',
  from: '',
  to: '',
}

const states: Record<string, string> = {
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

function message(error: unknown) {
  if (axios.isAxiosError(error)) {
    const detail =
      typeof error.response?.data?.message === 'string'
        ? error.response.data.message
        : 'no se pudo completar la solicitud.'

    return `Error ${
      error.response?.status ?? 'de conexión'
    }: ${detail}`
  }

  return 'No se pudo completar la operación.'
}

function date(value: string | null) {
  return value
    ? new Date(value).toLocaleString()
    : 'Sin contacto'
}

function parameters(
  filters: Filters,
  page: number,
  pageSize = 50,
) {
  return {
    search: filters.search || undefined,
    platform: filters.platform || undefined,
    status: filters.status || undefined,
    compliance: filters.compliance || undefined,
    department: filters.department || undefined,
    fromUtc: filters.from
      ? new Date(`${filters.from}T00:00:00`).toISOString()
      : undefined,
    toUtc: filters.to
      ? new Date(
          `${filters.to}T23:59:59.999`,
        ).toISOString()
      : undefined,
    page,
    pageSize,
  }
}

const headers = [
  'Equipo',
  'Plataforma',
  'Estado',
  'Cumplimiento',
  'Serial',
  'Sistema',
  'Versión',
  'Usuario',
  'Departamento',
  'IP',
  'Administrado',
  'Último contacto UTC',
]

function cells(item: DeviceListItem) {
  return [
    item.deviceName,
    item.platform,
    item.status,
    item.complianceStatus,
    item.serialNumber,
    item.operatingSystem ?? '',
    item.operatingSystemVersion ?? '',
    item.assignedUser ?? '',
    item.department ?? '',
    item.ipAddress ?? '',
    item.isManaged ? 'Sí' : 'No',
    item.lastSeenAtUtc ?? '',
  ]
}

export function WindowsModuleCompletion({
  mode,
}: {
  mode: Mode
}) {
  const { hasPermission } = useAuth()

  const canRead = hasPermission(
    mode === 'reports' ? 'reports.view' : 'devices.view',
  )
  const canExport = hasPermission('reports.export')
  const canCommand = hasPermission('devices.commands')

  const [draft, setDraft] = useState<Filters>(initial)
  const [filters, setFilters] = useState<Filters>(initial)
  const [page, setPage] = useState(1)
  const [data, setData] = useState<DeviceListResult | null>(null)
  const [device, setDevice] = useState<DeviceListItem | null>(null)
  const [apps, setApps] = useState<DeviceApplication[]>([])
  const [commands, setCommands] = useState<DeviceCommand[]>([])
  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [revision, setRevision] = useState(0)
  const [detailRevision, setDetailRevision] = useState(0)

  const version = useRef(0)
  const detailVersion = useRef(0)
  const cancelExport = useRef(false)

  const fetchPage = useCallback(
    async (
      selected: Filters,
      number: number,
      size = 50,
      exporting = false,
    ) => {
      const params = parameters(selected, number, size)

      if (mode === 'reports') {
        const response = await apiClient.get<DeviceListResult>(
          '/reports/device-detail',
          {
            params: {
              ...params,
              export: exporting,
            },
          },
        )

        return response.data
      }

      return devicesApi.getDevices(params)
    },
    [mode],
  )

  useEffect(() => {
    if (!canRead) return

    const current = ++version.current

    setLoading(true)
    setError('')

    void fetchPage(filters, page)
      .then(result => {
        if (current === version.current) setData(result)
      })
      .catch(error => {
        if (current === version.current) {
          setData(null)
          setError(message(error))
        }
      })
      .finally(() => {
        if (current === version.current) setLoading(false)
      })

    return () => {
      version.current++
    }
  }, [canRead, filters, page, revision, fetchPage])

  useEffect(() => {
    const current = ++detailVersion.current

    setApps([])
    setCommands([])

    if (!device || mode === 'reports') {
      setDetailLoading(false)
      return
    }

    setDetailLoading(true)

    void (async () => {
      const results = await Promise.allSettled([
        mode === 'applications'
          ? applicationsApi.getByDevice(device.id)
          : Promise.resolve([] as DeviceApplication[]),
        deviceCommandsApi.getForDevice(device.id),
      ])

      if (current !== detailVersion.current) return

      if (results[0].status === 'fulfilled') {
        setApps(results[0].value)
      }

      if (results[1].status === 'fulfilled') {
        setCommands(results[1].value.items)
      }

      if (
        results.some(result => result.status === 'rejected')
      ) {
        setError(
          'No se pudo cargar una parte del detalle. Verifica permisos y conexión.',
        )
      }

      setDetailLoading(false)
    })()

    return () => {
      detailVersion.current++
    }
  }, [device, mode, detailRevision])

  useEffect(
    () => () => {
      cancelExport.current = true
    },
    [],
  )

  function apply() {
    if (
      draft.from &&
      draft.to &&
      draft.from > draft.to
    ) {
      setError('La fecha inicial supera la final.')
      return
    }

    setError('')
    setPage(1)
    setDevice(null)
    setFilters({ ...draft })
    setRevision(value => value + 1)
  }

  async function send(type: string) {
    if (!device || busy || !canCommand) return

    setBusy(true)
    setError('')
    setNotice('')

    try {
      const command = await deviceCommandsApi.create({
        deviceId: device.id,
        commandType: type,
        expirationMinutes: 60,
      })

      setNotice(
        `Comando ${command.id.slice(0, 8)} aceptado. El agente debe ejecutarlo; Actualizar detalle consulta su resultado.`,
      )

      setDetailRevision(value => value + 1)
    } catch (error) {
      setError(message(error))
    } finally {
      setBusy(false)
    }
  }

  async function download(
    format: 'pdf' | 'excel' | 'csv',
  ) {
    if (!canExport || exporting) return

    setExporting(true)
    setError('')
    cancelExport.current = false

    try {
      const first = await fetchPage(filters, 1, 100, true)
      const items = [...first.items]

      if (first.totalCount > 50000) {
        throw new Error('too-many')
      }

      if (
        format === 'pdf' &&
        first.totalCount > 2000
      ) {
        throw new Error('pdf-limit')
      }

      for (
        let number = 2;
        number <= first.totalPages;
        number++
      ) {
        if (cancelExport.current) return

        setNotice(
          `Exportando página ${number} de ${first.totalPages}…`,
        )

        const result = await fetchPage(
          filters,
          number,
          100,
          true,
        )

        items.push(...result.items)
      }

      if (cancelExport.current) return

      const rows = items.map(cells)
      const name =
        `TitanMDM-dispositivos-${new Date().toISOString().slice(0, 10)}`

      if (format === 'excel') {
        const XLSX = await import('xlsx')
        const book = XLSX.utils.book_new()
        const sheet = XLSX.utils.aoa_to_sheet([
          headers,
          ...rows,
        ])

        sheet['!cols'] = headers.map(() => ({ wch: 24 }))
        sheet['!autofilter'] = {
          ref: sheet['!ref'] ?? 'A1:L1',
        }

        XLSX.utils.book_append_sheet(
          book,
          sheet,
          'Dispositivos',
        )

        XLSX.utils.book_append_sheet(
          book,
          XLSX.utils.aoa_to_sheet(
            Object.entries({
              ...filters,
              fechaReporte: new Date().toISOString(),
              registros: items.length,
            }),
          ),
          'Filtros',
        )

        XLSX.writeFile(book, `${name}.xlsx`)
      } else if (format === 'pdf') {
        const [
          { jsPDF },
          { default: autoTable },
        ] = await Promise.all([
          import('jspdf'),
          import('jspdf-autotable'),
        ])

        const doc = new jsPDF({
          orientation: 'landscape',
          format: 'a3',
        })

        doc.setFontSize(16)
        doc.text(
          'TitanMDM - Inventario filtrado',
          14,
          16,
        )
        doc.setFontSize(9)
        doc.text(
          `Plataforma: ${filters.platform || 'Todas'} | Registros: ${items.length} | ${new Date().toLocaleString()}`,
          14,
          23,
        )

        autoTable(doc, {
          startY: 30,
          head: [headers],
          body: rows,
          styles: {
            fontSize: 7,
            overflow: 'linebreak',
          },
          headStyles: {
            fillColor: [37, 99, 235],
          },
        })

        doc.save(`${name}.pdf`)
      } else {
        const escape = (value: string) => {
          const safe = /^[\s]*[=+@-]/.test(value)
            ? "'" + value
            : value

          return `"${safe.replace(/"/g, '""')}"`
        }

        const content = [headers, ...rows]
          .map(row => row.map(escape).join(';'))
          .join('\r\n')

        const blob = new Blob(
          ['\uFEFF' + content],
          { type: 'text/csv;charset=utf-8' },
        )

        const url = URL.createObjectURL(blob)
        const anchor = document.createElement('a')

        anchor.href = url
        anchor.download = `${name}.csv`
        anchor.click()

        window.setTimeout(
          () => URL.revokeObjectURL(url),
          1000,
        )
      }

      setNotice(
        `Exportados ${items.length} registros con los filtros aplicados. La exportación consulta páginas sucesivas; no bloquea cambios concurrentes en la flota.`,
      )
    } catch (error) {
      if (
        error instanceof Error &&
        error.message === 'too-many'
      ) {
        setError(
          'La consulta supera 50.000 registros. Reduce el alcance con filtros.',
        )
      } else if (
        error instanceof Error &&
        error.message === 'pdf-limit'
      ) {
        setError(
          'El PDF admite hasta 2.000 registros por exportación. Usa filtros o Excel para un volumen mayor.',
        )
      } else {
        setError(message(error))
      }
    } finally {
      setExporting(false)
    }
  }

  if (!canRead) {
    return (
      <section className="wmc">
        <p>
          Necesitas permiso de{' '}
          {mode === 'reports'
            ? 'reportes'
            : 'consulta de dispositivos'}{' '}
          para utilizar la gestión detallada.
        </p>
      </section>
    )
  }

  return (
    <section className="wmc">
      <header>
        <div>
          <span>GESTIÓN DETALLADA</span>
          <h2>
            {mode === 'reports'
              ? 'Inventario y exportación con filtros'
              : mode === 'applications'
                ? 'Software por equipo Windows'
                : 'Seguridad y control por equipo Windows'}
          </h2>

          <p>
            {mode === 'reports'
              ? 'Estos filtros afectan al informe detallado, no al resumen general. Sin fechas se incluye todo el inventario coincidente; las fechas filtran el último contacto.'
              : 'Selecciona un equipo para consultar registros, enviar análisis o abrir su centro de control.'}
          </p>
        </div>

        <button
          type="button"
          disabled={loading}
          onClick={() => setRevision(value => value + 1)}
        >
          Actualizar listado
        </button>
      </header>

      <form
        className="wmc-filters"
        onSubmit={event => {
          event.preventDefault()
          apply()
        }}
      >
        <label>
          Equipo, usuario, serial o IP
          <input
            value={draft.search}
            onChange={event =>
              setDraft({
                ...draft,
                search: event.target.value,
              })
            }
          />
        </label>

        {mode === 'reports' && (
          <label>
            Plataforma
            <select
              value={draft.platform}
              onChange={event =>
                setDraft({
                  ...draft,
                  platform: event.target.value,
                })
              }
            >
              <option value="">Todas</option>
              <option>Windows</option>
              <option>Android</option>
            </select>
          </label>
        )}

        <label>
          Conexión
          <select
            value={draft.status}
            onChange={event =>
              setDraft({
                ...draft,
                status: event.target.value,
              })
            }
          >
            <option value="">Todos</option>
            {[
              'Online',
              'Offline',
              'Pending',
              'Locked',
              'Quarantined',
              'Retired',
            ].map(value => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>

        <label>
          Cumplimiento
          <select
            value={draft.compliance}
            onChange={event =>
              setDraft({
                ...draft,
                compliance: event.target.value,
              })
            }
          >
            <option value="">Todos</option>
            {[
              'Unknown',
              'Evaluating',
              'Compliant',
              'NonCompliant',
              'Quarantined',
            ].map(value => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>

        {mode === 'reports' && (
          <>
            <label>
              Departamento
              <input
                value={draft.department}
                onChange={event =>
                  setDraft({
                    ...draft,
                    department: event.target.value,
                  })
                }
              />
            </label>

            <label>
              Último contacto desde
              <input
                type="date"
                value={draft.from}
                onChange={event =>
                  setDraft({
                    ...draft,
                    from: event.target.value,
                  })
                }
              />
            </label>

            <label>
              Hasta
              <input
                type="date"
                value={draft.to}
                onChange={event =>
                  setDraft({
                    ...draft,
                    to: event.target.value,
                  })
                }
              />
            </label>
          </>
        )}

        <button
          type="submit"
          disabled={loading || exporting}
        >
          Aplicar filtros
        </button>

        <button
          type="button"
          disabled={exporting}
          onClick={() => {
            setDraft(initial)
            setFilters(initial)
            setPage(1)
            setDevice(null)
            setRevision(value => value + 1)
          }}
        >
          Restablecer
        </button>
      </form>

      {error && (
        <p className="wmc-error" role="alert">{error}</p>
      )}

      {notice && (
        <p className="wmc-notice" role="status">{notice}</p>
      )}

      {mode === 'reports' && (
        <div className="wmc-actions">
          {(['pdf', 'excel', 'csv'] as const).map(format => (
            <button
              type="button"
              key={format}
              disabled={
                !canExport ||
                exporting ||
                !data?.totalCount
              }
              onClick={() => void download(format)}
            >
              Exportar {format.toUpperCase()}
            </button>
          ))}

          {exporting && (
            <button
              type="button"
              onClick={() => {
                cancelExport.current = true
                setNotice(
                  'Cancelando después de la solicitud actual…',
                )
              }}
            >
              Cancelar exportación
            </button>
          )}
        </div>
      )}

      <p aria-live="polite">
        {data?.totalCount ?? 0} registros coincidentes ·{' '}
        {data?.items.length ?? 0} en esta página.
      </p>

      <div className="wmc-scroll">
        <table>
          <thead>
            <tr>
              <th>Equipo</th>
              <th>Estado</th>
              <th>Cumplimiento</th>
              <th>Usuario / departamento</th>
              <th>IP</th>
              <th>Último contacto</th>
              <th>Detalle</th>
            </tr>
          </thead>

          <tbody>
            {data?.items.map(item => (
              <tr key={item.id}>
                <td>
                  <strong>{item.deviceName}</strong>
                  <small>
                    {item.operatingSystem}{' '}
                    {item.operatingSystemVersion}
                  </small>
                </td>
                <td>{item.status}</td>
                <td>{item.complianceStatus}</td>
                <td>
                  {item.assignedUser || 'Sin usuario'}
                  <small>
                    {item.department || 'Sin departamento'}
                  </small>
                </td>
                <td>{item.ipAddress || '—'}</td>
                <td>{date(item.lastSeenAtUtc)}</td>
                <td>
                  {mode === 'reports' ? (
                    <Link
                      to={`/devices/${item.id}?workspace=${item.platform.toLowerCase()}`}
                    >
                      Ver equipo
                    </Link>
                  ) : (
                    <button
                      type="button"
                      onClick={() => {
                        setError('')
                        setNotice('')
                        setDevice(item)
                      }}
                    >
                      Seleccionar
                    </button>
                  )}
                </td>
              </tr>
            ))}

            {!data?.items.length && (
              <tr>
                <td colSpan={7}>
                  {loading ? 'Cargando…' : 'Sin resultados.'}
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
          {Math.max(1, data?.totalPages ?? 1)}
        </span>
        <button
          type="button"
          disabled={
            loading ||
            page >= (data?.totalPages ?? 1)
          }
          onClick={() => setPage(value => value + 1)}
        >
          Siguiente
        </button>
      </div>

      {device && mode !== 'reports' && (
        <article className="wmc-detail">
          <h3>{device.deviceName}</h3>

          <div className="wmc-actions">
            <button
              type="button"
              disabled={detailLoading}
              onClick={() =>
                setDetailRevision(value => value + 1)
              }
            >
              Actualizar detalle
            </button>

            {canCommand && (
              <Link
                to={`/devices/${device.id}/control-center?workspace=windows`}
              >
                Abrir centro de control: software, seguridad,
                procesos, servicios y actualizaciones
              </Link>
            )}

            <button
              type="button"
              disabled={busy || !canCommand}
              onClick={() =>
                void send(
                  mode === 'applications'
                    ? 'APP_INVENTORY'
                    : 'SECURITY_STATUS',
                )
              }
            >
              {mode === 'applications'
                ? 'Solicitar inventario de software'
                : 'Analizar seguridad'}
            </button>

            {mode === 'security' && (
              <button
                type="button"
                disabled={busy || !canCommand}
                onClick={() => void send('COMPLIANCE_CHECK')}
              >
                Evaluar cumplimiento
              </button>
            )}

            <button
              type="button"
              onClick={() => setDevice(null)}
            >
              Cerrar equipo
            </button>
          </div>

          {detailLoading && (
            <p role="status">Cargando detalle…</p>
          )}

          {mode === 'applications' && (
            <div className="wmc-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Aplicación</th>
                    <th>Versión</th>
                    <th>Paquete</th>
                    <th>Presencia</th>
                    <th>Última detección</th>
                  </tr>
                </thead>
                <tbody>
                  {apps.map(app => (
                    <tr key={app.id}>
                      <td>{app.applicationName}</td>
                      <td>{app.versionName || '—'}</td>
                      <td>{app.packageName}</td>
                      <td>
                        {app.isPresent ? 'Presente' : 'Ausente'}
                      </td>
                      <td>{date(app.lastSeenAtUtc)}</td>
                    </tr>
                  ))}

                  {!apps.length && (
                    <tr>
                      <td colSpan={5}>
                        Sin inventario reportado. Solicita
                        APP_INVENTORY y consulta después
                        de su ejecución.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <h4>Últimos 50 comandos del equipo</h4>

          <div className="wmc-scroll">
            <table>
              <thead>
                <tr>
                  <th>Comando</th>
                  <th>Estado</th>
                  <th>Fecha</th>
                  <th>Error / resultado</th>
                </tr>
              </thead>

              <tbody>
                {commands.map(command => (
                  <tr key={command.id}>
                    <td>
                      {command.commandType}
                      <small>{command.id.slice(0, 8)}</small>
                    </td>
                    <td>
                      {states[command.status] ?? command.status}
                    </td>
                    <td>{date(command.updatedAtUtc)}</td>
                    <td>
                      {command.errorMessage ||
                        command.errorCode ||
                        '—'}

                      {command.resultJson && (
                        <details>
                          <summary>
                            Ver resultado reportado
                          </summary>
                          <pre>{command.resultJson}</pre>
                        </details>
                      )}
                    </td>
                  </tr>
                ))}

                {!commands.length && (
                  <tr>
                    <td colSpan={4}>
                      Sin comandos disponibles.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </article>
      )}
    </section>
  )
}

export async function readWindowsDeploymentTargets(
  api: typeof devicesApi,
): Promise<DeviceListResult> {
  const first = await api.getDevices({
    platform: 'Windows',
    page: 1,
    pageSize: 100,
  })

  if (first.totalCount > 50000) {
    throw new Error(
      'La flota supera el límite del selector. Usa grupos de dispositivos.',
    )
  }

  const items = [...first.items]

  for (
    let page = 2;
    page <= first.totalPages;
    page++
  ) {
    const result = await api.getDevices({
      platform: 'Windows',
      page,
      pageSize: 100,
    })

    items.push(...result.items)
  }

  return { ...first, items }
}