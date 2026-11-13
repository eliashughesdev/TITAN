import { useEffect, useId, useRef, useState } from 'react'
import {
  motion,
  useMotionValue,
  useSpring,
  type Transition,
} from 'motion/react'

import type { TitanAnimationState } from '../context/TitanAssistantContext'
import { expressionFor, type TitanExpression } from '../config/titanExpression'

/*
 * Titan: robot de cuerpo azul cobalto y plata brillante, placa pectoral
 * con escudo y la palabra TITAN, y ojos LED expresivos.
 *
 * Movimiento:
 *  - Reposo: levitación continua (curvas seno con easeInOut), sin saltos.
 *  - Gestos y expresiones: física de muelle (spring) con inercia.
 *  - Parpadeo: cierre y apertura suaves, a intervalos irregulares.
 *  - Mirada: los ojos siguen al puntero con un muelle amortiguado.
 *
 * Los LED (ojos, escudo, juntas, antena, propulsor) usan las variables
 * CSS --titan-led / --titan-glow: toman el color del módulo activo y
 * cambian con una transición suave (ver titan-assistant.css).
 */

interface Pose {
  head: number
  armLeft: number
  armRight: number
  lift: number
  eyeX: number
  eyeY: number
  eyeScaleY: number
  eyeTilt: number
}

const POSES: Record<TitanExpression, Pose> = {
  idle: { head: 0, armLeft: 0, armRight: 0, lift: 0, eyeX: 0, eyeY: 0, eyeScaleY: 1, eyeTilt: 0 },
  thinking: { head: -7, armLeft: 0, armRight: -118, lift: -2, eyeX: 1.6, eyeY: -2.2, eyeScaleY: 0.82, eyeTilt: 0 },
  talking: { head: 3, armLeft: 0, armRight: -32, lift: -1, eyeX: 0, eyeY: 0, eyeScaleY: 1, eyeTilt: 0 },
  success: { head: 4, armLeft: 0, armRight: -150, lift: -6, eyeX: 0, eyeY: -1.2, eyeScaleY: 0.42, eyeTilt: 0 },
  warning: { head: 6, armLeft: 18, armRight: -18, lift: 0, eyeX: 0, eyeY: 0.6, eyeScaleY: 0.78, eyeTilt: 10 },
  error: { head: 8, armLeft: 26, armRight: -26, lift: 2, eyeX: 0, eyeY: 1, eyeScaleY: 0.7, eyeTilt: 14 },
  sleeping: { head: 9, armLeft: 6, armRight: -6, lift: 3, eyeX: 0, eyeY: 1.4, eyeScaleY: 0.12, eyeTilt: 0 },
}

/** Muelle con inercia: llega sin rebotes bruscos ni detenciones secas. */
const SPRING: Transition = { type: 'spring', stiffness: 120, damping: 15, mass: 0.9 }
const SOFT_SPRING: Transition = { type: 'spring', stiffness: 70, damping: 14, mass: 1 }

interface Props {
  animation: TitanAnimationState
  /** "head": solo la cabeza (Titan minimizado). */
  variant?: 'full' | 'head'
  followPointer: boolean
  reducedMotion: boolean
}

