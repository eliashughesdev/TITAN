import { Minus, MessageCircle, Settings2 } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState, type CSSProperties } from 'react'
import { AnimatePresence, motion, type Transition } from 'motion/react'

import {
  useTitanAssistant,
  type TitanAnimationState,
} from '../context/TitanAssistantContext'
import { useOptionalWorkspace } from '../../workspace/WorkspaceContext'
import { isTitanExcluded, resolveTitanTheme } from '../config/titanTheme'
import { useTitanPresence } from '../hooks/useTitanPresence'
import { TitanAvatarCanvas } from './TitanAvatarCanvas'
import { TitanSpeechBubble } from './TitanSpeechBubble'
import { TitanPreferencesPanel } from './TitanPreferencesPanel'
import { TitanAgentChatPanel } from './TitanAgentChatPanel'
import '../styles/titan-assistant.css'

/*
 * Titan vive acoplado en la esquina inferior derecha y no recorre la
 * pantalla: nunca tapa el área de trabajo. Mientras el usuario escribe
 * se vuelve translúcido y deja pasar los clics; bajo un diálogo se
 * aparta; puede minimizarse a una burbuja. Alt+T lo abre en cualquier
 * momento. Todos los cambios usan muelles (spring) con inercia.
 */

const MINIMIZED_KEY = 'titanmdm:titan-minimized'

const EXPRESSIVE_ANIMATIONS: ReadonlySet<TitanAnimationState> = new Set([
  'thinking',
  'talking',
  'error',
  'success',
  'warning',
  'pointing',
])

const ASSISTANT_PRIORITIES: ReadonlySet<string> = new Set([
  'assistant',
  'contextual',
  'user',
  'critical',
])

const DOCK_SPRING: Transition = { type: 'spring', stiffness: 170, damping: 22, mass: 0.9 }

type DockMode = 'active' | 'translucent' | 'hidden'

function readMinimized(): boolean {
  try {
    return window.localStorage.getItem(MINIMIZED_KEY) === 'true'
  } catch {
    return false
  }
}

function storeMinimized(value: boolean): void {
  try {
    window.localStorage.setItem(MINIMIZED_KEY, String(value))
  } catch {
    // Preferencia opcional del navegador.
  }
}

const DOCK_TARGETS: Record<DockMode, { opacity: number; scale: number; y: number }> = {
  active: { opacity: 1, scale: 1, y: 0 },
  translucent: { opacity: 0.22, scale: 0.84, y: 14 },
  hidden: { opacity: 0, scale: 0.78, y: 26 },
}

