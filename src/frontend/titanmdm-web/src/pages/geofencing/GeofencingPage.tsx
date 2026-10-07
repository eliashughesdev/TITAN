import {
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
  useState,
  type ReactNode,
} from 'react'

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

import './GeofencingPage.css'

export function GeofencingPage() {
  const [geofences, setGeofences] =
    useState<Geofence[]>([])

  const [devices, setDevices] =
    useState<DeviceListItem[]>([])

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
    useState('')

  const [longitude, setLongitude] =
    useState('')

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

  const [working, setWorking] =
    useState(false)

  const [message, setMessage] =
    useState<string | null>(null)

  const [error, setError] =
    useState<string | null>(null)

  const loadData =
    useCallback(async () => {
      try {
        setLoading(true)
        setError(null)

        const [
          geofenceData,
          deviceData,
        ] = await Promise.all([
          locationApi.getGeofences(),

          devicesApi.getDevices({
            page: 1,
            pageSize: 100,
          }),
        ])

        setGeofences(
          geofenceData,
        )

        setDevices(
          deviceData.items,
        )
      } catch (loadError) {
        console.error(
          loadError,
        )

        setError(
          'No fue posible cargar Geofencing.',
        )
      } finally {
        setLoading(false)
      }
    }, [])

  useEffect(() => {
    void loadData()
  }, [loadData])

  useEffect(() => {
    document.title =
      'Geofencing | TitanMDM'
  }, [])

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
      const value =
        search
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

  function toggleDevice(
    deviceId: string,
  ) {
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

  async function createGeofence() {
    const parsedLatitude =
      Number(latitude)

    const parsedLongitude =
      Number(longitude)

    const parsedRadius =
      Number(radius)

    if (!name.trim()) {
      setError(
        'Introduce un nombre para la geocerca.',
      )
      return
    }

    if (
      !Number.isFinite(
        parsedLatitude,
      ) ||
      parsedLatitude < -90 ||
      parsedLatitude > 90
    ) {
      setError(
        'La latitud debe estar entre -90 y 90.',
      )
      return
    }

    if (
      !Number.isFinite(
        parsedLongitude,
      ) ||
      parsedLongitude < -180 ||
      parsedLongitude > 180
    ) {
      setError(
        'La longitud debe estar entre -180 y 180.',
      )
      return
    }

    if (
      !Number.isFinite(
        parsedRadius,
      ) ||
      parsedRadius < 25 ||
      parsedRadius > 100000
    ) {
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
      setLatitude('')
      setLongitude('')
      setRadius('250')

      setSelectedGeofenceId(
        created.id,
      )

      setMessage(
        `Geocerca "${created.name}" creada correctamente.`,
      )

      await loadData()
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

      await loadData()
    } catch (assignError) {
      console.error(
        assignError,
      )

      setError(
        'No fue posible asignar los dispositivos.',
      )
    } finally {
      setWorking(false)
    }
  }

  async function deleteGeofence(
    geofence: Geofence,
  ) {
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

      await loadData()
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
            centralizada de dispositivos.
          </p>
        </div>

        <button
          type="button"
          className="geo-secondary"
          disabled={loading}
          onClick={() =>
            void loadData()
          }
        >
          <RefreshCw size={16} />
          Actualizar
        </button>
      </header>

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
          label="Dispositivos"
          value={devices.length}
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
                onChange={(event) =>
                  setDescription(
                    event.target.value,
                  )
                }
                placeholder="Zona corporativa"
              />
            </label>

            <div className="geo-two-columns">
              <label>
                Latitud
                <input
                  type="number"
                  step="any"
                  value={latitude}
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
                min="25"
                max="100000"
                value={radius}
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
              disabled={working}
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
            {geofences.length === 0 ? (
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
                        setSelectedGeofenceId(
                          geofence.id,
                        )
                      }
                    >
                      <MapPinned
                        size={18}
                      />

                      <div>
                        <strong>
                          {geofence.name}
                        </strong>

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
                      disabled={working}
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
                setSearch(
                  event.target.value,
                )
              }
              placeholder="Buscar dispositivo..."
            />
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
          {filteredDevices.map(
            (device) => (
              <label
                key={device.id}
                className="geo-device-item"
              >
                <input
                  type="checkbox"
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
                    {device.deviceName}
                  </strong>

                  <span>
                    {device.platform}
                    {' · '}
                    {device.status}
                  </span>

                  <small>
                    {device.serialNumber}
                  </small>
                </div>
              </label>
            ),
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