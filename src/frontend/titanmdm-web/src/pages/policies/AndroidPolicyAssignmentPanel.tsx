import {
  CheckCircle2,
  Loader2,
  RefreshCw,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  policiesApi,
  type AndroidPolicyAssignment,
  type AndroidPolicyPublication,
} from '../../api/policiesApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../types/device'

interface AndroidPolicyAssignmentPanelProps {
  policyId: string
  publication: AndroidPolicyPublication | null
  policyDirty: boolean
}

function formatDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return 'N/D'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function getErrorMessage(
  error: unknown,
): string {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error
  ) {
    const response =
      (
        error as {
          response?: {
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (response?.data?.message) {
      return response.data.message
    }
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Ocurrió un error inesperado.'
}

export default function AndroidPolicyAssignmentPanel({
  policyId,
  publication,
  policyDirty,
}: AndroidPolicyAssignmentPanelProps) {
  const [devices, setDevices] =
    useState<DeviceListItem[]>([])

  const [selectedDeviceId, setSelectedDeviceId] =
    useState('')

  const [assignment, setAssignment] =
    useState<AndroidPolicyAssignment | null>(
      null,
    )

  const [loadingDevices, setLoadingDevices] =
    useState(false)

  const [loadingAssignment, setLoadingAssignment] =
    useState(false)

  const [assigning, setAssigning] =
    useState(false)

  const [error, setError] =
    useState<string | null>(null)

  const [success, setSuccess] =
    useState<string | null>(null)

  const published =
    publication?.status === 'Published'

  const selectedDevice =
    useMemo(
      () =>
        devices.find(
          (device) =>
            device.id === selectedDeviceId,
        ) ?? null,
      [devices, selectedDeviceId],
    )

  const loadDevices =
    useCallback(async () => {
      setLoadingDevices(true)
      setError(null)

      try {
        const result =
          await devicesApi.getDevices({
            platform: 'Android',
            page: 1,
            pageSize: 100,
          })

        setDevices(result.items)

        setSelectedDeviceId(
          (current) => {
            if (
              current &&
              result.items.some(
                (device) =>
                  device.id === current,
              )
            ) {
              return current
            }

            return result.items[0]?.id ?? ''
          },
        )
      } catch (loadError) {
        setError(
          getErrorMessage(loadError),
        )
      } finally {
        setLoadingDevices(false)
      }
    }, [])

  const loadAssignment =
    useCallback(async () => {
      if (!selectedDeviceId) {
        setAssignment(null)
        return
      }

      setLoadingAssignment(true)

      try {
        const result =
          await policiesApi
            .getAndroidAssignment(
              policyId,
              selectedDeviceId,
            )

        setAssignment(result)
      } catch (loadError) {
        setError(
          getErrorMessage(loadError),
        )
      } finally {
        setLoadingAssignment(false)
      }
    }, [
      policyId,
      selectedDeviceId,
    ])

  useEffect(() => {
    void loadDevices()
  }, [loadDevices])

  useEffect(() => {
    void loadAssignment()
  }, [loadAssignment])

  const handleAssign =
    async () => {
      if (!selectedDeviceId) {
        setError(
          'Selecciona un dispositivo Android.',
        )
        return
      }

      if (!published) {
        setError(
          'La política debe estar publicada en Android Enterprise antes de asignarla.',
        )
        return
      }

      if (policyDirty) {
        setError(
          'Guarda los cambios de la política antes de asignarla.',
        )
        return
      }

      setAssigning(true)
      setError(null)
      setSuccess(null)

      try {
        const result =
          await policiesApi.assignAndroid(
            policyId,
            selectedDeviceId,
          )

        setAssignment(result)

        setSuccess(
          'Google Android Management API aceptó la asignación. El dispositivo permanecerá en estado Applying hasta confirmar la aplicación de la política.',
        )
      } catch (assignError) {
        setError(
          getErrorMessage(assignError),
        )
      } finally {
        setAssigning(false)
      }
    }

  return (
    <section className="android-assignment-card">
      <div className="android-assignment-header">
        <div className="android-assignment-title">
          <div className="android-assignment-icon">
            <Smartphone size={20} />
          </div>

          <div>
            <h3>
              Asignación Android Enterprise
            </h3>

            <p>
              Asigna la política publicada a
              dispositivos administrados por Google.
            </p>
          </div>
        </div>

        <button
          type="button"
          className="android-assignment-refresh"
          onClick={() => {
            void loadDevices()
            void loadAssignment()
          }}
          disabled={
            loadingDevices ||
            loadingAssignment
          }
        >
          <RefreshCw
            size={16}
            className={
              loadingDevices ||
              loadingAssignment
                ? 'spin'
                : ''
            }
          />

          Actualizar
        </button>
      </div>

      <div className="android-assignment-publication">
        <div>
          <span>
            Publicación
          </span>

          <strong>
            {published
              ? 'Publicada'
              : publication?.status ??
                'Sin publicar'}
          </strong>
        </div>

        <div>
          <span>
            Google Policy
          </span>

          <strong>
            {publication?.googlePolicyName ??
              'N/D'}
          </strong>
        </div>
      </div>

      {policyDirty && (
        <div className="android-assignment-warning">
          Existen cambios sin guardar. Guarda y
          publica la versión actual antes de
          realizar nuevas asignaciones.
        </div>
      )}

      {error && (
        <div className="android-assignment-error">
          {error}
        </div>
      )}

      {success && (
        <div className="android-assignment-success">
          <CheckCircle2 size={18} />
          {success}
        </div>
      )}

      <div className="android-assignment-body">
        <label
          className="android-assignment-field"
        >
          <span>
            Dispositivo Android
          </span>

          <select
            value={selectedDeviceId}
            onChange={(event) => {
              setSelectedDeviceId(
                event.target.value,
              )

              setSuccess(null)
              setError(null)
            }}
            disabled={
              loadingDevices ||
              devices.length === 0
            }
          >
            {devices.length === 0 && (
              <option value="">
                No hay dispositivos Android
              </option>
            )}

            {devices.map((device) => (
              <option
                key={device.id}
                value={device.id}
              >
                {device.deviceName}
                {' — '}
                {device.status}
              </option>
            ))}
          </select>
        </label>

        {selectedDevice && (
          <div className="android-selected-device">
            <Smartphone size={18} />

            <div>
              <strong>
               {selectedDevice.deviceName}
              </strong>

              <span>
                {selectedDevice.manufacturer ??
                  'Fabricante N/D'}
                {' · '}
                {selectedDevice.model ??
                  'Modelo N/D'}
              </span>
            </div>
          </div>
        )}

        <button
          type="button"
          className="android-assign-button"
          disabled={
            assigning ||
            loadingDevices ||
            !selectedDeviceId ||
            !published ||
            policyDirty
          }
          onClick={() => {
            void handleAssign()
          }}
        >
          {assigning ? (
            <Loader2
              size={17}
              className="spin"
            />
          ) : (
            <ShieldCheck size={17} />
          )}

          {assigning
            ? 'Asignando...'
            : 'Asignar política'}
        </button>
      </div>

      {loadingAssignment && (
        <div className="android-assignment-loading">
          <Loader2
            size={17}
            className="spin"
          />

          Consultando asignación...
        </div>
      )}

      {!loadingAssignment &&
        assignment && (
          <div className="android-assignment-status">
            <div>
              <span>
                Estado TitanMDM
              </span>

              <strong>
                {assignment.assignmentStatus}
              </strong>
            </div>

            <div>
              <span>
                Versión
              </span>

              <strong>
                {assignment.policyVersion}
              </strong>
            </div>

            <div>
              <span>
                Estado Google
              </span>

              <strong>
                {assignment.appliedPolicyState ??
                  'Pendiente'}
              </strong>
            </div>

            <div>
              <span>
                Última sincronización
              </span>

              <strong>
                {formatDate(
                  assignment
                    .lastPolicySyncTimeUtc,
                )}
              </strong>
            </div>

            {assignment.errorMessage && (
              <div className="android-assignment-status-error">
                <span>
                  Error
                </span>

                <strong>
                  {assignment.errorMessage}
                </strong>
              </div>
            )}
          </div>
        )}
    </section>
  )
}