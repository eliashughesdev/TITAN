import {
  AlertTriangle,
  KeyRound,
  LockKeyhole,
  MapPin,
  MapPinOff,
  Power,
  RotateCcw,
  ShieldAlert,
  Trash2,
} from 'lucide-react'

import {
  useState,
} from 'react'

import type {
  LucideIcon,
} from 'lucide-react'

import {
  androidEnterpriseApi,
} from '../../../api/androidEnterpriseApi'

import {
  useAuth,
} from '../../../auth/AuthContext'

import {
  Permissions,
} from '../../../auth/permissions'

import type {
  AndroidRemoteAction,
  AndroidRemoteActionRequest,
  AndroidRemoteActionResponse,
} from '../../../types/androidEnterprise'

import {
  formatDeviceDate,
} from '../utils/deviceDetail.utils'

interface Props {
  deviceId: string
}

interface ActionDefinition {
  action: AndroidRemoteAction
  label: string
  description: string
  tone: 'standard' | 'warning' | 'danger'
  icon: LucideIcon
}

const actions: ActionDefinition[] = [
  {
    action: 'lock',
    label: 'Bloquear',
    description: 'Solicita el bloqueo inmediato de la pantalla.',
    tone: 'standard',
    icon: LockKeyhole,
  },
  {
    action: 'reboot',
    label: 'Reiniciar',
    description: 'Solicita un reinicio mediante Android Management API.',
    tone: 'warning',
    icon: RotateCcw,
  },
  {
    action: 'reset-password',
    label: 'Restablecer contraseña',
    description: 'Emite una solicitud con la nueva contraseña indicada.',
    tone: 'danger',
    icon: KeyRound,
  },
  {
    action: 'start-lost-mode',
    label: 'Iniciar modo perdido',
    description: 'Solicita mostrar la información de recuperación.',
    tone: 'warning',
    icon: MapPin,
  },
  {
    action: 'stop-lost-mode',
    label: 'Detener modo perdido',
    description: 'Solicita finalizar el modo perdido activo.',
    tone: 'standard',
    icon: MapPinOff,
  },
  {
    action: 'wipe',
    label: 'Borrar dispositivo',
    description: 'Solicita un borrado de fábrica irreversible.',
    tone: 'danger',
    icon: ShieldAlert,
  },
  {
    action: 'delete',
    label: 'Eliminar de Android Enterprise',
    description: 'Solicita eliminar el recurso administrado de Google.',
    tone: 'danger',
    icon: Trash2,
  },
]

const confirmations:
  Partial<Record<AndroidRemoteAction, string>> = {
    'reset-password':
      '¿Confirmas el restablecimiento de la contraseña del dispositivo?',
    wipe:
      'Esta acción solicita un borrado de fábrica irreversible. ¿Deseas continuar?',
    delete:
      'Esta acción solicita eliminar el dispositivo de Android Enterprise. ¿Deseas continuar?',
  }