export function TitanAssistant() {
  const {
    user,
    page,
    preferences,
    runtime,
    say,
    clearMessage,
  } = useTitanAssistant()

  const workspace = useOptionalWorkspace()
  const theme = useMemo(
    () => resolveTitanTheme(workspace?.activeModule),
    [workspace?.activeModule],
  )

  const { typing, overlay } = useTitanPresence()
  const [chatOpen, setChatOpen] = useState(false)
  // Las preferencias se abren para la pantalla actual: al navegar se cierran.
  const [preferencesAt, setPreferencesAt] = useState<string | null>(null)
  const preferencesOpen = preferencesAt === page.pathname
  const setPreferencesOpen = useCallback(
    (open: boolean | ((current: boolean) => boolean)) => {
      setPreferencesAt(current => {
        const isOpen = current === page.pathname
        const next = typeof open === 'function' ? open(isOpen) : open
        return next ? page.pathname : null
      })
    },
    [page.pathname],
  )
  const [minimized, setMinimized] = useState(readMinimized)
  const [hovered, setHovered] = useState(false)

  const excluded = isTitanExcluded(page.pathname, workspace?.activeWorkspaceId)
  const panelOpen = chatOpen || preferencesOpen

  const setMinimizedPersisted = useCallback((value: boolean) => {
    setMinimized(value)
    storeMinimized(value)
  }, [])

  const openChat = useCallback(() => {
    setMinimizedPersisted(false)
    setPreferencesOpen(false)
    setChatOpen(true)
  }, [setMinimizedPersisted, setPreferencesOpen])

  // Disponible siempre: Alt+T abre la conversación desde cualquier pantalla.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.altKey && !event.ctrlKey && !event.metaKey && event.key.toLowerCase() === 't') {
        event.preventDefault()
        if (chatOpen) {
          setChatOpen(false)
        } else {
          openChat()
        }
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [chatOpen, openChat])

  const quiet =
    minimized ||
    typing ||
    overlay ||
    panelOpen ||
    preferences.doNotDisturb ||
    !preferences.proactiveComments

  useEffect(() => {
    if (!user || quiet) return

    const timer = window.setTimeout(() => {
      say(`Hola, ${user.firstName}. Estoy disponible si necesitas ayuda.`, 'contextual')
    }, 3500)

    return () => window.clearTimeout(timer)
  }, [user, page.pathname, quiet, say])

  if (!user || !preferences.visible || excluded) return null

  const mode: DockMode =
    overlay && !panelOpen
      ? 'hidden'
      : typing && !panelOpen && !hovered
        ? 'translucent'
        : 'active'

  const avatarAnimation: TitanAnimationState =
    ASSISTANT_PRIORITIES.has(runtime.priority) &&
    EXPRESSIVE_ANIMATIONS.has(runtime.animation)
      ? runtime.animation
      : preferences.doNotDisturb
        ? 'sleeping'
        : 'idle'

  const transition: Transition = preferences.reducedMotion ? { duration: 0 } : DOCK_SPRING

  const style = {
    '--titan-accent': theme.accent,
    '--titan-led': theme.led,
    '--titan-glow': theme.glow,
  } as CSSProperties

  return (
    <div
      className="titan-dock"
      data-mode={mode}
      data-minimized={minimized}
      style={style}
      onPointerEnter={() => setHovered(true)}
      onPointerLeave={() => setHovered(false)}
    >
      {runtime.message && mode === 'active' && !minimized && !panelOpen && (
        <TitanSpeechBubble message={runtime.message} onClose={clearMessage} />
      )}

      {preferencesOpen && (
        <TitanPreferencesPanel onClose={() => setPreferencesOpen(false)} />
      )}

      {chatOpen && <TitanAgentChatPanel onClose={() => setChatOpen(false)} />}

      <AnimatePresence initial={false} mode="popLayout">
        {minimized ? (
          <motion.button
            key="orb"
            type="button"
            className="titan-dock__orb"
            aria-label={`Mostrar a Titan (${theme.label}) · Alt+T`}
            title="Mostrar a Titan · Alt+T"
            initial={{ opacity: 0, scale: 0.6 }}
            animate={{ opacity: mode === 'hidden' ? 0 : mode === 'translucent' ? 0.5 : 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.6 }}
            transition={transition}
            whileHover={preferences.reducedMotion ? undefined : { scale: 1.08 }}
            whileTap={preferences.reducedMotion ? undefined : { scale: 0.94 }}
            onClick={() => setMinimizedPersisted(false)}
          >
            <TitanAvatarCanvas
              animation={avatarAnimation}
              variant="head"
              followPointer={preferences.followPointer}
              reducedMotion={preferences.reducedMotion}
            />
          </motion.button>
        ) : (
          <motion.div
            key="titan"
            className="titan-dock__body"
            initial={{ opacity: 0, scale: 0.7, y: 30 }}
            animate={DOCK_TARGETS[mode]}
            exit={{ opacity: 0, scale: 0.7, y: 30 }}
            transition={transition}
          >
            <div className="titan-dock__toolbar" role="toolbar" aria-label="Titan">
              <button type="button" aria-label="Conversar con Titan (Alt+T)" title="Conversar · Alt+T" onClick={() => (chatOpen ? setChatOpen(false) : openChat())}>
                <MessageCircle size={15} />
              </button>
              <button
                type="button"
                aria-label="Preferencias de Titan"
                title="Preferencias"
                onClick={() => {
                  setPreferencesOpen(value => !value)
                  setChatOpen(false)
                }}
              >
                <Settings2 size={15} />
              </button>
              <button
                type="button"
                aria-label="Minimizar a Titan"
                title="Minimizar"
                onClick={() => {
                  setChatOpen(false)
                  setPreferencesOpen(false)
                  setMinimizedPersisted(true)
                }}
              >
                <Minus size={15} />
              </button>
            </div>

            <motion.button
              type="button"
              className="titan-dock__avatar"
              aria-label={`Abrir Titan Assistant · ${theme.label}`}
              whileHover={preferences.reducedMotion ? undefined : { scale: 1.04 }}
              whileTap={preferences.reducedMotion ? undefined : { scale: 0.96 }}
              transition={transition}
              onClick={() => (chatOpen ? setChatOpen(false) : openChat())}
            >
              <TitanAvatarCanvas
                animation={avatarAnimation}
                followPointer={preferences.followPointer}
                reducedMotion={preferences.reducedMotion}
              />
            </motion.button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
