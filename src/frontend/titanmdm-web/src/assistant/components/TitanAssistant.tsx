import { MessageCircle, Settings2 } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import {
  useTitanAssistant,
  type TitanAnimationState,
  type TitanPosition,
} from '../context/TitanAssistantContext'
import {
  TitanMovementEngine,
  type TitanMovementSnapshot,
} from '../engine/TitanMovementEngine'
import { TitanAvatarCanvas } from './TitanAvatarCanvas'
import { TitanSpeechBubble } from './TitanSpeechBubble'
import { TitanPreferencesPanel } from './TitanPreferencesPanel'
import { TitanAgentChatPanel } from './TitanAgentChatPanel'
import '../styles/titan-assistant.css'

const WIDTH = 130
const HEIGHT = 190
const MARGIN = 14

type Side = 'left' | 'right'

function edgePosition(
  side: Side,
  level: 'bottom' | 'middle' = 'bottom',
  peek = false,
): TitanPosition {
  const y = level === 'middle'
    ? Math.max(85, Math.round(window.innerHeight * 0.48) - HEIGHT / 2)
    : Math.max(85, window.innerHeight - HEIGHT - 24)

  return {
    x: side === 'right'
      ? window.innerWidth - (peek ? 65 : WIDTH + MARGIN)
      : peek ? -65 : MARGIN,
    y,
  }
}

function hasBlockingOverlay(): boolean {
  return Boolean(
    document.querySelector(
      '[role="dialog"], [aria-modal="true"], ' +
      '[role="menu"], [data-titan-blocking="true"]',
    ),
  )
}

function isSpaceOccupied(position: TitanPosition): boolean {
  const samples = [
    [position.x + 38, position.y + 42],
    [position.x + 65, position.y + 95],
    [position.x + 64, position.y + 155],
  ]

  return samples.some(([x, y]) => {
    if (
      x < 0 || y < 0 ||
      x >= window.innerWidth ||
      y >= window.innerHeight
    ) {
      return false
    }

    return document.elementsFromPoint(x, y).some((element) => {
      if (element.closest('.titan-assistant')) return false

      return Boolean(element.closest(
        'button, a, input, textarea, select, ' +
        '[role="dialog"], [role="menu"], ' +
        '[data-titan-blocking="true"]',
      ))
    })
  })
}

function visualState(
  snapshot: TitanMovementSnapshot,
): TitanAnimationState {
  switch (snapshot.state) {
    case 'walking':
      return 'walking'
    case 'prepare-takeoff':
      return 'prepare-takeoff'
    case 'takeoff':
      return 'takeoff'
    case 'flying':
      return 'flying'
    case 'landing':
      return 'landing'
    default:
      return 'idle'
  }
}

