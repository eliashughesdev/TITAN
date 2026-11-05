import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  ExternalLink,
  Info,
  List,
  MapPin,
  MapPinOff,
  MapPinned,
  Navigation,
  Radio,
  RefreshCw,
  Search,
  ShieldAlert,
  Smartphone,
} from 'lucide-react'
import {
  CircleMarker,
  MapContainer,
  TileLayer,
  useMap,
} from 'react-leaflet'
import type {
  LatLngBoundsExpression,
  LatLngExpression,
} from 'leaflet'
import { useNavigate } from 'react-router-dom'
import {
  locationApi,
  type FleetLocation,
  type FleetLocationFilters,
  type FleetLocationState,
} from '../../api/locationApi'
import { getActiveProvider } from '../../config/mapConfig'
import 'leaflet/dist/leaflet.css'
import './FleetMapPage.css'

const POLL_INTERVAL_MS = 60_000
const DEFAULT_CENTER: LatLngExpression = [18.4861, -69.9312]

type LoadMode = 'initial' | 'refresh' | 'poll'

type PositionedFleetLocation = FleetLocation & {
  latitude: number
  longitude: number
}

const LOCATION_STATE_COLORS: Record<FleetLocationState, string> = {
  Fresh: '#16a34a',
  Stale: '#f59e0b',
  Offline: '#94a3b8',
  NoLocation: '#64748b',
  PermissionDenied: '#ef4444',
}

const LOCATION_STATE_LABELS: Record<FleetLocationState, string> = {
  Fresh: 'Reciente',
  Stale: 'Antigua',
  Offline: 'Sin conexión',
  NoLocation: 'Sin ubicación',
  PermissionDenied: 'Permiso denegado',
}

const EMPTY_FILTERS: FleetLocationFilters = {
  search: '',
  manufacturer: '',
  department: '',
  siteId: '',
  locationState: '',
  maxSignalAgeMinutes: undefined,
}

function hasValidPosition(
  location: FleetLocation,
): location is PositionedFleetLocation {
  const { latitude, longitude } = location

  return (
    latitude !== null &&
    longitude !== null &&
    Number.isFinite(latitude) &&
    Number.isFinite(longitude) &&
    latitude >= -90 &&
    latitude <= 90 &&
    longitude >= -180 &&
    longitude <= 180
  )
}

function markerColorFor(location: FleetLocation): string {
  return LOCATION_STATE_COLORS[location.locationState]
}

function formatCapturedAt(value: string | null): string {
  if (!value) return 'Sin datos'

  const capturedAt = new Date(value)

  return Number.isNaN(capturedAt.getTime())
    ? 'Sin datos'
    : capturedAt.toLocaleString()
}

function MapController({
  center,
  bounds,
}: {
  center: LatLngExpression | null
  bounds: LatLngBoundsExpression | null
}) {
  const map = useMap()

  useEffect(() => {
    if (center) {
      map.setView(center, Math.max(map.getZoom(), 13))
      return
    }

    if (bounds) {
      map.fitBounds(bounds, {
        padding: [28, 28],
        maxZoom: 15,
      })
    }
  }, [bounds, center, map])

  return null
}

