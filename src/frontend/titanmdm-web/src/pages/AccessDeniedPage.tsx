import {
  ArrowLeft,
  Home,
  LockKeyhole,
  ShieldAlert,
} from 'lucide-react'

import {
  useNavigate,
} from 'react-router-dom'

import {
  useWorkspace,
} from '../workspace/WorkspaceContext'

export function AccessDeniedPage() {
  const navigate =
    useNavigate()

  const {
    activeModule,
  } =
    useWorkspace()

  return (
    <div className="access-denied-page">
      <div className="access-denied-card">
        <div className="access-denied-card__icon">
          <ShieldAlert
            size={30}
          />
        </div>

        <span className="access-denied-card__eyebrow">
          ACCESO RESTRINGIDO
        </span>

        <h1>
          No tienes permiso para acceder
          a esta función
        </h1>

        <p>
          Tu cuenta está autenticada,
          pero el rol asignado actualmente
          no incluye el permiso necesario
          para abrir esta sección.
        </p>

        {activeModule && (
          <div className="access-denied-context">
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

        <div className="access-denied-actions">
          <button
            type="button"
            className="access-denied-button"
            onClick={() =>
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
            onClick={() =>
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