export function TitanAssistant() {
  const {
    user,
    page,
    preferences,
    runtime,
    setPosition,
    setVelocity,
    setDirection,
    setAnimation,
    say,
    clearMessage,
  } = useTitanAssistant()

  const rootRef = useRef<HTMLDivElement>(null)
  const engineRef = useRef<TitanMovementEngine | null>(null)
  const sideRef = useRef<Side>('right')
  const levelRef = useRef<'bottom' | 'middle'>('bottom')
  const peekRef = useRef(false)

  const [snapshot, setSnapshot] = useState<TitanMovementSnapshot | null>(
    null,
  )
  const [animation, setVisualAnimation] =
    useState<TitanAnimationState>('idle')
  const [chatOpen, setChatOpen] = useState(false)
  const [preferencesOpen, setPreferencesOpen] = useState(false)
  const [blocked, setBlocked] = useState(false)
  const [pointer, setPointer] = useState({ x: 0, y: 0 })

  const moveWithinEdge = useCallback((
    level: 'bottom' | 'middle',
    peek = false,
  ) => {
    const engine = engineRef.current
    if (!engine) return

    const target = edgePosition(sideRef.current, level, peek)

    levelRef.current = level
    peekRef.current = peek

    if (preferences.reducedMotion) {
      engine.setPosition(target)
      return
    }

    // La trayectoria mantiene X junto al borde.
    // El motor mueve piernas y brazos según la velocidad.
    engine.moveTo(target, { allowFlight: false })
  }, [preferences.reducedMotion])

  useEffect(() => {
    const initial = edgePosition('right')
    const engine = new TitanMovementEngine(initial)

    engine.setAllowFlight(false)

    engine.setListener((next) => {
      setSnapshot(next)
      setVisualAnimation(
        peekRef.current && !next.moving
          ? 'peeking'
          : visualState(next),
      )

      if (rootRef.current) {
        rootRef.current.style.transform =
          `translate3d(${next.position.x}px, ` +
          `${next.position.y}px, 0)`
      }
    })

    engine.setArrivalListener((next) => {
      setPosition(next.position)
      setVelocity({ x: 0, y: 0 })
      setDirection(next.direction)

      const arrivedState: TitanAnimationState =
        peekRef.current ? 'peeking' : 'idle'

      setVisualAnimation(arrivedState)
      setAnimation(arrivedState, 'autonomous')
    })

    engineRef.current = engine
    setSnapshot(engine.getSnapshot())
    setPosition(initial)

    return () => {
      engine.destroy()
      engineRef.current = null
    }
  }, [
    setAnimation,
    setDirection,
    setPosition,
    setVelocity,
  ])

  useEffect(() => {
    const onResize = () => {
      engineRef.current?.setPosition(
        edgePosition(
          sideRef.current,
          levelRef.current,
          peekRef.current,
        ),
      )
    }

    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  useEffect(() => {
    // Cada pantalla comienza con Titan apartado en el borde inferior.
    sideRef.current = 'right'
    levelRef.current = 'bottom'
    peekRef.current = false

    engineRef.current?.stop()
    engineRef.current?.setPosition(
      edgePosition('right', 'bottom'),
    )
  }, [page.pathname])

  useEffect(() => {
    const onPointerMove = (event: PointerEvent) => {
      setPointer({ x: event.clientX, y: event.clientY })
    }

    window.addEventListener('pointermove', onPointerMove, {
      passive: true,
    })

    return () => {
      window.removeEventListener('pointermove', onPointerMove)
    }
  }, [])

  useEffect(() => {
    if (
      !preferences.autonomousBehavior ||
      preferences.reducedMotion ||
      preferences.doNotDisturb ||
      chatOpen ||
      preferencesOpen
    ) {
      return
    }

    const interval = window.setInterval(() => {
      if (hasBlockingOverlay()) return

      const nextLevel =
        levelRef.current === 'bottom' ? 'middle' : 'bottom'

      const target = edgePosition(
        sideRef.current,
        nextLevel,
        false,
      )

      // Si ese tramo lateral contiene controles, se queda
      // donde está y puede asomarse sin invadir el formulario.
      if (isSpaceOccupied(target)) {
        moveWithinEdge(levelRef.current, true)
        return
      }

      moveWithinEdge(nextLevel, false)
    }, 18000)

    return () => window.clearInterval(interval)
  }, [
    chatOpen,
    preferencesOpen,
    preferences.autonomousBehavior,
    preferences.reducedMotion,
    preferences.doNotDisturb,
    moveWithinEdge,
  ])

  useEffect(() => {
    const checkSpace = () => {
      if (chatOpen || preferencesOpen) {
        setBlocked(false)
        return
      }

      const current = engineRef.current?.getSnapshot().position

      if (!current) return

      if (hasBlockingOverlay()) {
        engineRef.current?.stop()
        setBlocked(true)
        return
      }

      if (!isSpaceOccupied(current)) {
        setBlocked(false)
        return
      }

      const alternative = edgePosition(
        sideRef.current,
        levelRef.current,
        true,
      )

      if (!isSpaceOccupied(alternative)) {
        engineRef.current?.setPosition(alternative)
        peekRef.current = true
        setBlocked(false)
      } else {
        setBlocked(true)
      }
    }

    checkSpace()
    const interval = window.setInterval(checkSpace, 1500)
    return () => window.clearInterval(interval)
  }, [page.pathname, chatOpen, preferencesOpen])

  useEffect(() => {
    if (
      !user ||
      !preferences.proactiveComments ||
      preferences.doNotDisturb ||
      blocked ||
      chatOpen
    ) {
      return
    }

    const timer = window.setTimeout(() => {
      if (
        document.activeElement?.matches(
          'input, textarea, select',
        )
      ) {
        return
      }

      say(
        `Hola, ${user.firstName}. Estoy disponible si necesitas ayuda.`,
        'contextual',
      )
    }, 3500)

    return () => window.clearTimeout(timer)
  }, [
    user?.id,
    page.pathname,
    blocked,
    chatOpen,
    preferences.proactiveComments,
    preferences.doNotDisturb,
    say,
  ])

  if (!user || !preferences.visible) return null

  const position = snapshot?.position ?? edgePosition('right')

  return (
    <div
      ref={rootRef}
      className={[
        'titan-assistant',
        'titan-assistant--refined',
        blocked && !chatOpen && !preferencesOpen
          ? 'titan-assistant--blocked'
          : '',
      ].filter(Boolean).join(' ')}
      style={{
        transform:
          `translate3d(${position.x}px, ${position.y}px, 0)`,
      }}
    >
      {runtime.message && !blocked && (
        <TitanSpeechBubble
          message={runtime.message}
          onClose={clearMessage}
        />
      )}

      {preferencesOpen && (
        <TitanPreferencesPanel
          onClose={() => setPreferencesOpen(false)}
        />
      )}

      {chatOpen && (
        <TitanAgentChatPanel
          firstName={user.firstName}
          module={page.module}
          onClose={() => setChatOpen(false)}
        />
      )}

      <div className="titan-assistant__toolbar">
        <button
          type="button"
          aria-label="Abrir conversación con Titan"
          onClick={() => {
            peekRef.current = false
            engineRef.current?.setPosition(
              edgePosition(sideRef.current, levelRef.current),
            )
            setChatOpen((value) => !value)
            setPreferencesOpen(false)
          }}
        >
          <MessageCircle size={16} />
        </button>

        <button
          type="button"
          aria-label="Preferencias de Titan"
          onClick={() => {
            peekRef.current = false
            engineRef.current?.setPosition(
              edgePosition(sideRef.current, levelRef.current),
            )
            setPreferencesOpen((value) => !value)
            setChatOpen(false)
          }}
        >
          <Settings2 size={16} />
        </button>
      </div>

      <TitanAvatarCanvas
        animation={animation}
        velocity={snapshot?.velocity ?? { x: 0, y: 0 }}
        direction={snapshot?.direction ?? runtime.direction}
        pointerX={pointer.x}
        pointerY={pointer.y}
        reducedMotion={preferences.reducedMotion}
        onClick={() => {
          peekRef.current = false
          engineRef.current?.setPosition(
            edgePosition(sideRef.current, levelRef.current),
          )
          setChatOpen((value) => !value)
          setPreferencesOpen(false)
        }}
      />
    </div>
  )
}