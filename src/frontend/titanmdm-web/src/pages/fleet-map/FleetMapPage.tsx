import {
  useEffect,
  useState,
  useCallback,
  useMemo,
  useRef,
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
  locationApi,
  type FleetLocation,
  type FleetLocationFilters,
  type FleetLocationState,
} from '../../api/locationApi'
import {
  isGoogleMapsConfigured,
  loadGoogleMaps,
} from '../../config/googleMaps'
import { useNavigate } from 'react-router-dom'
import './FleetMapPage.css'

/*
 * Tipado minimo de la API de Google Maps.
 * Evita la dependencia de @types/google.maps.
 */
interface MapsLatLng {
  lat: number
  lng: number
}

interface MapsMarker {
  setMap(map: MapsMap | null): void
  addListener(
    event: string,
    handler: () => void,
  ): void
}

interface MapsMap {
  panTo(position: MapsLatLng): void
  fitBounds(bounds: MapsLatLngBounds): void
}

interface MapsLatLngBounds {
  extend(position: MapsLatLng): void
}

interface MapsFactory {
  Map: new (
    element: HTMLElement,
    options: Record<string, unknown>,
  ) => MapsMap
  Marker: new (
    options: Record<string, unknown>,
  ) => MapsMarker
  LatLngBounds: new () => MapsLatLngBounds
  SymbolPath: { CIRCLE: number }
}

declare global {
  interface Window {
    google?: {
      maps?: MapsFactory
    }
  }
}

const POLL_INTERVAL_MS = 60_000

type MapsStatus =
  | 'unconfigured'
  | 'loading'
  | 'ready'
  | 'error'

type LoadMode = 'initial' | 'refresh' | 'poll'

const LOCATION_STATE_COLORS: Record<
  FleetLocationState,
  string
> = {
  Fresh: '#16a34a',
  Stale: '#f59e0b',
  Offline: '#94a3b8',
  NoLocation: '#64748b',
  PermissionDenied: '#ef4444',
}

const LOCATION_STATE_LABELS: Record<
  FleetLocationState,
  string
