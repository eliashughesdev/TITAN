
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Clock3,
  Command,
  ExternalLink,
  LockKeyhole,
  MapPin,
  MonitorSmartphone,
  RefreshCcw,
  Search,
  ShieldCheck,
  Smartphone,
  Wifi,
  WifiOff,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'
import { Link } from 'react-router-dom'
import { androidEnterpriseApi } from '../../../api/androidEnterpriseApi'
import { useAuth } from '../../../auth/AuthContext'
import { Permissions } from '../../../auth/permissions'
import { AndroidRemoteConsole } from './remote/AndroidRemoteConsole'
import type {
  AndroidAuditEvent,
  AndroidDeviceInventorySummary,
  AndroidInventoryItem,
} from '../../../types/androidEnterprise'
import './AndroidFleetCommandCenter.css'

const PAGE_SIZE = 25

function dateLabel(date?: string | null): string {
  if (!date) return 'Sin información'
  const parsed = new Date(date)
  return Number.isNaN(parsed.getTime())
    ? 'Sin información'
    : parsed.toLocaleString('es-DO')
}

function isOnline(device: AndroidInventoryItem): boolean {
  const value = device.status.toLowerCase()
  return value === 'online' || value === 'en línea'
}

function manufacturerLabel(device: AndroidInventoryItem): string {
  return (device.manufacturer || 'Fabricante desconocido').trim()
}

function deviceCompliance(device: AndroidInventoryItem): string {
  return device.complianceStatus || 'Sin evaluar'
}

function getErrorMessage(error: unknown): string {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (
      error as {
        response?: { data?: { message?: string; code?: string } }
      }
    ).response
    if (response?.data?.message) return response.data.message
    if (response?.data?.code) return response.data.code
  }
  return error instanceof Error
    ? error.message
    : 'La operación no pudo completarse.'
}