export function getAndroidRemoteActionError(
  error: unknown,
): string {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
  ) {
    const response = (
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

  return 'No fue posible emitir la acción remota Android.'
}

export function AndroidRemoteActionsPanel({
  deviceId,
}: Props) {
  const {
    hasPermission,
  } = useAuth()

  const canExecute =
    hasPermission(
      Permissions.androidEnterprise.actions,
    )

  const [
    activeAction,
    setActiveAction,
  ] = useState<AndroidRemoteAction | null>(null)

  const [
    newPassword,
    setNewPassword,
  ] = useState('')

  const [
    lostModeMessage,
    setLostModeMessage,
  ] = useState('')

  const [
    lostModePhoneNumber,
    setLostModePhoneNumber,
  ] = useState('')

  const [
    result,
    setResult,
  ] = useState<AndroidRemoteActionResponse | null>(null)

  const [
    error,
    setError,
  ] = useState<string | null>(null)

  async function execute(
    action: AndroidRemoteAction,
  ) {
    if (!canExecute || activeAction) {
      return
    }

    if (
      action === 'reset-password'
      && !newPassword.trim()
    ) {
      setResult(null)
      setError('Indica la nueva contraseña antes de emitir la solicitud.')
      return
    }

    const confirmation =
      confirmations[action]

    if (
      confirmation
      && !window.confirm(confirmation)
    ) {
      return
    }

    const request: AndroidRemoteActionRequest = {
      action,
    }

    if (action === 'reset-password') {
      request.newPassword = newPassword
      setNewPassword('')
    }

    if (action === 'start-lost-mode') {
      if (lostModeMessage.trim()) {
        request.lostModeMessage = lostModeMessage.trim()
      }

      if (lostModePhoneNumber.trim()) {
        request.lostModePhoneNumber = lostModePhoneNumber.trim()
      }
    }

    try {
      setActiveAction(action)
      setResult(null)
      setError(null)

      const response =
        await androidEnterpriseApi
          .executeDeviceAction(
            deviceId,
            request,
          )

      setResult(response)
    } catch (requestError) {
      console.error(requestError)
      setError(
        getAndroidRemoteActionError(
          requestError,
        ),
      )
    } finally {
      setActiveAction(null)
    }
  }

  return (
    <article className="device-detail-card device-detail-card--wide android-remote-actions">
      <header>
        <Power size={18} />

        <div>
          <h2>Acciones remotas Android</h2>
          <p>
            Solicitudes directas a Android Management API
          </p>
        </div>
      </header>

      {!canExecute ? (
        <div className="android-remote-access-note">
          <AlertTriangle size={17} />
          No tienes el permiso android.enterprise.actions para emitir acciones remotas.
        </div>
      ) : (
        <div className="android-remote-actions-body">
          <div className="android-remote-guidance">
            La respuesta confirma que Google recibió o emitió la solicitud; no confirma que el dispositivo haya completado la acción.
          </div>

          <div className="android-remote-inputs">
            <label>
              <span>Nueva contraseña</span>
              <input
                type="password"
                autoComplete="new-password"
                value={newPassword}
                disabled={activeAction !== null}
                onChange={(event) =>
                  setNewPassword(event.target.value)
                }
                placeholder="Solo para restablecer contraseña"
              />
            </label>

            <label>
              <span>Mensaje de modo perdido</span>
              <input
                type="text"
                value={lostModeMessage}
                disabled={activeAction !== null}
                onChange={(event) =>
                  setLostModeMessage(event.target.value)
                }
                placeholder="Opcional"
              />
            </label>

            <label>
              <span>Teléfono de recuperación</span>
              <input
                type="tel"
                value={lostModePhoneNumber}
                disabled={activeAction !== null}
                onChange={(event) =>
                  setLostModePhoneNumber(event.target.value)
                }
                placeholder="Opcional"
              />
            </label>
          </div>

          <div className="android-remote-action-grid">
            {actions.map((item) => {
              const Icon = item.icon

              return (
                <button
                  type="button"
                  key={item.action}
                  className={`android-remote-action android-remote-action--${item.tone}`}
                  disabled={activeAction !== null}
                  onClick={() =>
                    void execute(item.action)
                  }
                >
                  <Icon size={18} />

                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.description}</small>
                  </span>

                  {activeAction === item.action && (
                    <em>Emitiendo...</em>
                  )}
                </button>
              )
            })}
          </div>

          {error && (
            <div
              className="device-detail-notice device-detail-notice--error"
              role="alert"
            >
              {error}
            </div>
          )}

          {result && (
            <div
              className="android-remote-result"
              role="status"
              aria-live="polite"
            >
              <strong>
                {result.status === 'DeletionRequested'
                  ? 'Eliminación solicitada'
                  : 'Acción emitida'}
              </strong>

              <span>
                {result.action} · {formatDeviceDate(result.issuedAtUtc)}
              </span>

              <small>
                La solicitud fue aceptada; espera la próxima sincronización o cambio de estado para verificar el resultado en el dispositivo.
              </small>
            </div>
          )}
        </div>
      )}
    </article>
  )
}