export function TitanAvatarCanvas({
  animation,
  variant = 'full',
  followPointer,
  reducedMotion,
}: Props) {
  const uid = useId().replace(/[^a-zA-Z0-9_-]/g, '')
  const id = (name: string) => `titan-${name}-${uid}`

  const svgRef = useRef<SVGSVGElement>(null)
  const expression = expressionFor(animation)
  const pose = POSES[expression]
  const animate = !reducedMotion

  // Mirada: muelle amortiguado hacia el puntero.
  const lookX = useMotionValue(0)
  const lookY = useMotionValue(0)
  const eyeX = useSpring(lookX, { stiffness: 110, damping: 16, mass: 0.6 })
  const eyeY = useSpring(lookY, { stiffness: 110, damping: 16, mass: 0.6 })

  useEffect(() => {
    if (!followPointer || reducedMotion) {
      lookX.set(0)
      lookY.set(0)
      return
    }

    const onMove = (event: PointerEvent) => {
      const rect = svgRef.current?.getBoundingClientRect()
      if (!rect) return
      const cx = rect.left + rect.width / 2
      const cy = rect.top + rect.height * 0.25
      const dx = event.clientX - cx
      const dy = event.clientY - cy
      const distance = Math.max(1, Math.hypot(dx, dy))
      const reach = Math.min(1, distance / 260)
      lookX.set((dx / distance) * 2.4 * reach)
      lookY.set((dy / distance) * 1.8 * reach)
    }

    window.addEventListener('pointermove', onMove, { passive: true })
    return () => window.removeEventListener('pointermove', onMove)
  }, [followPointer, reducedMotion, lookX, lookY])

  // Parpadeo natural: intervalos irregulares, a veces doble.
  const [blink, setBlink] = useState(false)
  useEffect(() => {
    if (!animate || expression === 'sleeping') return

    let timer = 0
    const schedule = () => {
      timer = window.setTimeout(() => {
        setBlink(true)
        timer = window.setTimeout(() => {
          setBlink(false)
          if (Math.random() < 0.18) {
            timer = window.setTimeout(() => {
              setBlink(true)
              timer = window.setTimeout(() => {
                setBlink(false)
                schedule()
              }, 130)
            }, 190)
          } else {
            schedule()
          }
        }, 130)
      }, 3200 + Math.random() * 3600)
    }

    schedule()
    return () => window.clearTimeout(timer)
  }, [animate, expression])

  const float = animate
    ? {
        animate: { y: [0, -4.5, 0] },
        transition: { duration: 4.2, ease: 'easeInOut' as const, repeat: Infinity },
      }
    : {}

  const sway = animate
    ? {
        animate: { rotate: [-1.1, 1.1, -1.1] },
        transition: { duration: 7.5, ease: 'easeInOut' as const, repeat: Infinity },
      }
    : {}

  const viewBox = variant === 'head' ? '20 0 80 72' : '0 0 120 170'

  return (
    <svg
      ref={svgRef}
      className="titan-avatar-canvas"
      data-expression={expression}
      viewBox={viewBox}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={id('cobalt')} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#5b8cff" />
          <stop offset=".35" stopColor="#2453d6" />
          <stop offset=".7" stopColor="#123596" />
          <stop offset="1" stopColor="#0a1f5c" />
        </linearGradient>

        <linearGradient id={id('silver')} x1="0" y1="0" x2=".8" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset=".3" stopColor="#e8edf4" />
          <stop offset=".62" stopColor="#b9c4d2" />
          <stop offset="1" stopColor="#8492a6" />
        </linearGradient>

        <linearGradient id={id('visor')} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0d2a63" />
          <stop offset=".55" stopColor="#071636" />
          <stop offset="1" stopColor="#040b1f" />
        </linearGradient>

        <radialGradient id={id('led')}>
          <stop offset="0" stopColor="#ffffff" />
          <stop offset=".45" className="titan-led-stop" />
          <stop offset="1" className="titan-led-stop titan-led-stop--edge" />
        </radialGradient>

        <filter id={id('glow')} x="-60%" y="-60%" width="220%" height="220%">
          <feGaussianBlur stdDeviation="2.2" />
        </filter>
      </defs>

      <motion.g {...float}>
        {variant === 'full' && (
          <>
            {/* Propulsor de levitación */}
            <motion.ellipse
              className="titan-led titan-led--soft"
              cx="60"
              cy="151"
              rx="13"
              ry="3.6"
              filter={`url(#${id('glow')})`}
              animate={animate ? { opacity: [0.45, 0.85, 0.45], scaleX: [0.9, 1.08, 0.9] } : undefined}
              transition={{ duration: 4.2, ease: 'easeInOut', repeat: Infinity }}
            />
          </>
        )}

        <motion.g {...sway} style={{ originX: 0.5, originY: 0.9 }}>
          <motion.g animate={{ y: pose.lift }} transition={SOFT_SPRING}>
            {variant === 'full' && (
              <>
                {/* Brazo izquierdo */}
                <motion.g
                  animate={{ rotate: pose.armLeft }}
                  transition={SPRING}
                  style={{ originX: 0.6, originY: 0.08 }}
                >
                  <path
                    d="M27 77 Q20 92 22 105 Q24 112 30 110 Q33 96 37 82 Z"
                    fill={`url(#${id('cobalt')})`}
                    stroke="#0a1f5c"
                    strokeWidth="1.2"
                  />
                  <circle cx="26" cy="111" r="5.2" fill={`url(#${id('silver')})`} stroke="#6f7f95" strokeWidth=".8" />
                  <circle cx="32" cy="76" r="6.6" fill={`url(#${id('silver')})`} stroke="#6f7f95" strokeWidth=".8" />
                  <circle className="titan-led" cx="32" cy="76" r="2.1" />
                </motion.g>

                {/* Brazo derecho (gestos) */}
                <motion.g
                  animate={
                    expression === 'talking' && animate
                      ? { rotate: [pose.armRight, pose.armRight - 16, pose.armRight] }
                      : { rotate: pose.armRight }
                  }
                  transition={
                    expression === 'talking' && animate
                      ? { duration: 1.6, ease: 'easeInOut', repeat: Infinity }
                      : SPRING
                  }
                  style={{ originX: 0.4, originY: 0.08 }}
                >
                  <path
                    d="M93 77 Q100 92 98 105 Q96 112 90 110 Q87 96 83 82 Z"
                    fill={`url(#${id('cobalt')})`}
                    stroke="#0a1f5c"
                    strokeWidth="1.2"
                  />
                  <circle cx="94" cy="111" r="5.2" fill={`url(#${id('silver')})`} stroke="#6f7f95" strokeWidth=".8" />
                  <circle cx="88" cy="76" r="6.6" fill={`url(#${id('silver')})`} stroke="#6f7f95" strokeWidth=".8" />
                  <circle className="titan-led" cx="88" cy="76" r="2.1" />
                </motion.g>

                {/* Torso cobalto */}
                <path
                  d="M31 77 Q33 68 45 68 L75 68 Q87 68 89 77 L85 112 Q83 124 60 126 Q37 124 35 112 Z"
                  fill={`url(#${id('cobalt')})`}
                  stroke="#0a1f5c"
                  strokeWidth="1.3"
                />
                <path
                  d="M38 74 Q46 70 60 70 Q74 70 82 74"
                  fill="none"
                  stroke="#9ec0ff"
                  strokeWidth="1.4"
                  strokeLinecap="round"
                  opacity=".55"
                />

                {/* Placa pectoral plateada con escudo y TITAN */}
                <path
                  d="M43 77 L77 77 Q80 77 79.6 80 L77 104 Q76 110 60 112 Q44 110 43 104 L40.4 80 Q40 77 43 77 Z"
                  fill={`url(#${id('silver')})`}
                  stroke="#7d8ba0"
                  strokeWidth="1"
                />
                <path
                  className="titan-led titan-led--halo"
                  d="M60 79 L69.5 82 L68.6 90 Q66.8 96.4 60 99.6 Q53.2 96.4 51.4 90 L50.5 82 Z"
                  filter={`url(#${id('glow')})`}
                />
                <path
                  d="M60 79 L69.5 82 L68.6 90 Q66.8 96.4 60 99.6 Q53.2 96.4 51.4 90 L50.5 82 Z"
                  fill={`url(#${id('led')})`}
                  stroke="#0a1f5c"
                  strokeWidth=".9"
                />
                <path d="M60 83 L64.6 84.6 L64 89.4 Q63 92.6 60 94.4 Q57 92.6 56 89.4 L55.4 84.6 Z" fill="#0a1f5c" opacity=".35" />
                <motion.text
                  className="titan-led-text"
                  x="60"
                  y="107.4"
                  textAnchor="middle"
                  fontSize="6.2"
                  fontWeight="800"
                  letterSpacing="1.4"
                  fontFamily="'Segoe UI', Arial, sans-serif"
                  animate={animate ? { opacity: [0.78, 1, 0.78] } : undefined}
                  transition={{ duration: 3.6, ease: 'easeInOut', repeat: Infinity }}
                >
                  TITAN
                </motion.text>

                {/* Cuerpo inferior afinado (levita, sin piernas) */}
                <path
                  d="M40 117 Q60 131 80 117 L72 139 Q60 147 48 139 Z"
                  fill={`url(#${id('cobalt')})`}
                  stroke="#0a1f5c"
                  strokeWidth="1.2"
                />
                <ellipse className="titan-led" cx="60" cy="141" rx="6" ry="1.6" opacity=".9" />

                {/* Cuello */}
                <rect x="52" y="62" width="16" height="8" rx="3" fill="#7d8ba0" />
              </>
            )}

            {/* Cabeza (gira con muelle) */}
            <motion.g
              animate={{ rotate: pose.head }}
              transition={SPRING}
              style={{ originX: 0.5, originY: 0.8 }}
            >
              <line x1="60" y1="13" x2="60" y2="5" stroke="#8492a6" strokeWidth="2" strokeLinecap="round" />
              <motion.circle
                className="titan-led"
                cx="60"
                cy="4.2"
                r="2.8"
                animate={animate ? { opacity: [0.55, 1, 0.55] } : undefined}
                transition={{ duration: 2.8, ease: 'easeInOut', repeat: Infinity }}
              />

              <circle cx="27" cy="39" r="7" fill={`url(#${id('cobalt')})`} stroke="#0a1f5c" strokeWidth="1" />
              <circle cx="93" cy="39" r="7" fill={`url(#${id('cobalt')})`} stroke="#0a1f5c" strokeWidth="1" />
              <circle className="titan-led" cx="27" cy="39" r="2.4" />
              <circle className="titan-led" cx="93" cy="39" r="2.4" />

              <path
                d="M29 39 Q29 12 60 12 Q91 12 91 39 Q91 61 60 65 Q29 61 29 39 Z"
                fill={`url(#${id('silver')})`}
                stroke="#6f7f95"
                strokeWidth="1.2"
              />
              <path d="M36 22 Q46 15 60 15" fill="none" stroke="#ffffff" strokeWidth="2.2" strokeLinecap="round" opacity=".9" />

              {/* Visor */}
              <path
                d="M36 34 Q36 22 60 22 Q84 22 84 34 L84 43 Q84 56 60 57 Q36 56 36 43 Z"
                fill={`url(#${id('visor')})`}
                stroke="#0a1f5c"
                strokeWidth="1"
              />
              <path d="M40 30 Q48 25 58 25" fill="none" stroke="#7fa7ff" strokeWidth="1.2" strokeLinecap="round" opacity=".45" />

              {/* Ojos LED: mirada (muelle) > expresión (muelle) > parpadeo */}
              <motion.g style={{ x: eyeX, y: eyeY }}>
                <motion.g animate={{ x: pose.eyeX, y: pose.eyeY }} transition={SPRING}>
                  {[49, 71].map((cx, index) => (
                    <motion.g
                      key={cx}
                      animate={{ rotate: index === 0 ? pose.eyeTilt : -pose.eyeTilt }}
                      transition={SPRING}
                      style={{ originX: 0.5, originY: 0.5 }}
                    >
                      <motion.g
                        animate={{ scaleY: blink ? 0.08 : pose.eyeScaleY }}
                        transition={
                          blink
                            ? { duration: 0.12, ease: [0.4, 0, 0.2, 1] }
                            : SPRING
                        }
                        style={{ originX: 0.5, originY: 0.5 }}
                      >
                        <rect
                          className="titan-led titan-led--halo"
                          x={cx - 5.5}
                          y="32"
                          width="11"
                          height="14"
                          rx="5.5"
                          filter={`url(#${id('glow')})`}
                        />
                        <rect
                          className="titan-led"
                          x={cx - 4.6}
                          y="32.8"
                          width="9.2"
                          height="12.4"
                          rx="4.6"
                        />
                        <rect x={cx - 2.4} y="34.6" width="2.6" height="3.4" rx="1.3" fill="#ffffff" opacity=".85" />
                      </motion.g>
                    </motion.g>
                  ))}
                </motion.g>
              </motion.g>

              {/* Boca LED */}
              <motion.rect
                className="titan-led"
                x="53"
                y="50"
                width="14"
                height="2.2"
                rx="1.1"
                style={{ originX: 0.5, originY: 0.5 }}
                animate={
                  expression === 'talking' && animate
                    ? { scaleX: [1, 0.45, 1.05, 0.6, 1], opacity: 1 }
                    : {
                        scaleX: expression === 'success' ? 1.15 : expression === 'sleeping' ? 0.5 : 0.8,
                        opacity: expression === 'sleeping' ? 0.4 : 0.85,
                      }
                }
                transition={
                  expression === 'talking' && animate
                    ? { duration: 0.9, ease: 'easeInOut', repeat: Infinity }
                    : SPRING
                }
              />
            </motion.g>
          </motion.g>
        </motion.g>
      </motion.g>
    </svg>
  )
}
