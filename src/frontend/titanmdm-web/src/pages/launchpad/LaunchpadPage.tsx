import type {
  CSSProperties,
} from 'react'

import {
  ArrowRight,
  Bot,
  Boxes,
  CheckCircle2,
  LockKeyhole,
  ShieldCheck,
} from 'lucide-react'

import {
  useEffect,
  useMemo,
} from 'react'

import {
  useNavigate,
} from 'react-router-dom'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  titanModules,
  type TitanModuleDefinition,
} from '../../config/moduleRegistry'

import './LaunchpadPage.css'

export function LaunchpadPage() {
  const navigate =
    useNavigate()

  const {
    user,
    hasPermission,
  } =
    useAuth()

  /*
   * ============================================================
   * ENABLED MODULES
   * ============================================================
   */

  const availableModules =
    useMemo<
      TitanModuleDefinition[]
    >(
      () =>
        titanModules.filter(
          (
            module:
              TitanModuleDefinition,
          ) =>
            module.enabled
            &&
            module.permissions.some(
              (
                permission:
                  string,
              ) =>
                hasPermission(
                  permission,
                ),
            ),
        ),
      [
        hasPermission,
      ],
    )

  /*
   * ============================================================
   * FUTURE MODULES
   * ============================================================
   */

  const futureModules =
    useMemo<
      TitanModuleDefinition[]
    >(
      () =>
        titanModules.filter(
          (
            module:
              TitanModuleDefinition,
          ) =>
            !module.enabled,
        ),
      [],
    )

  /*
   * ============================================================
   * DOCUMENT TITLE
   * ============================================================
   */

  useEffect(
    () => {
      document.title =
        'Inicio | TitanMDM'
    },
    [],
  )

  const firstName =
    user?.firstName ??
    'Usuario'

  return (
    <div className="launchpad-page">
      {/* ========================================================
          HERO
         ======================================================== */}

      <section className="launchpad-hero">
        <div className="launchpad-hero__content">
          <div className="launchpad-hero__eyebrow">
            <Boxes
              size={15}
            />

            TITAN WORKSPACE
          </div>

          <h1>
            Bienvenido,
            {' '}
            {firstName}
          </h1>

          <p>
            Selecciona el área de trabajo que
            deseas administrar.
            TitanMDM mostrará únicamente los
            módulos autorizados para tu cuenta.
          </p>
        </div>

        <div className="launchpad-hero__status">
          <div className="launchpad-system-status">
            <CheckCircle2
              size={18}
            />

            <div>
              <strong>
                Plataforma disponible
              </strong>

              <span>
                Sesión autenticada y protegida
              </span>
            </div>
          </div>

          <div className="launchpad-ai-card">
            <Bot
              size={19}
            />

            <div>
              <strong>
                Titan AI
              </strong>

              <span>
                Asistente contextual activo
              </span>
            </div>
          </div>
        </div>
      </section>

      {/* ========================================================
          AVAILABLE MODULES
         ======================================================== */}

      <section className="launchpad-section">
        <div className="launchpad-section__header">
          <div>
            <span>
              ESPACIOS DE TRABAJO
            </span>

            <h2>
              Tus módulos
            </h2>
          </div>

          <div className="launchpad-access-label">
            <LockKeyhole
              size={14}
            />

            Acceso basado en permisos
          </div>
        </div>

        {availableModules.length ===
        0 ? (
          <div className="launchpad-empty">
            <ShieldCheck
              size={28}
            />

            <strong>
              Sin módulos disponibles
            </strong>

            <p>
              Tu cuenta está autenticada,
              pero todavía no posee permisos
              para acceder a un área de trabajo.
            </p>
          </div>
        ) : (
          <div className="launchpad-grid">
            {availableModules.map(
              (
                module:
                  TitanModuleDefinition,
              ) => {
                const Icon =
                  module.icon

                const style:
                  CSSProperties &
                  Record<
                    string,
                    string
                  > = {
                    '--module-primary':
                      module.theme.primary,

                    '--module-soft':
                      module.theme.soft,

                    '--module-border':
                      module.theme.border,

                    '--module-gradient':
                      module.theme.gradient,
                  }

                return (
                  <button
                    key={
                      module.id
                    }
                    type="button"
                    className={
                      `launchpad-card launchpad-card--${module.id}`
                    }
                    style={
                      style
                    }
                    onClick={() =>
                      navigate(
                        module.path,
                      )
                    }
                  >
                    <div className="launchpad-card__top">
                      <div className="launchpad-card__icon">
                        <Icon
                          size={25}
                        />
                      </div>

                      {module.badge && (
                        <span className="launchpad-card__badge">
                          {module.badge}
                        </span>
                      )}
                    </div>

                    <div className="launchpad-card__content">
                      <span>
                        {module.shortTitle}
                      </span>

                      <h3>
                        {module.title}
                      </h3>

                      <p>
                        {module.description}
                      </p>
                    </div>

                    <div className="launchpad-card__footer">
                      <span>
                        Abrir módulo
                      </span>

                      <ArrowRight
                        size={17}
                      />
                    </div>
                  </button>
                )
              },
            )}
          </div>
        )}
      </section>

      {/* ========================================================
          ROADMAP MODULES
         ======================================================== */}

      {futureModules.length >
        0 && (
        <section className="launchpad-section launchpad-section--future">
          <div className="launchpad-section__header">
            <div>
              <span>
                ROADMAP
              </span>

              <h2>
                Próximamente
              </h2>
            </div>
          </div>

          <div className="launchpad-grid launchpad-grid--future">
            {futureModules.map(
              (
                module:
                  TitanModuleDefinition,
              ) => {
                const Icon =
                  module.icon

                return (
                  <div
                    key={
                      module.id
                    }
                    className="launchpad-card launchpad-card--disabled"
                  >
                    <div className="launchpad-card__top">
                      <div className="launchpad-card__icon">
                        <Icon
                          size={23}
                        />
                      </div>

                      {module.badge && (
                        <span className="launchpad-card__badge">
                          {module.badge}
                        </span>
                      )}
                    </div>

                    <div className="launchpad-card__content">
                      <span>
                        {module.shortTitle}
                      </span>

                      <h3>
                        {module.title}
                      </h3>

                      <p>
                        {module.description}
                      </p>
                    </div>
                  </div>
                )
              },
            )}
          </div>
        </section>
      )}
    </div>
  )
}