> = {
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

function markerColorFor(
  location: FleetLocation,
): string {
  return (
    LOCATION_STATE_COLORS[location.locationState] ??
    '#94a3b8'
  )
}

function hasPosition(location: FleetLocation): boolean {
  return (
    location.latitude !== null &&
    location.longitude !== null
  )
}

export function FleetMapPage() {
  const navigate = useNavigate()
  const mapRef = useRef<MapsMap | null>(null)
  const markersRef = useRef<MapsMarker[]>([])
  const mapNodeRef = useRef<HTMLDivElement | null>(
    null,
  )

  const [mapsConfigured] = useState(
    isGoogleMapsConfigured,
  )
  const [mapsStatus, setMapsStatus] =
    useState<MapsStatus>(() =>
      isGoogleMapsConfigured()
        ? 'loading'
        : 'unconfigured',
    )

  const [locations, setLocations] = useState<
    FleetLocation[]
  >([])
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(
    null,
  )
  const [lastUpdatedAt, setLastUpdatedAt] =
    useState<Date | null>(null)

  const [filters, setFilters] =
    useState<FleetLocationFilters>(EMPTY_FILTERS)

  const [selectedDevice, setSelectedDevice] =
    useState<FleetLocation | null>(null)

  const loadFleet = useCallback(
    async (mode: LoadMode) => {
      if (mode === 'refresh' || mode === 'poll') {
        setRefreshing(true)
      }

      if (mode !== 'poll') {
        setError(null)
      }

      try {
        const data = await locationApi.getFleet(
          filters,
        )
        setLocations(data)
        setLastUpdatedAt(new Date())
      } catch (err) {
        console.error(
          'Error loading fleet locations:',
          err,
        )
        setError(
          'No fue posible cargar la ubicación de la flota.',
        )
      } finally {
        setLoading(false)
        setRefreshing(false)
      }
    },
    [filters],
  )

  /*
   * Carga inicial y recarga por cambio de filtros.
   * Se difiere al siguiente tick para no ejecutar
   * setState sincrono dentro del effect.
   */
  useEffect(() => {
    let cancelled = false

    const timer = window.setTimeout(() => {
      if (!cancelled) {
        void loadFleet('initial')
      }
    }, 0)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [loadFleet])

  /*
   * Monitoreo periodico acotado: solo mientras la
   * pagina esta visible. No es seguimiento continuo:
   * refleja lo ultimo reportado por el agente.
   */
  useEffect(() => {
    const interval = window.setInterval(() => {
      if (document.visibilityState === 'visible') {
        void loadFleet('poll')
      }
    }, POLL_INTERVAL_MS)

    return () => window.clearInterval(interval)
  }, [loadFleet])

  /*
   * Carga perezosa del SDK de Google Maps.
   */
  useEffect(() => {
    if (!mapsConfigured) {
      return
    }

    let cancelled = false

    void loadGoogleMaps().then((loaded) => {
      if (cancelled) {
        return
      }

      setMapsStatus(loaded ? 'ready' : 'error')
    })

    return () => {
      cancelled = true
    }
  }, [mapsConfigured])

  /*
   * Inicializacion perezosa del mapa + marcadores
   * en un unico effect: el mapa se crea la primera
   * vez que hay un nodo y el SDK esta listo.
   */
  useEffect(() => {
    const maps = window.google?.maps

    if (
      !maps ||
      mapsStatus !== 'ready' ||
      !mapNodeRef.current
    ) {
      return
    }

    if (!mapRef.current) {
      try {
        mapRef.current = new maps.Map(
          mapNodeRef.current,
          {
            center: { lat: 18.4861, lng: -69.9312 },
            zoom: 11,
            disableDefaultUI: false,
            zoomControl: true,
            mapTypeControl: false,
            streetViewControl: false,
            fullscreenControl: false,
          },
        )
      } catch (e) {
        console.error(
          'Map initialization failed:',
          e,
        )
        window.setTimeout(() => {
          setMapsStatus('error')
        }, 0)
        return
      }
    }

    const map = mapRef.current

    markersRef.current.forEach((m) =>
      m.setMap(null),
    )
    markersRef.current = []

    const bounds = new maps.LatLngBounds()
    let hasLocations = false

    locations
      .filter(hasPosition)
      .forEach((loc) => {
        const position: MapsLatLng = {
          lat: loc.latitude as number,
          lng: loc.longitude as number,
        }

        const marker = new maps.Marker({
          position,
          map,
          title: loc.deviceName,
          icon: {
            path: maps.SymbolPath.CIRCLE,
            fillColor: markerColorFor(loc),
            fillOpacity: 0.9,
            strokeWeight: 2,
            strokeColor: '#fff',
            scale: 9,
          },
        })

        marker.addListener('click', () => {
          setSelectedDevice(loc)
          map.panTo(position)
        })

        markersRef.current.push(marker)
        bounds.extend(position)
        hasLocations = true
      })

    if (hasLocations) {
      map.fitBounds(bounds)
    }
  }, [locations, mapsStatus])

  const handleFilterChange = (
    key: keyof FleetLocationFilters,
    value: string | number | undefined,
  ) => {
    setFilters((prev) => ({ ...prev, [key]: value }))
  }

  const stateCounts = useMemo(
    () =>
      locations.reduce<Record<string, number>>(
        (acc, loc) => {
          acc[loc.locationState] =
            (acc[loc.locationState] ?? 0) + 1
          return acc
        },
        {},
      ),
    [locations],
  )

  const withCoordinates = useMemo(
    () => locations.filter(hasPosition).length,
    [locations],
  )

  const siteOptions = useMemo(() => {
    const map = new Map<string, string>()

    locations.forEach((loc) => {
      if (loc.siteId && loc.siteName) {
        map.set(loc.siteId, loc.siteName)
      }
    })

    return Array.from(map.entries()).map(
      ([id, name]) => ({ id, name }),
    )
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
      label: 'Dispositivos',
      value: locations.length,
      tone: 'neutral',
      icon: Smartphone,
    },
    {
      key: 'position',
      label: 'Con posición',
      value: withCoordinates,
      tone: 'info',
      icon: Navigation,
    },
    {
      key: 'fresh',
      label: 'Recientes',
      value: stateCounts.Fresh ?? 0,
      tone: 'ok',
      icon: Activity,
    },
    {
      key: 'stale',
      label: 'Antiguas',
      value: stateCounts.Stale ?? 0,
      tone: 'warn',
      icon: Clock3,
    },
    {
      key: 'noloc',
      label: 'Sin ubicación',
      value: stateCounts.NoLocation ?? 0,
      tone: 'muted',
      icon: MapPinOff,
    },
    {
      key: 'denied',
      label: 'Permiso denegado',
      value: stateCounts.PermissionDenied ?? 0,
      tone: 'danger',
      icon: ShieldAlert,
    },
  ]

  const mapsStatusLabel: Record<MapsStatus, string> = {
    unconfigured: 'Google Maps: sin configurar',
    loading: 'Google Maps: cargando',
    ready: 'Google Maps: listo',
    error: 'Google Maps: error',
  }

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
              Monitoreo periódico de la flota Android. Las
              posiciones se muestran solo cuando el
              dispositivo las reporta mediante el flujo
              autorizado.
            </p>
          </div>
        </div>

        <div className="fleet-map-hero__actions">
          <span
            className={`fleet-maps-status fleet-maps-status--${mapsStatus}`}
          >
            <Radio size={13} />
            {mapsStatusLabel[mapsStatus]}
          </span>
          <button
            type="button"
            className="fleet-btn fleet-btn--ghost"
            onClick={() => void loadFleet('refresh')}
            disabled={loading || refreshing}
          >
            <RefreshCw
              size={16}
              className={
                loading || refreshing ? 'spin' : ''
              }
            />
            Actualizar
          </button>
        </div>
      </header>

      <section
        className="fleet-kpi-grid"
        aria-label="Indicadores de flota"
      >
        {kpis.map((kpi) => {
          const Icon = kpi.icon

          return (
            <article
              key={kpi.key}
              className={`fleet-kpi fleet-kpi--${kpi.tone}`}
            >
              <span className="fleet-kpi__icon">
                <Icon size={18} />
              </span>
              <span className="fleet-kpi__value">
                {kpi.value}
              </span>
              <span className="fleet-kpi__label">
                {kpi.label}
              </span>
            </article>
          )
        })}
      </section>

      <section
        className="fleet-map-toolbar"
        aria-label="Filtros de flota"
      >
        <div className="fleet-filter-lead">
          <Search size={16} />
          <input
            type="search"
            aria-label="Buscar dispositivo"
            placeholder="Buscar por nombre o serie..."
            value={filters.search || ''}
            onChange={(e) =>
              handleFilterChange(
                'search',
                e.target.value,
              )
            }
          />
        </div>

        <label className="fleet-field">
          <span>Fabricante</span>
          <input
            type="text"
            placeholder="Todos"
            value={filters.manufacturer || ''}
            onChange={(e) =>
              handleFilterChange(
                'manufacturer',
                e.target.value,
              )
            }
          />
        </label>

        <label className="fleet-field">
          <span>Localidad / Sitio</span>
          <select
            value={filters.siteId || ''}
            onChange={(e) =>
              handleFilterChange(
                'siteId',
                e.target.value,
              )
            }
          >
            <option value="">Todos</option>
            {siteOptions.map((site) => (
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
            value={filters.department || ''}
            onChange={(e) =>
              handleFilterChange(
                'department',
                e.target.value,
              )
            }
          />
        </label>

        <label className="fleet-field">
          <span>Estado de ubicación</span>
          <select
            aria-label="Estado de ubicación"
            value={filters.locationState || ''}
            onChange={(e) =>
              handleFilterChange(
                'locationState',
                e.target.value,
              )
            }
          >
            <option value="">Todos los estados</option>
            {(
              Object.keys(
                LOCATION_STATE_LABELS,
              ) as FleetLocationState[]
            ).map((state) => (
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
            onChange={(e) =>
              handleFilterChange(
                'maxSignalAgeMinutes',
                e.target.value
                  ? parseInt(e.target.value, 10)
                  : undefined,
              )
            }
          />
        </label>
      </section>

      <main className="fleet-map-container">
        <div className="fleet-map-split">
          <aside
            className="fleet-device-list"
            aria-label="Dispositivos"
          >
            <div className="fleet-list-header">
              <List size={16} />
              <span>
                {locations.length} dispositivos
              </span>
              {refreshing && (
                <RefreshCw
                  size={13}
                  className="spin fleet-list-refresh"
                />
              )}
            </div>

            {loading ? (
              <div className="fleet-list-empty">
                Cargando dispositivos...
              </div>
            ) : locations.length === 0 ? (
              <div className="fleet-list-empty">
                No hay dispositivos que coincidan con los
                filtros.
              </div>
            ) : (
              <ul>
                {locations.map((loc) => (
                  <li
                    key={loc.deviceId}
                    className={
                      selectedDevice?.deviceId ===
                      loc.deviceId
                        ? 'selected'
                        : undefined
                    }
                  >
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedDevice(loc)
                        if (hasPosition(loc)) {
                          mapRef.current?.panTo({
                            lat: loc.latitude as number,
                            lng: loc.longitude as number,
                          })
                        }
                      }}
                    >
                      <span
                        className="fleet-state-dot"
                        style={{
                          backgroundColor:
                            markerColorFor(loc),
                        }}
                      />
                      <span className="fleet-device-name">
                        {loc.deviceName}
                      </span>
                      <span className="fleet-device-state">
                        {LOCATION_STATE_LABELS[
                          loc.locationState
                        ] ?? loc.locationState}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </aside>

          <div className="fleet-map-viewport">
            {mapsStatus === 'ready' ? (
              <div
                id="google-map"
                ref={mapNodeRef}
                className="fleet-map-canvas"
              />
            ) : mapsStatus === 'loading' ? (
              <div className="fleet-map-state">
                <RefreshCw size={40} className="spin" />
                <p>Cargando Google Maps...</p>
              </div>
            ) : mapsStatus === 'error' ? (
              <div className="fleet-map-state fleet-map-state--error">
                <AlertTriangle size={40} />
                <h2>Error de Google Maps</h2>
                <p>
                  No se pudo cargar el SDK de Google Maps.
                  Verifique que la clave API sea válida y
                  esté habilitada para Maps JavaScript API.
                </p>
              </div>
            ) : (
              <div className="fleet-map-state fleet-map-state--config">
                <AlertTriangle size={40} />
                <h2>Configuración Pendiente</h2>
                <p>Falta la clave de Google Maps API.</p>
                <div className="fleet-config-hint">
                  <p>
                    Configure la variable de entorno{' '}
                    <code>VITE_GOOGLE_MAPS_API_KEY</code>{' '}
                    y reinicie el build del frontend.
                  </p>
                  <ol>
                    <li>
                      Cree el archivo{' '}
                      <code>.env</code> en{' '}
                      <code>src/frontend/titanmdm-web</code>.
                    </li>
                    <li>
                      Añada{' '}
                      <code>
                        VITE_GOOGLE_MAPS_API_KEY=&lt;su-clave&gt;
                      </code>
                      .
                    </li>
                    <li>
                      Habilite <em>Maps JavaScript API</em> en
                      Google Cloud Console.
                    </li>
                    <li>
                      Reinicie{' '}
                      <code>npm run dev</code> o vuelva a
                      construir.
                    </li>
                  </ol>
                  <p className="fleet-config-note">
                    <Info size={14} />
                    La lista de dispositivos y los
                    indicadores siguen disponibles sin la
                    clave. No se muestran posiciones
                    estimadas.
                  </p>
                </div>
              </div>
            )}

            {error && (
              <div className="fleet-map-toast" role="alert">
                <AlertTriangle size={15} />
                {error}
              </div>
            )}

            {mapsStatus === 'ready' && lastUpdatedAt && (
              <div className="fleet-map-updated">
                <CheckCircle2 size={13} />
                Actualizado{' '}
                {lastUpdatedAt.toLocaleTimeString()} ·
                monitoreo periódico (60 s)
              </div>
            )}

            {selectedDevice && mapsStatus === 'ready' && (
              <div className="fleet-device-card">
                <button
                  type="button"
                  className="fleet-device-card__close"
                  aria-label="Cerrar detalle"
                  onClick={() => setSelectedDevice(null)}
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
                    <span className="fleet-info-label">
                      Estado:
                    </span>
                    <span
                      className={`fleet-location-pill ${selectedDevice.locationState.toLowerCase()}`}
                    >
                      {LOCATION_STATE_LABELS[
                        selectedDevice.locationState
                      ] ?? selectedDevice.locationState}
                    </span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">
                      Plataforma:
                    </span>
                    <span>{selectedDevice.status}</span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">
                      Fabricante:
                    </span>
                    <span>
                      {selectedDevice.manufacturer || 'N/A'}
                    </span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">
                      Sitio:
                    </span>
                    <span>
                      {selectedDevice.siteName || 'N/A'}
                    </span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">
                      Última señal:
                    </span>
                    <span>
                      {selectedDevice.capturedAtUtc
                        ? new Date(
                            selectedDevice.capturedAtUtc,
                          ).toLocaleString()
                        : 'Sin datos'}
                    </span>
                  </div>
                  <div className="fleet-info-row">
                    <span className="fleet-info-label">
                      Edad de señal:
                    </span>
                    <span>
                      {selectedDevice.signalAgeMinutes !==
                      null
                        ? `${Math.round(selectedDevice.signalAgeMinutes)} min`
                        : 'N/D'}
                    </span>
                  </div>

                  {!hasPosition(selectedDevice) && (
                    <div className="fleet-no-location-note">
                      Este dispositivo no ha reportado
                      coordenadas. No se muestra una
                      posición estimada.
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
