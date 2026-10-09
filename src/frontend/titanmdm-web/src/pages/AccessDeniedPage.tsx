import {
  ArrowLeft,
  Home,
  LockKeyhole,
  MapPinOff,
  ShieldAlert,
} from 'lucide-react'

import {
  useLocation,
  useNavigate,
} from 'react-router-dom'

import {
  useWorkspace,
} from '../workspace/WorkspaceContext'

interface AccessDeniedState {
  reason?:
    | 'workspace'
    | 'permission'
    | 'scope'

  from?: string
}

export function AccessDeniedPage() {
  const navigate =
    useNavigate()

  const location =
    useLocation()

  const {
    activeModule,
  } =
    useWorkspace()

  const state =
    (
      location.state
      ??
      {}
    ) as AccessDeniedState

  const reason =
    state.reason
    ??
    'permission'

  const scopeDenied =
    reason ===
      'scope'

  const workspaceDenied =
    reason ===
      'workspace'

  const title =
    scopeDenied
      ? 'Recurso fuera de tu alcance'
      : workspaceDenied
        ? 'No tienes acceso a este módulo'
        : 'No tienes permiso para esta función'

  const message =
    scopeDenied
      ? (
          'Tu cuenta posee permisos funcionales, ' +
          'pero el recurso solicitado pertenece a ' +
          'una localidad que no está asignada a tu usuario.'
        )
      : workspaceDenied
        ? (
            'Tu cuenta está autenticada, pero el ' +
            'workspace solicitado no está habilitado ' +
            'para los roles asignados.'
          )
        : (
            'Tu cuenta está autenticada, pero los ' +
            'roles asignados actualmente no incluyen ' +
            'el permiso necesario para ejecutar esta función.'
          )

  const Icon =
    scopeDenied
      ? MapPinOff
      : ShieldAlert

  return (
    <div
      className="access-denied-page"
    >
      <div
        className="access-denied-card"
      >
        <div
          className="access-denied-card__icon"
        >
          <Icon
            size={30}
          />
        </div>

        <span
          className="access-denied-card__eyebrow"
        >
          ACCESO RESTRINGIDO
        </span>

        <h1>
          {title}
        </h1>

        <p>
          {message}
        </p>

        {activeModule && (
          <div
            className="access-denied-context"
          >
            <LockKeyhole
              size={16}
            />

            <div>
              <span>
                Espacio actual
              </span>

              <strong>
                {activeModule.title}
              </strong>
            </div>
          </div>
        )}

        {state.from && (
          <div
            className="access-denied-context"
          >
            <div>
              <span>
                Recurso solicitado
              </span>

              <strong>
                {state.from}
              </strong>
            </div>
          </div>
        )}

        <div
          className="access-denied-actions"
        >
          <button
            type="button"
            className="access-denied-button"
            onClick={
              () =>
                navigate(
                  -1,
                )
            }
          >
            <ArrowLeft
              size={16}
            />

            Volver
          </button>

          <button
            type="button"
            className="access-denied-button access-denied-button--primary"
            onClick={
              () =>
                navigate(
                  '/',
                )
            }
          >
            <Home
              size={16}
            />

            Titan Workspace
          </button>
        </div>
      </div>
    </div>
  )
}