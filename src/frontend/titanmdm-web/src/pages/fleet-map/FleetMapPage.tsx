import {
  useEffect,
  useState,
  useCallback,
  useRef,
} from 'react'
import {
  MapPin,
  Search,
  AlertTriangle,
  ExternalLink,
  RefreshCw,
  Map as MapIcon,
  List,
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

const LOCATION_STATE_COLORS: Record<
  FleetLocationState,
  string
> = {
  Fresh: '#22c55e',
  Stale: '#facc15',
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

type LoadMode = 'initial' | 'refresh' | 'poll'

export function FleetMapPage() {
  const navigate = useNavigate()
  const mapRef = useRef<MapsMap | null>(null)
  const markersRef = useRef<MapsMarker[]>([])
  const mapNodeRef = useRef<HTMLDivElement | null>(
    null,
  )

  const [mapsConfigured] = useState(
    isGoogleMapsConfigured(),
  )
  const [mapsLoadFailed, setMapsLoadFailed] =
    useState(false)
  const [mapsSdkReady, setMapsSdkReady] =
    useState(false)

  const [locations, setLocations] = useState<
    FleetLocation[]
  >([])
  const [loading, setLoading] = useState(
    isGoogleMapsConfigured(),
  )
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(
    null,
  )

  const [filters, setFilters] =
    useState<FleetLocationFilters>(EMPTY_FILTERS)

  const [selectedDevice, setSelectedDevice] =
    useState<FleetLocation | null>(null)

  const loadFleet = useCallback(
    async (mode: LoadMode) => {
      if (mode === 'refresh' || mode === 'poll') {
        setRefreshing(true)
      }

      setError(null)

      try {
        const data = await locationApi.getFleet(
          filters,
        )
        setLocations(data)
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
   * Polling acotado: solo mientras la pagina esta
   * visible.
   */
  useEffect(() => {
    if (!mapsConfigured) {
      return
    }

    const interval = window.setInterval(() => {
      if (document.visibilityState === 'visible') {
        void loadFleet('poll')
      }
    }, POLL_INTERVAL_MS)

    return () => window.clearInterval(interval)
  }, [loadFleet, mapsConfigured])

  /*
   * Carga perezosa del SDK de Google Maps.
   * Sin clave configurada se muestra el estado
   * "Configuracion pendiente".
   */
  useEffect(() => {
    if (!mapsConfigured) {
      return
    }

    let cancelled = false

    void loadGoogleMaps().then((loaded) => {
      if (!cancelled && loaded) {
        setMapsSdkReady(true)
      }

      if (!cancelled && !loaded) {
        setMapsLoadFailed(true)
      }
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
      !mapsSdkReady ||
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
          setError('Error al inicializar el mapa.')
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
      .filter(
        (loc) =>
          loc.latitude !== null &&
          loc.longitude !== null,
      )
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
            scale: 8,
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
  }, [locations, mapsSdkReady])

  const handleFilterChange = (
    key: keyof FleetLocationFilters,
    value: string | number | undefined,
  ) => {
    setFilters((prev) => ({ ...prev, [key]: value }))
  }

  const stateCounts = locations.reduce<
    Record<string, number>
  >(
    (acc, loc) => {
      acc[loc.locationState] =
        (acc[loc.locationState] ?? 0) + 1
      return acc
    },
    {},
  )

  const withCoordinates = locations.filter(
    (loc) =>
      loc.latitude !== null && loc.longitude !== null,
  ).length

  return (
    <div className="fleet-map-page">
      <header className="fleet-map-header">
        <div className="header-title">
          <MapIcon size={24} />
          <div>
            <h1>Centro de Geolocalización</h1>
            <p>
              Monitoreo de flota Android — las
              posiciones se muestran solo cuando el
              dispositivo las reporta
            </p>
          </div>
        </div>

        <div className="header-actions">
          <button
            className="btn-refresh"
            onClick={() =>
              void loadFleet('refresh')
            }
            disabled={loading || refreshing}
          >
            <RefreshCw
              size={16}
              className={loading ? 'spin' : ''}
            />
            Actualizar
          </button>
        </div>
      </header>

      <section className="fleet-map-toolbar">
        <div className="filter-group">
          <div className="input-with-icon">
            <Search size={16} />
            <input
              type="text"
              placeholder="Buscar dispositivo..."
              value={filters.search || ''}
              onChange={(e) =>
                handleFilterChange(
                  'search',
                  e.target.value,
                )
              }
            />
          </div>

          <div className="input-with-icon">
            <input
              type="text"
              placeholder="Fabricante"
              value={filters.manufacturer || ''}
              onChange={(e) =>
                handleFilterChange(
                  'manufacturer',
                  e.target.value,
                )
              }
            />
          </div>

          <div className="input-with-icon">
            <input
              type="text"
              placeholder="Departamento"
              value={filters.department || ''}
              onChange={(e) =>
                handleFilterChange(
                  'department',
                  e.target.value,
                )
              }
            />
          </div>

          <div className="input-with-icon">
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
              <option value="">
                Todos los estados
              </option>
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
          </div>

          <div className="input-with-icon">
            <span className="label-small">
              Máx. Edad (min)
            </span>
            <input
              type="number"
              min={1}
              value={
                filters.maxSignalAgeMinutes ?? ''
              }
              onChange={(e) =>
                handleFilterChange(
                  'maxSignalAgeMinutes',
                  e.target.value
                    ? parseInt(e.target.value, 10)
                    : undefined,
                )
              }
            />
          </div>
        </div>

        <div className="fleet-stats">
          <div className="stat-item">
            <span className="stat-value">
              {locations.length}
            </span>
            <span className="stat-label">
              Dispositivos
            </span>
          </div>
          <div className="stat-item">
            <span className="stat-value">
              {withCoordinates}
            </span>
            <span className="stat-label">
              Con posición
            </span>
          </div>
          {Object.entries(stateCounts).map(
            ([state, count]) => (
              <div
                key={state}
                className="stat-item state"
              >
                <span
                  className="stat-dot"
                  style={{
                    backgroundColor:
                      LOCATION_STATE_COLORS[
                        state as FleetLocationState
                      ] ?? '#94a3b8',
                  }}
                />
                <span className="stat-value">
                  {count}
                </span>
                <span className="stat-label">
                  {LOCATION_STATE_LABELS[
                    state as FleetLocationState
                  ] ?? state}
                </span>
              </div>
            ),
          )}
        </div>
      </section>

      <main className="fleet-map-container">
        {!mapsConfigured ? (
          <div className="map-error-state">
            <AlertTriangle size={48} />
            <h2>Configuración Pendiente</h2>
            <p>
              Falta la clave de Google Maps API.
            </p>
            <div className="error-hint">
              Configure la variable de entorno
              <code>
                {' '}
                VITE_GOOGLE_MAPS_API_KEY{' '}
              </code>
              en el frontend y reinicie el build.
            </div>
          </div>
        ) : mapsLoadFailed || error ? (
          <div className="map-error-state">
            <AlertTriangle size={48} />
            <h2>Error</h2>
            <p>
              {mapsLoadFailed
                ? 'No se pudo cargar Google Maps. Verifique la clave API.'
                : error}
            </p>
          </div>
        ) : loading ? (
          <div className="map-loading-state">
            <RefreshCw size={48} className="spin" />
            <p>Cargando flota...</p>
          </div>
        ) : (
          <div className="fleet-map-split">
            <aside className="fleet-device-list">
              <div className="list-header">
                <List size={16} />
                <span>
                  {locations.length} dispositivos
                </span>
              </div>

              {locations.length === 0 ? (
                <div className="list-empty">
                  No hay dispositivos que coincidan
                  con los filtros.
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
                          : ''
                      }
                    >
                      <button
                        type="button"
                        onClick={() => {
                          setSelectedDevice(loc)
                          if (
                            loc.latitude !== null &&
                            loc.longitude !== null
                          ) {
                            mapRef.current?.panTo({
                              lat: loc.latitude,
                              lng: loc.longitude,
                            })
                          }
                        }}
                      >
                        <span
                          className="state-dot"
                          style={{
                            backgroundColor:
                              markerColorFor(loc),
                          }}
                        />
                        <span className="device-name">
                          {loc.deviceName}
                        </span>
                        <span className="device-state">
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

            <div className="map-viewport">
              <div
                id="google-map"
                ref={mapNodeRef}
                style={{
                  width: '100%',
                  height: '100%',
                }}
              />

              {selectedDevice && (
                <div className="device-info-card">
                  <button
                    className="close-card"
                    onClick={() =>
                      setSelectedDevice(null)
                    }
                  >
                    ×
                  </button>
                  <div className="card-header">
                    <MapPin
                      size={20}
                      color={markerColorFor(
                        selectedDevice,
                      )}
                    />
                    <h3>
                      {selectedDevice.deviceName}
                    </h3>
                  </div>
                  <div className="card-body">
                    <div className="info-row">
                      <span className="label">
                        Estado:
                      </span>
                      <span
                        className={`location-state-pill ${selectedDevice.locationState.toLowerCase()}`}
                      >
                        {LOCATION_STATE_LABELS[
                          selectedDevice
                            .locationState
                        ] ??
                          selectedDevice.locationState}
                      </span>
                    </div>
                    <div className="info-row">
                      <span className="label">
                        Plataforma:
                      </span>
                      <span
                        className={`status-pill ${selectedDevice.status.toLowerCase()}`}
                      >
                        {selectedDevice.status}
                      </span>
                    </div>
                    <div className="info-row">
                      <span className="label">
                        Fabricante:
                      </span>
                      <span>
                        {selectedDevice.manufacturer ||
                          'N/A'}
                      </span>
                    </div>
                    <div className="info-row">
                      <span className="label">
                        Sitio:
                      </span>
                      <span>
                        {selectedDevice.siteName ||
                          'N/A'}
                      </span>
                    </div>
                    <div className="info-row">
                      <span className="label">
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
                    <div className="info-row">
                      <span className="label">
                        Edad de señal:
                      </span>
                      <span>
                        {selectedDevice.signalAgeMinutes !==
                        null
                          ? `${Math.round(selectedDevice.signalAgeMinutes)} min`
                          : 'N/D'}
                      </span>
                    </div>
                    {selectedDevice.latitude ===
                      null && (
                      <div className="no-location-note">
                        Este dispositivo no ha
                        reportado coordenadas. No se
                        muestra una posición estimada.
                      </div>
                    )}
                  </div>
                  <div className="card-footer">
                    <button
                      className="btn-view-detail"
                      onClick={() =>
                        navigate(
                          `/devices/${selectedDevice.deviceId}`,
                        )
                      }
                    >
                      Ver Ficha de Equipo
                      <ExternalLink size={14} />
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </main>
    </div>
  )
}
