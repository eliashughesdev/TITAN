import {
  ChevronLeft,
  ChevronRight,
  Crosshair,
  MapPin,
  MapPinned,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  Smartphone,
  Trash2,
  Users,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

import {
  Circle,
  MapContainer,
  Marker,
  TileLayer,
  useMap,
  useMapEvents,
} from 'react-leaflet'

import L from 'leaflet'

import {
  locationApi,
  type Geofence,
} from '../../api/locationApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../types/device'

import { deviceDisplayName } from '../devices/components/deviceAlias'

import { useAuth } from '../../auth/AuthContext'
import { Permissions } from '../../auth/permissions'
import { getActiveProvider } from '../../config/mapConfig'
import 'leaflet/dist/leaflet.css'
import './GeofencingPage.css'

const DEVICES_PAGE_SIZE = 50
const SANTO_DOMINGO_CENTER: [number, number] = [
  18.4861,
  -69.9312,
]
const MIN_RADIUS_METERS = 25
const MAX_RADIUS_METERS = 100000

const CENTER_ICON = L.divIcon({
  className: 'geo-editor-marker',
  html: '<span class="geo-editor-marker__dot geo-editor-marker__dot--center"></span>',
  iconSize: [20, 20],
  iconAnchor: [10, 10],
})

const RADIUS_ICON = L.divIcon({
  className: 'geo-editor-marker',
  html: '<span class="geo-editor-marker__dot geo-editor-marker__dot--radius"></span>',
  iconSize: [18, 18],
  iconAnchor: [9, 9],
})

function parseCoordinate(
  value: string,
  minimum: number,
  maximum: number,
): number | null {
  if (value.trim() === '') return null

  const parsed = Number(value)

  return Number.isFinite(parsed) &&
    parsed >= minimum &&
    parsed <= maximum
    ? parsed
    : null
}

function parseRadius(value: string): number | null {
  const parsed = Number(value)

  return Number.isFinite(parsed) &&
    parsed >= MIN_RADIUS_METERS &&
    parsed <= MAX_RADIUS_METERS
    ? parsed
    : null
}

function clampRadius(value: number): number {
  return Math.min(
    MAX_RADIUS_METERS,
    Math.max(
      MIN_RADIUS_METERS,
      Math.round(value),
    ),
  )
}

function distanceMeters(
  from: [number, number],
  to: [number, number],
): number {
  const earthRadiusMeters = 6371000
  const toRadians = (value: number) =>
    (value * Math.PI) / 180

  const latitudeDelta = toRadians(to[0] - from[0])
  const longitudeDelta = toRadians(to[1] - from[1])
  const fromLatitude = toRadians(from[0])
  const toLatitude = toRadians(to[0])

  const haversine =
    Math.sin(latitudeDelta / 2) ** 2 +
    Math.cos(fromLatitude) *
      Math.cos(toLatitude) *
      Math.sin(longitudeDelta / 2) ** 2

  return (
    2 *
    earthRadiusMeters *
    Math.asin(Math.min(1, Math.sqrt(haversine)))
  )
}

function radiusHandlePosition(
  center: [number, number],
  radiusMeters: number,
): [number, number] {
  const metersPerDegreeLongitude =
    111320 *
    Math.max(
      Math.cos((center[0] * Math.PI) / 180),
      0.01,
    )

  return [
    center[0],
    center[1] + radiusMeters / metersPerDegreeLongitude,
  ]
}

function MapEventsHandler({
  onMapClick,
}: {
  onMapClick: (lat: number, lng: number) => void
}) {
  useMapEvents({
    click: (event) => {
      onMapClick(event.latlng.lat, event.latlng.lng)
    },
  })

  return null
}

function MapController({
  center,
}: {
  center: [number, number] | null
}) {
  const map = useMap()
  const latitude = center?.[0]
  const longitude = center?.[1]

  useEffect(() => {
    if (
      latitude === undefined ||
      longitude === undefined
    ) {
      return
    }

    map.setView(
      [latitude, longitude],
      Math.max(map.getZoom(), 13),
    )
  }, [latitude, longitude, map])

  return null
}

export function GeofencingPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(
    Permissions.geofencing.manage,
  )
  const provider = useMemo(
    () => getActiveProvider(),
    [],
  )

  const [geofences, setGeofences] =
    useState<Geofence[]>([])

  const [devices, setDevices] =
    useState<DeviceListItem[]>([])

  const [devicePage, setDevicePage] =
    useState(1)

  const [totalDevices, setTotalDevices] =
    useState(0)

  const [selectedGeofenceId,
    setSelectedGeofenceId] =
    useState<string | null>(null)

  const [selectedDeviceIds,
    setSelectedDeviceIds] =
    useState<Set<string>>(
      new Set(),
    )

  const [name, setName] =
    useState('')

  const [description, setDescription] =
    useState('')

  const [latitude, setLatitude] =
    useState('18.4861')

  const [longitude, setLongitude] =
    useState('-69.9312')

  const [radius, setRadius] =
    useState('250')

  const [alertOnEnter, setAlertOnEnter] =
    useState(true)

  const [alertOnExit, setAlertOnExit] =
    useState(true)

  const [search, setSearch] =
    useState('')

  const [loading, setLoading] =
    useState(true)

  const [devicesLoading, setDevicesLoading] =
    useState(false)

  const [working, setWorking] =
    useState(false)

  const [message, setMessage] =
    useState<string | null>(null)

  const [error, setError] =
    useState<string | null>(null)

  const deviceRequestId = useRef(0)

  const loadDevices = useCallback(
    async (
      page: number,
      searchValue: string,
    ) => {
      const requestId = ++deviceRequestId.current

      setDevicesLoading(true)

      try {
        const data = await devicesApi.getDevices({
          page,
          pageSize: DEVICES_PAGE_SIZE,
          platform: 'Android',
          search:
            searchValue.trim() || undefined,
        })

        /*
         * Defensa adicional: aunque el backend filtra por
         * Android, nunca mostramos un dispositivo de otra
         * plataforma en Geofencing.
         */
        const androidDevices = data.items.filter(
          (device) => device.platform === 'Android',
        )

        if (requestId !== deviceRequestId.current) {
          return
        }

        setDevices(androidDevices)
        setTotalDevices(data.totalCount)
        setDevicePage(data.page ?? page)
      } catch (loadError) {
        console.error(
          'Error loading Android devices:',
          loadError,
        )

        if (requestId === deviceRequestId.current) {
          setError(
            'No fue posible cargar la lista de dispositivos Android.',
          )
        }
      } finally {
        if (requestId === deviceRequestId.current) {
          setDevicesLoading(false)
        }
      }
    },
    [],
  )

  const reloadGeofences = useCallback(
    async () => {
      const data = await locationApi.getGeofences()
      setGeofences(data)
    },
    [],
  )

  useEffect(() => {
    let active = true

    async function loadGeofences() {
      try {
        setLoading(true)
        setError(null)

        const data =
          await locationApi.getGeofences()

        if (active) {
          setGeofences(data)
        }
      } catch (loadError) {
        console.error(loadError)

        if (active) {
          setError(
            'No fue posible cargar Geofencing.',
          )
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadGeofences()

    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    document.title =
      'Geofencing | TitanMDM'
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(
      () => {
        void loadDevices(
          devicePage,
          search,
        )
      },
      search.trim() ? 250 : 0,
    )

    return () => {
      window.clearTimeout(timer)
    }
  }, [
    devicePage,
    search,
    loadDevices,
  ])

  const selectedGeofence =
    useMemo(
      () =>
        geofences.find(
          (item) =>
            item.id ===
            selectedGeofenceId,
        ) ?? null,
      [
        geofences,
        selectedGeofenceId,
      ],
    )

  const filteredDevices =
    useMemo(() => {
      const value = search
        .trim()
        .toLowerCase()

      if (!value) {
        return devices
      }

      return devices.filter(
        (device) =>
          device.deviceName
            .toLowerCase()
            .includes(value) ||
          (device.alias ?? '')
            .toLowerCase()
            .includes(value) ||
          (device.model ?? '')
            .toLowerCase()
            .includes(value) ||
          device.serialNumber
            .toLowerCase()
            .includes(value) ||
          (
            device.assignedUser ??
            ''
          )
            .toLowerCase()
            .includes(value),
      )
    }, [devices, search])

  const totalAssignments =
    useMemo(
      () =>
        geofences.reduce(
          (total, geofence) =>
            total +
            geofence.assignedDevices,
          0,
        ),
      [geofences],
    )

  const parsedLatitude =
    parseCoordinate(latitude, -90, 90)

  const parsedLongitude =
    parseCoordinate(longitude, -180, 180)

  const parsedRadius =
    parseRadius(radius)

  const mapCenter =
    parsedLatitude !== null &&
    parsedLongitude !== null
      ? ([parsedLatitude, parsedLongitude] as [
          number,
          number,
        ])
      : null

  const radiusHandle =
    mapCenter && parsedRadius !== null
      ? radiusHandlePosition(
          mapCenter,
          parsedRadius,
        )
      : null

  function toggleDevice(
    deviceId: string,
  ) {
    if (!canManage) return

    setSelectedDeviceIds(
      (current) => {
        const next =
          new Set(current)

        if (next.has(deviceId)) {
          next.delete(deviceId)
        } else {
          next.add(deviceId)
        }

        return next
      },
    )
  }

  const handleMapClick = useCallback(
    (lat: number, lng: number) => {
      if (!canManage) return

      setLatitude(lat.toFixed(6))
      setLongitude(lng.toFixed(6))
      setSelectedGeofenceId(null)
    },
    [canManage],
  )

  const handleSelectGeofence =
    useCallback(
      (geofence: Geofence) => {
        setSelectedGeofenceId(
          geofence.id,
        )
        setLatitude(
          geofence.latitude.toFixed(6),
        )
        setLongitude(
          geofence.longitude.toFixed(6),
        )
        setRadius(
          String(
            geofence.radiusMeters,
          ),
        )
      },
      [],
    )

  function handleSearchChange(
    value: string,
  ) {
    setSearch(value)
    setDevicePage(1)
  }

  async function refreshAll() {
    setError(null)
    setLoading(true)

    try {
      await reloadGeofences()
    } catch (refreshError) {
      console.error(refreshError)
      setError(
        'No fue posible actualizar las geocercas.',
      )
    } finally {
      setLoading(false)
    }

    await loadDevices(
      devicePage,
      search,
    )
  }

  async function createGeofence() {
    if (!canManage) {
      setError(
        'Tu rol no tiene permiso para administrar geocercas.',
      )
      return
    }

    if (!name.trim()) {
      setError(
        'Introduce un nombre para la geocerca.',
      )
      return
    }

    if (parsedLatitude === null) {
      setError(
        'La latitud debe estar entre -90 y 90.',
      )
      return
    }

    if (parsedLongitude === null) {
      setError(
        'La longitud debe estar entre -180 y 180.',
      )
      return
    }

    if (parsedRadius === null) {
      setError(
        'El radio debe estar entre 25 y 100000 metros.',
      )
      return
    }

    try {
      setWorking(true)
      setError(null)
      setMessage(null)

      const created =
        await locationApi
          .createGeofence({
            name: name.trim(),

            description:
              description.trim() ||
              null,

            latitude:
              parsedLatitude,

            longitude:
              parsedLongitude,

            radiusMeters:
              parsedRadius,

            alertOnEnter,
            alertOnExit,
          })

      setName('')
      setDescription('')

      setSelectedGeofenceId(
        created.id,
      )

      setMessage(
        `Geocerca "${created.name}" creada correctamente.`,
      )

      await reloadGeofences()
    } catch (createError) {
      console.error(
        createError,
      )

      setError(
        'No fue posible crear la geocerca.',
      )
    } finally {
      setWorking(false)
    }
  }

  async function assignDevices() {
    if (!canManage) {
      setError(
        'Tu rol no tiene permiso para asignar dispositivos.',
      )
      return
    }

    if (!selectedGeofence) {
      setError(
        'Selecciona una geocerca.',
      )
      return
    }

    if (
      selectedDeviceIds.size === 0
    ) {
      setError(
        'Selecciona al menos un dispositivo.',
      )
      return
    }

    try {
      setWorking(true)
      setError(null)
      setMessage(null)

      await locationApi
        .assignDevices(
          selectedGeofence.id,
          Array.from(
            selectedDeviceIds,
          ),
        )

      setMessage(
        `${selectedDeviceIds.size} dispositivo(s) asignado(s) a "${selectedGeofence.name}".`,
      )

      setSelectedDeviceIds(
        new Set(),
      )

      await reloadGeofences()
    } catch (assignError) {
      console.error(
        assignError,
      )

      setError(
        'No fue posible asignar los dispositivos. Verifica que sean Android de tu organización.',
      )
    } finally {
      setWorking(false)
    }
  }

  async function deleteGeofence(
    geofence: Geofence,
  ) {
    if (!canManage) {
      setError(
        'Tu rol no tiene permiso para eliminar geocercas.',
      )
      return
    }

    const confirmed =
      window.confirm(
        `¿Eliminar la geocerca "${geofence.name}"?`,
      )

    if (!confirmed) {
      return
    }

    try {
      setWorking(true)
      setError(null)

      await locationApi
        .deleteGeofence(
          geofence.id,
        )

      if (
        selectedGeofenceId ===
        geofence.id
      ) {
        setSelectedGeofenceId(
          null,
        )
      }

      setMessage(
        'Geocerca eliminada.',
      )

      await reloadGeofences()
    } catch (deleteError) {
      console.error(
        deleteError,
      )

      setError(
        'No fue posible eliminar la geocerca.',
      )
    } finally {
      setWorking(false)
    }
  }

  const totalPages = Math.max(
    1,
    Math.ceil(
      totalDevices /
        DEVICES_PAGE_SIZE,
    ),
  )

  return (
    <div className="geo-page">
      <header className="geo-header">
        <div>
          <span className="geo-eyebrow">
            TITANMDM LOCATION INTELLIGENCE
          </span>

          <h1>
            Geofencing
          </h1>

          <p>
            Ubicación, zonas
            geográficas y asignación
            centralizada de dispositivos
            Android.
          </p>
        </div>

        <button
          type="button"
          className="geo-secondary"
          disabled={loading}
          onClick={() =>
            void refreshAll()
          }
        >
          <RefreshCw size={16} />
          Actualizar
        </button>
      </header>

      {!canManage && (
        <div className="geo-notice warning">
          <ShieldAlert size={15} />
          Tu rol tiene acceso de solo lectura a Geofencing. La creación,
          asignación y eliminación requieren el permiso geofencing.manage.
        </div>
      )}

      {message && (
        <div className="geo-notice success">
          {message}
        </div>
      )}

      {error && (
        <div className="geo-notice error">
          {error}
        </div>
      )}

      <section className="geo-kpis">
        <GeoKpi
          icon={<MapPinned />}
          label="Geocercas"
          value={geofences.length}
        />

        <GeoKpi
          icon={<Smartphone />}
          label="Dispositivos Android"
          value={totalDevices}
        />

        <GeoKpi
          icon={<Users />}
          label="Asignaciones"
          value={totalAssignments}
        />

        <GeoKpi
          icon={<ShieldAlert />}
          label="Zonas activas"
          value={
            geofences.filter(
              (item) =>
                item.isEnabled,
            ).length
          }
        />
      </section>

      <div className="geo-layout">
        <section className="geo-card">
          <header>
            <Plus size={18} />

            <div>
              <strong>
                Nueva geocerca
              </strong>

              <span>
                Define una zona
                circular administrada.
              </span>
            </div>
          </header>

          <div className="geo-form">
            <label>
              Nombre
              <input
                value={name}
                disabled={
                  !canManage ||
                  working
                }
                onChange={(event) =>
                  setName(
                    event.target.value,
                  )
                }
                placeholder="Oficina principal"
              />
            </label>

            <label>
              Descripción
              <input
                value={description}
                disabled={
                  !canManage ||
                  working
                }
                onChange={(event) =>
                  setDescription(
                    event.target.value,
                  )
                }
                placeholder="Zona corporativa"
              />
            </label>

            <div className="geo-map-editor">
              <MapContainer
                center={
                  mapCenter ??
                  SANTO_DOMINGO_CENTER
                }
                zoom={13}
                className="geo-map-canvas"
              >
                <TileLayer
                  url={provider.url}
                  attribution={provider.attribution}
                  maxZoom={provider.maxZoom}
                />

                <MapEventsHandler
                  onMapClick={handleMapClick}
                />

                <MapController
                  center={mapCenter}
                />

                {geofences.map(
                  (geofence) => (
                    <Circle
                      key={geofence.id}
                      center={[
                        geofence.latitude,
                        geofence.longitude,
                      ]}
                      radius={
                        geofence.radiusMeters
                      }
                      eventHandlers={{
                        click: () =>
                          handleSelectGeofence(
                            geofence,
                          ),
                      }}
                      pathOptions={{
                        color:
                          geofence.id ===
                          selectedGeofenceId
                            ? '#15803d'
                            : geofence.isEnabled
                              ? '#4f63d8'
                              : '#94a3b8',
                        weight:
                          geofence.id ===
                          selectedGeofenceId
                            ? 3
                            : 1.5,
                        fillColor:
                          geofence.isEnabled
                            ? '#4f63d8'
                            : '#94a3b8',
                        fillOpacity:
                          geofence.id ===
                          selectedGeofenceId
                            ? 0.2
                            : 0.08,
                        dashArray:
                          geofence.isEnabled
                            ? undefined
                            : '6 6',
                        bubblingMouseEvents: false,
                      }}
                    />
                  ),
                )}

                {mapCenter && (
                  <>
                    <Circle
                      center={mapCenter}
                      radius={
                        parsedRadius ?? 0
                      }
                      pathOptions={{
                        color: '#16a34a',
                        fillColor: '#16a34a',
                        fillOpacity: 0.22,
                        weight: 2,
                      }}
                    />

                    <Marker
                      position={mapCenter}
                      icon={CENTER_ICON}
                      draggable={canManage}
                      eventHandlers={{
                        dragend: (event) => {
                          if (!canManage) return

                          const marker =
                            event.target as L.Marker
                          const point =
                            marker.getLatLng()

                          setLatitude(
                            point.lat.toFixed(
                              6,
                            ),
                          )
                          setLongitude(
                            point.lng.toFixed(
                              6,
                            ),
                          )
                          setSelectedGeofenceId(
                            null,
                          )
                        },
                      }}
                    />

                    {radiusHandle &&
                      parsedRadius !==
                        null && (
                        <Marker
                          position={
                            radiusHandle
                          }
                          icon={RADIUS_ICON}
                          draggable={canManage}
                          eventHandlers={{
                            dragend: (
                              event,
                            ) => {
                              if (
                                !canManage
                              ) {
                                return
                              }

                              const marker =
                                event.target as L.Marker
                              const point =
                                marker.getLatLng()

                              const nextRadius =
                                distanceMeters(
                                  mapCenter,
                                  [
                                    point.lat,
                                    point.lng,
                                  ],
                                )

                              setRadius(
                                String(
                                  clampRadius(
                                    nextRadius,
                                  ),
                                ),
                              )
                              setSelectedGeofenceId(
                                null,
                              )
                            },
                          }}
                        />
                      )}
                  </>
                )}
              </MapContainer>
            </div>

            <p className="geo-map-hint">
              Haz clic en el mapa para fijar el centro. Arrastra el
              marcador verde para mover el centro y el marcador ámbar
              para ajustar el radio en metros.
            </p>

            <div className="geo-two-columns">
              <label>
                Latitud
                <input
                  type="number"
                  step="any"
                  value={latitude}
                  disabled={
                    !canManage ||
                    working
                  }
                  onChange={(event) =>
                    setLatitude(
                      event.target.value,
                    )
                  }
                  placeholder="18.4861"
                />
              </label>

              <label>
                Longitud
                <input
                  type="number"
                  step="any"
                  value={longitude}
                  disabled={
                    !canManage ||
                    working
                  }
                  onChange={(event) =>
                    setLongitude(
                      event.target.value,
                    )
                  }
                  placeholder="-69.9312"
                />
              </label>
            </div>

            <label>
              Radio en metros
              <input
                type="number"
                min={MIN_RADIUS_METERS}
                max={MAX_RADIUS_METERS}
                value={radius}
                disabled={
                  !canManage ||
                  working
                }
                onChange={(event) =>
                  setRadius(
                    event.target.value,
                  )
                }
              />
            </label>

            <div className="geo-checks">
              <label>
                <input
                  type="checkbox"
                  checked={alertOnEnter}
                  disabled={
                    !canManage ||
                    working
                  }
                  onChange={(event) =>
                    setAlertOnEnter(
                      event.target.checked,
                    )
                  }
                />
                Detectar entrada
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={alertOnExit}
                  disabled={
                    !canManage ||
                    working
                  }
                  onChange={(event) =>
                    setAlertOnExit(
                      event.target.checked,
                    )
                  }
                />
                Detectar salida
              </label>
            </div>

            <button
              type="button"
              className="geo-primary"
              disabled={
                !canManage ||
                working
              }
              onClick={() =>
                void createGeofence()
              }
            >
              <MapPin size={16} />
              Crear geocerca
            </button>
          </div>
        </section>

        <section className="geo-card">
          <header>
            <Crosshair size={18} />

            <div>
              <strong>
                Zonas configuradas
              </strong>

              <span>
                Selecciona una para
                administrar su flota.
              </span>
            </div>
          </header>

          <div className="geo-zone-list">
            {loading ? (
              <div className="geo-empty">
                Cargando geocercas...
              </div>
            ) : geofences.length === 0 ? (
              <div className="geo-empty">
                No existen geocercas.
              </div>
            ) : (
              geofences.map(
                (geofence) => (
                  <article
                    key={geofence.id}
                    className={
                      selectedGeofenceId ===
                      geofence.id
                        ? 'geo-zone selected'
                        : 'geo-zone'
                    }
                  >
                    <button
                      type="button"
                      className="geo-zone-main"
                      onClick={() =>
                        handleSelectGeofence(
                          geofence,
                        )
                      }
                    >
                      <MapPinned
                        size={18}
                      />

                      <div>
                        <span className="geo-zone-title">
                          <strong>
                            {geofence.name}
                          </strong>

                          <span
                            className={
                              geofence.isEnabled
                                ? 'geo-status geo-status--active'
                                : 'geo-status geo-status--inactive'
                            }
                          >
                            {geofence.isEnabled
                              ? 'Activa'
                              : 'Desactivada'}
                          </span>
                        </span>

                        <span>
                          {
                            geofence.radiusMeters
                          }{' '}
                          m ·{' '}
                          {
                            geofence.assignedDevices
                          }{' '}
                          dispositivo(s)
                        </span>

                        <small>
                          {geofence.latitude.toFixed(
                            6,
                          )}
                          ,{' '}
                          {geofence.longitude.toFixed(
                            6,
                          )}
                        </small>
                      </div>
                    </button>

                    <button
                      type="button"
                      className="geo-delete"
                      disabled={
                        !canManage ||
                        working
                      }
                      aria-label={`Eliminar ${geofence.name}`}
                      onClick={() =>
                        void deleteGeofence(
                          geofence,
                        )
                      }
                    >
                      <Trash2
                        size={15}
                      />
                    </button>
                  </article>
                ),
              )
            )}
          </div>
        </section>
      </div>

      <section className="geo-card geo-devices-card">
        <header>
          <Smartphone size={18} />

          <div>
            <strong>
              Asignación de dispositivos
            </strong>

            <span>
              {selectedGeofence
                ? `Zona seleccionada: ${selectedGeofence.name}`
                : 'Selecciona primero una geocerca.'}
            </span>
          </div>
        </header>

        <div className="geo-device-toolbar">
          <div className="geo-search">
            <Search size={16} />

            <input
              value={search}
              onChange={(event) =>
                handleSearchChange(
                  event.target.value,
                )
              }
              placeholder="Buscar dispositivo Android..."
              aria-label="Buscar dispositivo Android"
            />
          </div>

          <div className="geo-pagination">
            <button
              type="button"
              className="geo-pagination-btn"
              aria-label="Página anterior"
              disabled={
                devicePage <= 1 ||
                devicesLoading
              }
              onClick={() =>
                setDevicePage(
                  (page) => page - 1,
                )
              }
            >
              <ChevronLeft size={16} />
            </button>

            <span className="geo-pagination-info">
              Página {devicePage} de {totalPages}
            </span>

            <button
              type="button"
              className="geo-pagination-btn"
              aria-label="Página siguiente"
              disabled={
                devicePage >=
                  totalPages ||
                devicesLoading
              }
              onClick={() =>
                setDevicePage(
                  (page) => page + 1,
                )
              }
            >
              <ChevronRight size={16} />
            </button>
          </div>

          <span>
            {selectedDeviceIds.size}
            {' '}
            seleccionado(s)
          </span>

          <button
            type="button"
            className="geo-primary"
            disabled={
              !canManage ||
              working ||
              !selectedGeofence ||
              selectedDeviceIds.size ===
                0
            }
            onClick={() =>
              void assignDevices()
            }
          >
            Asignar a zona
          </button>
        </div>

        <div className="geo-device-grid">
          {devicesLoading ? (
            <div className="geo-empty">
              Cargando dispositivos Android...
            </div>
          ) : filteredDevices.length ===
            0 ? (
            <div className="geo-empty">
              {search.trim()
                ? 'No hay dispositivos Android que coincidan con la búsqueda.'
                : 'No hay dispositivos Android disponibles en esta organización.'}
            </div>
          ) : (
            filteredDevices.map(
              (device) => (
                <label
                  key={device.id}
                  className="geo-device-item"
                >
                  <input
                    type="checkbox"
                    disabled={!canManage}
                    checked={
                      selectedDeviceIds.has(
                        device.id,
                      )
                    }
                    onChange={() =>
                      toggleDevice(
                        device.id,
                      )
                    }
                  />

                  <Smartphone
                    size={17}
                  />

                  <div>
                    <strong>
                      {deviceDisplayName(device)}
                    </strong>

                    <span>
                      {device.platform}
                      {' · '}
                      {device.status}
                    </span>

                    <small>
                      {device.alias?.trim()
                        ? `${device.deviceName} · ${device.serialNumber}`
                        : device.serialNumber}
                    </small>
                  </div>
                </label>
              ),
            )
          )}
        </div>
      </section>
    </div>
  )
}

function GeoKpi({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: number
}) {
  return (
    <article className="geo-kpi">
      <div>{icon}</div>

      <span>{label}</span>

      <strong>{value}</strong>
    </article>
  )
}
