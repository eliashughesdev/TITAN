
import { useEffect, useMemo, type CSSProperties } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import {
  ArrowRight,
  ArrowUpRight,
  Bot,
  Boxes,
  CheckCircle2,
  ChevronRight,
  CircleDot,
  LockKeyhole,
  ShieldCheck,
  Sparkles,
} from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import {
  titanModules,
  type TitanModuleDefinition,
} from '../../config/moduleRegistry'

import './LaunchpadPage.css'

type ModuleStyle = CSSProperties & {
  '--module-primary': string
  '--module-soft': string
  '--module-border': string
  '--module-gradient': string
}

function getModuleStyle(module: TitanModuleDefinition): ModuleStyle {
  return {
    '--module-primary': module.theme.primary,
    '--module-soft': module.theme.soft,
    '--module-border': module.theme.border,
    '--module-gradient': module.theme.gradient,
  }
}

function WorkspaceModuleCard({
  module,
  onOpen,
  index,
  reducedMotion,
}: {
  module: TitanModuleDefinition
  onOpen: () => void
  index: number
  reducedMotion: boolean
}) {
  const Icon = module.icon

  return (
    <motion.button
      type="button"
      className={`launchpad-card launchpad-card--${module.id}`}
      style={getModuleStyle(module)}
      onClick={onOpen}
      aria-label={`Abrir ${module.title}`}
      initial={reducedMotion ? false : { opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={reducedMotion ? undefined : { y: -5 }}
      whileTap={reducedMotion ? undefined : { scale: 0.99 }}
      transition={{
        duration: reducedMotion ? 0 : 0.3,
        delay: reducedMotion ? 0 : Math.min(index, 8) * 0.055,
      }}
    >
      <div className="launchpad-card__top">
        <div className="launchpad-card__icon">
          <Icon size={27} strokeWidth={1.85} />
        </div>

        {module.badge && (
          <span className="launchpad-card__badge">
            {module.badge}
          </span>
        )}
      </div>

      <div className="launchpad-card__content">
        <span>{module.shortTitle}</span>
        <h3>{module.title}</h3>
        <p>{module.description}</p>
      </div>

      <div className="launchpad-card__footer">
        <span className="launchpad-card__action">
          <span>Entrar al módulo</span>
          <ArrowUpRight size={15} />
        </span>

        <span className="launchpad-card__arrow" aria-hidden="true">
          <ArrowRight size={19} />
        </span>
      </div>
    </motion.button>
  )
}

function FutureModuleCard({
  module,
}: {
  module: TitanModuleDefinition
}) {
  const Icon = module.icon

  return (
    <article
      className="launchpad-card launchpad-card--disabled"
      style={getModuleStyle(module)}
    >
      <div className="launchpad-card__top">
        <div className="launchpad-card__icon">
          <Icon size={23} />
        </div>

        <span className="launchpad-card__badge">
          Próximamente
        </span>
      </div>

      <div className="launchpad-card__content">
        <span>{module.shortTitle}</span>
        <h3>{module.title}</h3>
        <p>{module.description}</p>
      </div>
    </article>
  )
}

export function LaunchpadPage() {
  const navigate = useNavigate()
  const { user, hasPermission } = useAuth()
  const prefersReducedMotion = useReducedMotion()
  const reducedMotion = prefersReducedMotion === true

  const availableModules = useMemo<TitanModuleDefinition[]>(
    () =>
      titanModules.filter(
        module =>
          module.enabled &&
          module.permissions.some(permission =>
            hasPermission(permission),
          ),
      ),
    [hasPermission],
  )

  const futureModules = useMemo<TitanModuleDefinition[]>(
    () => titanModules.filter(module => !module.enabled),
    [],
  )

  useEffect(() => {
    document.title = 'Inicio | TitanMDM'
  }, [])

  const firstName = user?.firstName?.trim() || 'Usuario'

  return (
    <main className="launchpad-page">
      {/* HERO */}

      <motion.section
        className="launchpad-hero"
        initial={reducedMotion ? false : { opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: reducedMotion ? 0 : 0.35 }}
      >
        <div className="launchpad-hero__glow" aria-hidden="true" />

        <div className="launchpad-hero__content">
          <div className="launchpad-hero__eyebrow">
            <Sparkles size={15} />
            TITAN ENTERPRISE WORKSPACE
          </div>

          <h1>
            Tu centro de operaciones,
            <span> {firstName}.</span>
          </h1>

          <p>
            Un entorno unificado para administrar la infraestructura,
            atender usuarios y gestionar las operaciones de tu organización.
            Accede únicamente a los módulos habilitados para tu cuenta.
          </p>

          <div className="launchpad-hero__chips">
            <span className="launchpad-hero__chip">
              <ShieldCheck size={15} />
              Acceso protegido
            </span>

            <span className="launchpad-hero__chip">
              <Boxes size={15} />
              {availableModules.length} espacios disponibles
            </span>
          </div>
        </div>

        <div className="launchpad-hero__status">
          <div className="launchpad-system-status">
            <span className="launchpad-status-icon launchpad-status-icon--success">
              <CheckCircle2 size={20} />
            </span>

            <div>
              <strong>Sesión autenticada</strong>
              <span>Acceso controlado por roles y permisos</span>
            </div>

            <CircleDot
              size={14}
              className="launchpad-status-end"
            />
          </div>

          <div className="launchpad-ai-card">
            <span className="launchpad-status-icon launchpad-status-icon--ai">
              <Bot size={21} />
            </span>

            <div>
              <strong>Titan AI</strong>
              <span>Asistente corporativo contextual</span>
            </div>

            <Sparkles
              size={16}
              className="launchpad-status-end launchpad-status-end--ai"
            />
          </div>
        </div>
      </motion.section>

      {/* AVAILABLE MODULES */}

      <section className="launchpad-section" aria-labelledby="launchpad-modules-title">
        <div className="launchpad-section__header">
          <div>
            <span className="launchpad-section__eyebrow">
              ESPACIOS DE TRABAJO
            </span>

            <h2 id="launchpad-modules-title">
              Explora tus módulos
            </h2>

            <p>
              Todas tus herramientas, en una sola plataforma.
            </p>
          </div>

          <div className="launchpad-access-label">
            <LockKeyhole size={15} />
            Acceso según permisos
          </div>
        </div>

        {availableModules.length === 0 ? (
          <div className="launchpad-empty">
            <ShieldCheck size={32} />
            <strong>Sin módulos disponibles</strong>
            <p>
              Tu cuenta está autenticada, pero todavía no tiene
              permisos para acceder a un espacio de trabajo.
            </p>
          </div>
        ) : (
          <div className="launchpad-grid">
            {availableModules.map((module, index) => (
              <WorkspaceModuleCard
                key={module.id}
                module={module}
                index={index}
                reducedMotion={reducedMotion}
                onOpen={() => navigate(module.path)}
              />
            ))}
          </div>
        )}
      </section>

      {/* FUTURE MODULES */}

      {futureModules.length > 0 && (
        <section
          className="launchpad-section launchpad-section--future"
          aria-labelledby="launchpad-future-title"
        >
          <div className="launchpad-section__header">
            <div>
              <span className="launchpad-section__eyebrow">
                EVOLUCIÓN DE TITANMDM
              </span>
              <h2 id="launchpad-future-title">
                Próximamente
              </h2>
              <p>Nuevas capacidades en preparación.</p>
            </div>
          </div>

          <div className="launchpad-grid launchpad-grid--future">
            {futureModules.map(module => (
              <FutureModuleCard
                key={module.id}
                module={module}
              />
            ))}
          </div>
        </section>
      )}

      {/* FOOTER */}

      <footer className="launchpad-footer">
        <span>
          <ShieldCheck size={15} />
          TitanMDM Enterprise · Espacio corporativo protegido
        </span>

        <span>
          Selecciona un módulo para continuar
          <ChevronRight size={15} />
        </span>
      </footer>
    </main>
  )
}