export function FleetMapPage() {
  const navigate = useNavigate()
  const provider = getActiveProvider()
  const [locations, setLocations] = useState<FleetLocation[]>([])
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null)
  const [filters, setFilters] =
    useState<FleetLocationFilters>(EMPTY_FILTERS)
  const [selectedDeviceId, setSelectedDeviceId] =
    useState<string | null>(null)

  const loadFleet = useCallback(
    async (mode: LoadMode) => {
      if (mode === 'refresh' || mode === 'poll') {
        setRefreshing(true)
      }

      if (mode !== 'poll') {
        setError(null)
      }

      try {
        const data = await locationApi.getFleet(filters)
        setLocations(
          data.filter(
            location =>
              location.platform === 'Android',
          ),
        )
        setLastUpdatedAt(new Date())
      } catch (failure) {
        console.error('Error loading Android fleet locations:', failure)
        setError('No fue posible cargar la ubicación de la flota Android.')
      } finally {
        setLoading(false)
        setRefreshing(false)
      }
    },
    [filters],
  )

  useEffect(() => {
    let cancelled = false

    const timer = window.setTimeout(() => {
      if (!cancelled) void loadFleet('initial')
    }, 0)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [loadFleet])

  useEffect(() => {
    const interval = window.setInterval(() => {
      if (document.visibilityState === 'visible') {
        void loadFleet('poll')
      }
    }, POLL_INTERVAL_MS)

    return () => window.clearInterval(interval)
  }, [loadFleet])

  const selectedDevice = useMemo(
    () =>
      locations.find(location => location.deviceId === selectedDeviceId) ??
      null,
    [locations, selectedDeviceId],
  )

  const positionedLocations = useMemo(
    () => locations.filter(hasValidPosition),
    [locations],
  )

  const fleetBounds = useMemo<LatLngBoundsExpression | null>(() => {
    if (positionedLocations.length === 0) return null

    return positionedLocations.map(location => [
      location.latitude,
      location.longitude,
    ])
  }, [positionedLocations])

  const selectedCenter = useMemo<LatLngExpression | null>(() => {
    if (!selectedDevice || !hasValidPosition(selectedDevice)) return null

    return [selectedDevice.latitude, selectedDevice.longitude]
  }, [selectedDevice])

  const stateCounts = useMemo(
    () =>
      locations.reduce<Record<FleetLocationState, number>>(
        (counts, location) => {
          counts[location.locationState] += 1
          return counts
        },
        {
          Fresh: 0,
          Stale: 0,
          Offline: 0,
          NoLocation: 0,
          PermissionDenied: 0,
        },
      ),
    [locations],
  )

  const siteOptions = useMemo(() => {
    const sites = new Map<string, string>()

    locations.forEach(location => {
      if (location.siteId && location.siteName) {
        sites.set(location.siteId, location.siteName)
      }
    })

    return Array.from(sites, ([id, name]) => ({ id, name }))
  }, [locations])

  const kpis: Array<{
    key: string
    label: string
    value: number
    tone: string
    icon: typeof Smartphone
  }> = [
    {
      key: 'total',
      label: 'Android enrolados',
      value: locations.length,
      tone: 'neutral',
      icon: Smartphone,
    },
    {
      key: 'position',
      label: 'Con posición válida',
      value: positionedLocations.length,
      tone: 'info',
      icon: Navigation,
    },
    {
      key: 'fresh',
      label: 'Recientes',
      value: stateCounts.Fresh,
      tone: 'ok',
      icon: Activity,
    },
    {
      key: 'stale',
      label: 'Antiguas',
      value: stateCounts.Stale,
      tone: 'warn',
      icon: Clock3,
    },
    {
      key: 'noloc',
      label: 'Sin ubicación',
      value: stateCounts.NoLocation,
      tone: 'muted',
      icon: MapPinOff,
    },
    {
      key: 'denied',
      label: 'Permiso denegado',
      value: stateCounts.PermissionDenied,
      tone: 'danger',
      icon: ShieldAlert,
    },
  ]

  function handleFilterChange(
    key: keyof FleetLocationFilters,
    value: string | number | undefined,
  ) {
    setFilters(current => ({ ...current, [key]: value }))
  }

  const emptyMessage = Object.values(filters).some(
    value => value !== '' && value !== undefined,
  )
    ? 'No hay dispositivos Android que coincidan con los filtros.'
    : 'Aún no existen teléfonos Android enrolados en esta organización.'

  return (
    <div className="fleet-map-page">
      <header className="fleet-map-hero">
        <div className="fleet-map-hero__identity">
          <MapPinned size={26} />
          <div>
            <div className="fleet-map-eyebrow">
              ANDROID ENTERPRISE · CENTRO DE GEOLOCALIZACIÓN
            </div>
            <h1>Mapa de Flota</h1>
            <p>
              Monitoreo periódico de posiciones reportadas por dispositivos
              Android mediante el flujo empresarial autorizado.
            </p>
          </div>
        </div>

        <div className="fleet-map-hero__actions">
          <span
            className="fleet-maps-status fleet-maps-status--ready"
            title={provider.productionGuidance}
          >
            <Radio size={13} />
            {provider.name}: activo
          </span>
          <button
            type="button"
            className="fleet-btn fleet-btn--ghost"
            onClick={() => void loadFleet('refresh')}
            disabled={loading || refreshing}
          >
            <RefreshCw
              size={16}
              className={loading || refreshing ? 'spin' : ''}
            />
            Actualizar
          </button>
        </div>
      </header>

      <section className="fleet-kpi-grid" aria-label="Indicadores de flota">
        {kpis.map(kpi => {
          const Icon = kpi.icon

          return (
            <article
              key={kpi.key}
              className={`fleet-kpi fleet-kpi--${kpi.tone}`}
            >
              <span className="fleet-kpi__icon">
                <Icon size={18} />
              </span>
              <span className="fleet-kpi__value">{kpi.value}</span>
              <span className="fleet-kpi__label">{kpi.label}</span>
            </article>
          )
        })}
      </section>

      <section className="fleet-map-toolbar" aria-label="Filtros de flota">
        <div className="fleet-filter-lead">
          <Search size={16} />
          <input
            type="search"
            aria-label="Buscar dispositivo"
            placeholder="Buscar por nombre o fabricante..."
            value={filters.search ?? ''}
            onChange={event =>
              handleFilterChange('search', event.target.value)
            }
          />
        </div>

        <label className="fleet-field">
          <span>Fabricante</span>
          <input
            type="text"
            placeholder="Todos"
            value={filters.manufacturer ?? ''}
            onChange={event =>
              handleFilterChange('manufacturer', event.target.value)
            }
          />
        </label>

        <label className="fleet-field">
          <span>Localidad / Sitio</span>
          <select
            value={filters.siteId ?? ''}
            onChange={event =>
              handleFilterChange('siteId', event.target.value)
            }
          >
            <option value="">Todos</option>
            {siteOptions.map(site => (
              <option key={site.id} value={site.id}>
                {site.name}
              </option>
            ))}
          </select>
        </label>

        <label className="fleet-field">
          <span>Departamento</span>
          <input
            type="text"
            placeholder="Todos"
            value={filters.department ?? ''}
            onChange={event =>
              handleFilterChange('department', event.target.value)
            }
          />
        </label>

        <label className="fleet-field">
          <span>Estado de ubicación</span>
          <select
            aria-label="Estado de ubicación"
            value={filters.locationState ?? ''}
            onChange={event =>
              handleFilterChange('locationState', event.target.value)
            }
          >
            <option value="">Todos los estados</option>
            {(
              Object.keys(LOCATION_STATE_LABELS) as FleetLocationState[]
            ).map(state => (
              <option key={state} value={state}>
                {LOCATION_STATE_LABELS[state]}
              </option>
            ))}
          </select>
        </label>

        <label className="fleet-field">
          <span>Última comunicación (min)</span>
          <input
            type="number"
            min={1}
            placeholder="Sin límite"
            value={filters.maxSignalAgeMinutes ?? ''}
            onChange={event =>
              handleFilterChange(
                'maxSignalAgeMinutes',
                event.target.value
                  ? Number.parseInt(event.target.value, 10)
                  : undefined,
              )
            }
          />
        </label>
      </section>

      <main className="fleet-map-container">
        <div className="fleet-map-split">
          <aside className="fleet-device-list" aria-label="Dispositivos Android">
            <div className="fleet-list-header">
              <List size={16} />
              <span>{locations.length} dispositivos Android</span>
              {refreshing && (
                <RefreshCw size={13} className="spin fleet-list-refresh" />
              )}
            </div>

            {loading ? (
              <div className="fleet-list-empty">Cargando dispositivos...</div>
            ) : locations.length === 0 ? (
              <div className="fleet-list-empty">{emptyMessage}</div>
            ) : (
              <ul>
                {locations.map(location => (
                  <li
                    key={location.deviceId}
                    className={
                      selectedDeviceId === location.deviceId
                        ? 'selected'
                        : undefined
                    }
                  >
                    <button
                      type="button"
                      onClick={() => setSelectedDeviceId(location.deviceId)}
                    >
                      <span
                        className="fleet-state-dot"
                        style={{ backgroundColor: markerColorFor(location) }}
                      />
                      <span className="fleet-device-name">
                        {location.deviceName}
                      </span>
                      <span className="fleet-device-state">
                        {LOCATION_STATE_LABELS[location.locationState]}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </aside>

          <div className="fleet-map-viewport">
            <MapContainer
              center={DEFAULT_CENTER}
              zoom={11}
              className="fleet-map-canvas"
              scrollWheelZoom
            >
              <TileLayer
                url={provider.url}
                attribution={provider.attribution}
                maxZoom={provider.maxZoom}
              />
              <MapController center={selectedCenter} bounds={fleetBounds} />
              {positionedLocations.map(location => (
                <CircleMarker
                  key={location.deviceId}
                  center={[location.latitude, location.longitude]}
                  radius={9}
                  pathOptions={{
                    color: '#ffffff',
                    weight: 2,
                    fillColor: markerColorFor(location),
                    fillOpacity: 0.92,
                  }}
                  eventHandlers={{
                    click: () => setSelectedDeviceId(location.deviceId),
                  }}
                />
              ))}
            </MapContainer>

            {positionedLocations.length === 0 && !loading && (
              <div className="fleet-map-no-positions" role="status">
                <MapPinOff size={18} />
                La flota Android aún no ha reportado coordenadas válidas.
              </div>
            )}

            <div className="fleet-tile-guidance">
              <Info size={13} />
              OSM público para uso moderado. Configure teselas corporativas o
              un proveedor con SLA antes de operar una flota en producción.
            </div>

            {error && (
              <div className="fleet-map-toast" role="alert">
                <AlertTriangle size={15} />
                {error}
              </div>
            )}

            {lastUpdatedAt && (
              <div className="fleet-map-updated">
                <CheckCircle2 size={13} />
                Actualizado {lastUpdatedAt.toLocaleTimeString()} · monitoreo
                periódico (60 s)
              </div>
            )}

            {selectedDevice && (
              <div className="fleet-device-card">
                <button
                  type="button"
                  className="fleet-device-card__close"
                  aria-label="Cerrar detalle"
                  onClick={() => setSelectedDeviceId(null)}
                >
                  ×
                </button>

                <div className="fleet-device-card__head">
                  <MapPin
                    size={20}
                    color={markerColorFor(selectedDevice)}
                  />
                  <h3>{selectedDevice.deviceName}</h3>
                </div>

                <div className="fleet-device-card__body">
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Ubicación:</span>
                    <span
                      className={`fleet-location-pill ${selectedDevice.locationState.toLowerCase()}`}
                    >
                      {LOCATION_STATE_LABELS[selectedDevice.locationState]}
                    </span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Conectividad:</span>
                    <span>{selectedDevice.status}</span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Fabricante:</span>
                    <span>{selectedDevice.manufacturer || 'N/A'}</span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Sitio:</span>
                    <span>{selectedDevice.siteName || 'N/A'}</span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Última posición:</span>
                    <span>{formatCapturedAt(selectedDevice.capturedAtUtc)}</span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">Edad de señal:</span>
                    <span>
                      {selectedDevice.signalAgeMinutes !== null
                        ? `${Math.round(selectedDevice.signalAgeMinutes)} min`
                        : 'N/D'}
                    </span>
                  </div>

                  {!hasValidPosition(selectedDevice) && (
                    <div className="fleet-no-location-note">
                      Este dispositivo no ha reportado coordenadas válidas. No
                      se crea un marcador ni se estima su posición.
                    </div>
                  )}
                </div>

                <div className="fleet-device-card__footer">
                  <button
                    type="button"
                    className="fleet-btn fleet-btn--primary"
                    onClick={() =>
                      navigate(
                        `/devices/${selectedDevice.deviceId}?workspace=android&platform=Android`,
                      )
                    }
                  >
                    Ver ficha Android
                    <ExternalLink size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}