export function AndroidFleetCommandCenter() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(Permissions.androidEnterprise.manage)

  const [summary, setSummary] =
    useState<AndroidDeviceInventorySummary | null>(null)
  const [devices, setDevices] = useState<AndroidInventoryItem[]>([])
  const [audit, setAudit] = useState<AndroidAuditEvent[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [query, setQuery] = useState('')
  const [appliedQuery, setAppliedQuery] = useState('')
  const [manufacturer, setManufacturer] = useState('all')
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [syncing, setSyncing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [inventory, totals, history] = await Promise.all([
        androidEnterpriseApi.getInventory({
          page,
          pageSize: PAGE_SIZE,
          search: appliedQuery,
        }),
        androidEnterpriseApi.getDeviceSummary(),
        androidEnterpriseApi.getAudit({
          page: 1,
          pageSize: 8,
        }),
      ])
      setDevices(inventory.items)
      setTotal(inventory.totalCount)
      setSummary(totals)
      setAudit(history.items)
    } catch (failure) {
      setError(getErrorMessage(failure))
    } finally {
      setLoading(false)
    }
  }, [page, appliedQuery])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)

    return () => window.clearTimeout(timer)
  }, [load])

  const manufacturers = useMemo(() => {
    return Array.from(
      new Set(
        devices.map(manufacturerLabel).filter(Boolean),
      ),
    ).sort()
  }, [devices])

  const visibleDevices = useMemo(() => {
    return manufacturer === 'all'
      ? devices
      : devices.filter(item => manufacturerLabel(item) === manufacturer)
  }, [devices, manufacturer])

  const selectedDevice = useMemo(() => {
    return devices.find(item => item.deviceId === selectedDeviceId) ?? null
  }, [devices, selectedDeviceId])

  const onlineCurrentPage = devices.filter(isOnline).length

  const complianceCurrentPage = devices.filter(item =>
    item.complianceStatus.toLowerCase() === 'compliant',
  ).length

  async function synchronize() {
    if (!canManage || syncing) return
    setSyncing(true)
    setError(null)
    setNotice(null)
    try {
      const result = await androidEnterpriseApi.synchronizeDevices()
      setNotice(
        `Sincronización finalizada: ${result.created} nuevos, ` +
        `${result.updated} actualizados y ${result.failed} errores.`,
      )
      await load()
    } catch (failure) {
      setError(getErrorMessage(failure))
    } finally {
      setSyncing(false)
    }
  }

  return (
    <section className="afcc" aria-label="Centro de control Android">
      <header className="afcc-hero">
        <div>
          <div className="afcc-eyebrow">
            <Activity size={14} />
            ANDROID ENTERPRISE · FLEET OPERATIONS
          </div>
          <h2>Centro de Control de Flota</h2>
          <p>
            Inventario, seguridad, aplicaciones, Launcher y operaciones
            empresariales desde una única consola.
          </p>
        </div>
        <div className="afcc-hero-actions">
          <button type="button" onClick={() => void load()} disabled={loading}>
            <RefreshCcw size={15} /> Actualizar
          </button>
          <button
            type="button"
            className="afcc-primary"
            disabled={!canManage || syncing}
            onClick={() => void synchronize()}
          >
            <RefreshCcw size={15} />
            {syncing ? 'Sincronizando...' : 'Sincronizar con Google'}
          </button>
        </div>
      </header>

      {error && (
        <div className="afcc-message afcc-message-error" role="alert">
          <AlertTriangle size={17} /> {error}
        </div>
      )}

      {notice && (
        <div className="afcc-message afcc-message-success" role="status">
          <CheckCircle2 size={17} /> {notice}
        </div>
      )}

      <div className="afcc-metrics">
        <Metric icon={Smartphone} label="Inventario Android"
          value={summary?.total ?? '—'} detail="Total de dispositivos" />
        <Metric icon={ShieldCheck} label="Administrados"
          value={summary?.managed ?? '—'} detail="Inventario Android Enterprise" />
        <Metric icon={Wifi} label="En línea"
          value={onlineCurrentPage} detail="Solo página cargada" />
        <Metric icon={WifiOff} label="Ausentes en Google"
          value={summary?.missingInGoogle ?? '—'} detail="Requieren revisión" />
        <Metric icon={CheckCircle2} label="Conformes"
          value={summary?.compliant ?? '—'} detail="Evaluación corporativa" />
        <Metric icon={Clock3} label="Sincronización"
          value={summary?.lastSynchronizationUtc
            ? dateLabel(summary.lastSynchronizationUtc)
            : 'Pendiente'}
          detail={`${complianceCurrentPage} conformes en la página`} />
      </div>

      <nav className="afcc-modules" aria-label="Herramientas Android">
        <Link to="/enrollment?workspace=android">
          <Smartphone size={17} /> Inscripción
        </Link>
        <Link to="/policies?workspace=android">
          <ShieldCheck size={17} /> Políticas
        </Link>
        <Link to="/apps?workspace=android">
          <MonitorSmartphone size={17} /> Aplicaciones
        </Link>
        <Link to="/android/titan-launcher?workspace=android">
          <Command size={17} /> Titan Launcher
        </Link>
        <Link to="/kiosk?workspace=android">
          <LockKeyhole size={17} /> Kiosk
        </Link>
        <Link to="/fleet-map?workspace=android">
          <MapPin size={17} /> Geolocalización
        </Link>
      </nav>

      <div className="afcc-layout">
        <section className="afcc-card afcc-inventory">
          <div className="afcc-card-heading">
            <div>
              <h3>Dispositivos de la flota</h3>
              <p>{total} resultados en el servidor Android</p>
            </div>
            <span>{loading ? 'Actualizando...' : 'Datos de API'}</span>
          </div>

          <form
            className="afcc-filters"
            onSubmit={event => {
              event.preventDefault()
              setPage(1)
              setAppliedQuery(query.trim())
            }}
          >
            <label className="afcc-search">
              <Search size={17} />
              <input
                value={query}
                onChange={event => setQuery(event.target.value)}
                placeholder="Nombre, modelo o número de serie"
                aria-label="Buscar dispositivo Android"
              />
            </label>
            <select
              value={manufacturer}
              onChange={event => setManufacturer(event.target.value)}
              aria-label="Filtrar fabricante en página actual"
            >
              <option value="all">Todos los fabricantes de esta página</option>
              {manufacturers.map(name =>
                <option value={name} key={name}>{name}</option>,
              )}
            </select>
            <button type="submit">Buscar</button>
          </form>

          {!loading && total === 0 ? (
            <div className="afcc-empty afcc-empty-state" role="status">
              <Smartphone size={34} />
              <div>
                <strong>
                  No hay dispositivos Android Enterprise enrolados.
                </strong>
                <p>
                  La inscripción, las políticas, las aplicaciones, Kiosk y
                  Titan Launcher siguen disponibles para configurar la flota.
                </p>
                <Link
                  className="afcc-empty-link"
                  to="/enrollment?workspace=android"
                >
                  Configurar inscripción Android
                  <ArrowRight size={14} />
                </Link>
              </div>
            </div>
          ) : (
            <>
              <div className="afcc-table-scroll">
                <table className="afcc-table">
                  <thead>
                    <tr>
                      <th>Dispositivo</th>
                      <th>Fabricante</th>
                      <th>Gestión</th>
                      <th>Estado</th>
                      <th>Cumplimiento</th>
                      <th>Último contacto</th>
                      <th>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleDevices.map(device => (
                      <tr
                        key={device.deviceId}
                        className={
                          selectedDeviceId === device.deviceId
                            ? 'afcc-selected-row'
                            : ''
                        }
                      >
                        <td>
                          <strong>{device.deviceName}</strong>
                          <small>{device.serialNumber || device.deviceId}</small>
                        </td>
                        <td>
                          {manufacturerLabel(device)}
                          <small>{device.model ?? 'Modelo desconocido'}</small>
                        </td>
                        <td>
                          {device.managementMode || 'No determinado'}
                        </td>
                        <td>
                          <span className={
                            isOnline(device)
                              ? 'afcc-status afcc-online'
                              : 'afcc-status afcc-offline'
                          }>
                            {device.status || 'Sin estado'}
                          </span>
                        </td>
                        <td>{deviceCompliance(device)}</td>
                        <td>{dateLabel(device.lastSeenAtUtc)}</td>
                        <td>
                          <button
                            type="button"
                            onClick={() => setSelectedDeviceId(device.deviceId)}
                          >
                            Gestionar <ArrowRight size={14} />
                          </button>
                        </td>
                      </tr>
                    ))}
                    {visibleDevices.length === 0 && (
                      <tr>
                        <td colSpan={7} className="afcc-empty">
                          No hay dispositivos que coincidan con los filtros.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <footer className="afcc-pager">
                <span>
                  Página {page} de {Math.max(1, Math.ceil(total / PAGE_SIZE))}
                </span>
                <div>
                  <button type="button" disabled={page <= 1 || loading}
                    onClick={() => setPage(current => current - 1)}>
                    Anterior
                  </button>
                  <button type="button"
                    disabled={page * PAGE_SIZE >= total || loading}
                    onClick={() => setPage(current => current + 1)}>
                    Siguiente
                  </button>
                </div>
              </footer>
            </>
          )}
        </section>

        <aside className="afcc-side">
          <section className="afcc-card">
            <div className="afcc-card-heading">
              <div>
                <h3>Operaciones del dispositivo</h3>
                <p>Acciones autorizadas mediante Android Enterprise</p>
              </div>
            </div>

            {!selectedDevice ? (
              <div className="afcc-empty afcc-device-empty">
                <Smartphone size={32} />
                Selecciona un teléfono para administrar sus operaciones.
              </div>
            ) : (
              <>
                <div className="afcc-device-heading">
                  <Smartphone size={25} />
                  <div>
                    <strong>{selectedDevice.deviceName}</strong>
                    <span>
                      {manufacturerLabel(selectedDevice)} ·{' '}
                      {selectedDevice.model ?? 'Sin modelo'}
                    </span>
                    <span>
                      {selectedDevice.managementMode ?? 'Modo sin verificar'}
                    </span>
                  </div>
                </div>

                <dl className="afcc-device-info">
                  <div>
                    <dt>Versión Android</dt>
                    <dd>
                      {selectedDevice.androidVersion ?? 'Sin información'}
                      {selectedDevice.apiLevel
                        ? ` · API ${selectedDevice.apiLevel}`
                        : ''}
                    </dd>
                  </div>
                  <div>
                    <dt>Política aplicada</dt>
                    <dd>{selectedDevice.appliedPolicyName || 'Sin información'}</dd>
                  </div>
                  <div>
                    <dt>Estado de aplicación</dt>
                    <dd>{selectedDevice.appliedPolicyState || 'Desconocido'}</dd>
                  </div>
                  <div>
                    <dt>Última sincronización de política</dt>
                    <dd>{dateLabel(selectedDevice.lastPolicySyncTimeUtc)}</dd>
                  </div>
                  <div>
                    <dt>Última sincronización de inventario</dt>
                    <dd>{dateLabel(selectedDevice.lastSynchronizedAtUtc)}</dd>
                  </div>
                </dl>

                <p className="afcc-advisory">
                  La consola inferior evalúa cada acción contra el modo de
                  gestión, la versión de Android, el fabricante y tus permisos
                  antes de habilitarla. Una emisión no equivale a ejecución
                  confirmada en el dispositivo.
                </p>

                <div className="afcc-device-links">
                  <Link className="afcc-detail-link"
                    to={`/devices/${selectedDevice.deviceId}?workspace=android&platform=Android`}>
                    Ficha Android
                    <ExternalLink size={15} />
                  </Link>
                  <Link className="afcc-detail-link"
                    to="/fleet-map?workspace=android">
                    Ubicación de flota
                    <MapPin size={15} />
                  </Link>
                  <Link className="afcc-detail-link"
                    to="/policies?workspace=android">
                    Política y restricciones
                    <ShieldCheck size={15} />
                  </Link>
                </div>
              </>
            )}
          </section>

          <section className="afcc-card">
            <div className="afcc-card-heading">
              <div>
                <h3>Actividad y auditoría</h3>
                <p>Últimos eventos disponibles</p>
              </div>
            </div>

            {audit.length === 0 && (
              <p className="afcc-empty">Sin eventos registrados.</p>
            )}

            <div className="afcc-audit">
              {audit.map(event => (
                <div className="afcc-event" key={event.id}>
                  <Activity size={16} />
                  <div>
                    <strong>{event.action}</strong>
                    <span>{event.result}</span>
                    <small>
                      {event.actorName || 'Sistema'} ·{' '}
                      {dateLabel(event.createdAtUtc)}
                    </small>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </aside>
      </div>

      {selectedDevice && (
        <AndroidRemoteConsole
          key={selectedDevice.deviceId}
          deviceId={selectedDevice.deviceId}
          deviceName={selectedDevice.deviceName}
        />
      )}
    </section>
  )
}

type MetricIcon = typeof Smartphone

function Metric({
  icon: Icon,
  label,
  value,
  detail,
}: {
  icon: MetricIcon
  label: string
  value: number | string
  detail: string
}) {
  return (
    <article className="afcc-metric">
      <Icon size={19} />
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{detail}</small>
      </div>
    </article>
  )
}
