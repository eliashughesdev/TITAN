
import type { ReactNode } from 'react'
import {
  Link,
  useLocation,
} from 'react-router-dom'
import {
  motion,
  useReducedMotion,
} from 'motion/react'
import {
  ArrowLeft,
  ArrowUpRight,
  Headset,
  ShieldCheck,
  Sparkles,
} from 'lucide-react'

import './HelpdeskExperience.css'

interface HelpdeskExperienceProps {
  children: ReactNode
  variant?: 'console' | 'portal'
}

interface WorkspaceHeader {
  eyebrow: string
  title: string
  description: string
}

function getWorkspaceHeader(
  pathname: string,
  variant: 'console' | 'portal',
): WorkspaceHeader {
  if (variant === 'portal') {
    return {
      eyebrow: 'PORTAL DE COLABORADORES',
      title: 'Mi centro de soporte',
      description:
        'Crea solicitudes, consulta su progreso y mantente en contacto con el equipo TIC.',
    }
  }

  if (pathname.includes('/reportes')) {
    return {
      eyebrow: 'INTELIGENCIA DE SERVICIO',
      title: 'Análisis y rendimiento',
      description:
        'Indicadores, tendencias, cumplimiento SLA y desempeño de la Mesa de Ayuda.',
    }
  }

  if (pathname.includes('/templates')) {
    return {
      eyebrow: 'CATÁLOGO DE SERVICIOS',
      title: 'Plantillas y formularios',
      description:
        'Organiza solicitudes y automatiza su clasificación con formularios dinámicos.',
    }
  }

  if (pathname.includes('/entra')) {
    return {
      eyebrow: 'IDENTIDAD EMPRESARIAL',
      title: 'Directorio y acceso corporativo',
      description:
        'Sincronización de usuarios, identidad Microsoft y controles de acceso.',
    }
  }

  if (pathname.includes('/mail')) {
    return {
      eyebrow: 'AUTOMATIZACIÓN DE CORREO',
      title: 'Buzón inteligente',
      description:
        'Supervisa la recepción de solicitudes y el procesamiento de mensajes.',
    }
  }

  if (pathname.includes('/tickets/')) {
    return {
      eyebrow: 'GESTIÓN DE INCIDENCIAS',
      title: 'Atención de solicitud',
      description:
        'Conversación, actividad, asignación, prioridades y seguimiento de SLA.',
    }
  }

  if (
    pathname.includes('/seguimiento') ||
    pathname.includes('/centro/')
  ) {
    return {
      eyebrow: 'OPERACIÓN Y CUMPLIMIENTO',
      title: 'Centro de seguimiento',
      description:
        'Monitorea objetivos de atención, alertas y continuidad del servicio.',
    }
  }

  return {
    eyebrow: 'SERVICE DESK ENTERPRISE',
    title: 'Centro de operaciones TIC',
    description:
      'Gestiona solicitudes, equipos de soporte, asignaciones y tiempos de atención.',
  }
}

export function HelpdeskExperience({
  children,
  variant = 'console',
}: HelpdeskExperienceProps) {
  const location = useLocation()
  const reducedMotion = useReducedMotion()

  const header = getWorkspaceHeader(
    location.pathname,
    variant,
  )

  const isInbox =
    location.pathname === '/helpdesk' ||
    location.pathname === '/helpdesk/'

  const home =
    variant === 'portal'
      ? '/my-support'
      : '/helpdesk?workspace=helpdesk'

  const showBack =
    variant === 'console' && !isInbox

  return (
    <motion.div
      key={location.pathname}
      className={[
        'titan-helpdesk-experience',
        'min-w-0 w-full',
        `hd-experience--${variant}`,
      ].join(' ')}
      initial={
        reducedMotion
          ? false
          : { opacity: 0, y: 12 }
      }
      animate={{
        opacity: 1,
        y: 0,
      }}
      transition={{
        duration: reducedMotion ? 0 : 0.3,
        ease: [0.22, 1, 0.36, 1],
      }}
    >
      <div
        className="hd-experience__ambient"
        aria-hidden="true"
      />

      <div className="hd-experience__content">
        <header className="hd-shell-hero">
          <div className="hd-shell-hero__main">
            <motion.div
              className="hd-shell-hero__mark"
              initial={false}
              whileHover={
                reducedMotion
                  ? undefined
                  : { rotate: -7, scale: 1.06 }
              }
              transition={{
                type: 'spring',
                stiffness: 260,
                damping: 18,
              }}
            >
              <Headset
                size={28}
                strokeWidth={1.9}
                aria-hidden="true"
              />
            </motion.div>

            <div className="hd-shell-hero__copy">
              <span className="hd-shell-hero__eyebrow">
                <Sparkles
                  size={13}
                  aria-hidden="true"
                />
                TITANMDM · {header.eyebrow}
              </span>

              <h1>{header.title}</h1>

              <p>{header.description}</p>
            </div>
          </div>

          <div className="hd-shell-hero__actions">
            <span className="hd-shell-hero__security">
              <ShieldCheck size={15} />
              Plataforma de soporte
            </span>

            {showBack && (
              <Link
                className="hd-shell-hero__back"
                to={home}
              >
                <ArrowLeft size={16} />
                Volver a la bandeja
                <ArrowUpRight size={13} />
              </Link>
            )}
          </div>

          <div
            className="hd-shell-hero__orb hd-shell-hero__orb--one"
            aria-hidden="true"
          />

          <div
            className="hd-shell-hero__orb hd-shell-hero__orb--two"
            aria-hidden="true"
          />
        </header>

        <div className="hd-shell-content">
          {children}
        </div>
      </div>
    </motion.div>
  )
}
