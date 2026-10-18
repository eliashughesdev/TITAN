import { useEffect, useRef, useState } from 'react'
import type {
  CSSProperties,
  FC,
  FormEvent,
} from 'react'

import { AnimatePresence, motion } from 'motion/react'

import {
  reducedSpriteMotion,
  spriteMotion,
} from './animations'

import { SPRITE_MAP, type FiorellaAction } from './types'

import './Fiorella.css'

const MODULE_NAMES: Record<string, string> = {
  '/dashboard': 'Dashboard',
  '/mirror': 'Escritura en relojes',
  '/records': 'Ponches',
  '/db-records': 'Historial SQL',
  '/remote-punch': 'Ponche Remoto',
  '/devices': 'Dispositivos',
  '/employees': 'Empleados',
  '/collaborators': 'Colaboradores',
  '/schedules': 'Horarios',
  '/biometric': 'Inventario Biométrico',
  '/bulk': 'Operaciones Masivas',
  '/reports': 'Reportes',
  '/export': 'Exportar Datos',
  '/sync-history': 'Historial Sync',
  '/users': 'Usuarios',
  '/settings': 'Configuración',
  '/advanced-reports': 'Reportes Avanzados',
}

export interface ChatMessage {
  id: string
  sender: 'user' | 'fiorella'
  text: string
}

interface FiorellaProps {
  action?: string
  x?: number
  facing?: 'left' | 'right'
  line?: string

  reducedMotion?: boolean

  onSpriteClick?: () => void
  onHide?: () => void

  isChatOpen?: boolean

  messages?: ChatMessage[]
  loading?: boolean
  connection?: 'unknown' | 'connected' | 'error'
  provider?: string

  activeModule?: string

  onSendMessage?: (msg: string) => void
  onClearChat?: () => void
  onCloseChat?: () => void
  onMinimizeChat?: () => void
  onCancelRequest?: () => void
}

/*
 * Sprites únicos que existen en disco. Se precargan una sola vez
 * para evitar "saltos" visuales al cambiar de acción.
 */
const PRELOAD_SPRITES = Array.from(
  new Set(Object.values(SPRITE_MAP)),
)

export const Fiorella: FC<FiorellaProps> = ({
  action = 'idle',
  x = 48,
  facing = 'right',
  line,

  reducedMotion = false,

  onSpriteClick,
  onHide,

  isChatOpen = false,

  messages = [],
  loading = false,
  connection = 'unknown',
  provider = 'Ponches',

  activeModule = '/dashboard',

  onSendMessage,
  onClearChat,
  onCloseChat,
  onMinimizeChat,
  onCancelRequest,
}) => {
  const [input, setInput] = useState('')
  const [imgError, setImgError] = useState(false)

  const messagesEndRef =
    useRef<HTMLDivElement | null>(null)

  const spanishModuleName =
    MODULE_NAMES[activeModule] || 'General'

  useEffect(() => {
    if (!isChatOpen) {
      return
    }

    const target = messagesEndRef.current

    if (
      target &&
      typeof target.scrollIntoView === 'function'
    ) {
      target.scrollIntoView({
        behavior: reducedMotion ? 'auto' : 'smooth',
      })
    }
  }, [messages, loading, isChatOpen, reducedMotion])

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()

    const cleanInput = input.trim()

    if (!cleanInput || loading) {
      return
    }

    onSendMessage?.(cleanInput)
    setInput('')
  }

  /*
   * La acción puede llegar como string arbitrario. Se normaliza
   * a un estado conocido para elegir sprite y movimiento.
   */
  const safeAction: FiorellaAction =
    Object.prototype.hasOwnProperty.call(
      spriteMotion,
      action,
    )
      ? (action as FiorellaAction)
      : 'idle'

  const spriteSrc =
    SPRITE_MAP[safeAction] || SPRITE_MAP.idle

  const spriteAnimation = reducedMotion
    ? reducedSpriteMotion[safeAction]
    : spriteMotion[safeAction]

  const containerStyle: CSSProperties = isChatOpen
    ? { right: '24px', bottom: '16px' }
    : { left: `${x}px`, bottom: '16px' }

  return (
    <div
      className={`
        fixed
        z-50
        flex
        flex-col
        items-end
        gap-2
        pointer-events-auto
        select-none
        ${reducedMotion ? '' : 'transition-all duration-300 ease-out'}
      `}
      style={containerStyle}
    >
      <div className="hidden" aria-hidden="true">
        {PRELOAD_SPRITES.map((src) => (
          <img
            key={src}
            src={src}
            alt=""
            draggable={false}
          />
        ))}
      </div>

      <AnimatePresence>
        {isChatOpen && (
          <motion.div
            key="fiorella-chat"
            initial={{ opacity: 0, y: 12, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 12, scale: 0.98 }}
            transition={{
              duration: reducedMotion ? 0 : 0.22,
              ease: 'easeOut',
            }}
            className="origin-bottom-right"
          >
            <div
              className="
                w-80
                max-w-[calc(100vw-2rem)]
                bg-white
                rounded-lg
                shadow-2xl
                border
                border-gray-200
                overflow-hidden
                flex
                flex-col
              "
            >
              <div
                className="
                  bg-red-800
                  text-white
                  px-4
                  py-2.5
                  flex
                  justify-between
                  items-center
                  text-sm
                  font-semibold
                  shadow
                "
              >
                <span>
                  Fiorella AI / {spanishModuleName}
                  <small className="fiorella-chat__connection">
                    <i data-state={connection} />
                    {connection === 'connected'
                      ? `Conectada · ${provider}`
                      : connection === 'error'
                        ? 'Sin conexión'
                        : 'Estado pendiente'}
                  </small>
                </span>

                <div className="flex items-center gap-1">
                  {onClearChat && (
                    <button
                      type="button"
                      onClick={() => {
                        const confirmar =
                          window.confirm(
                            '¿Deseas limpiar la conversación con Fiorella?',
                          )

                        if (confirmar) {
                          onClearChat()
                        }
                      }}
                      title="Limpiar conversación"
                      aria-label="Limpiar conversación"
                      className="
                        hover:bg-red-900
                        text-white
                        px-2
                        py-1
                        rounded
                        font-bold
                        transition-colors
                      "
                    >
                      🗑
                    </button>
                  )}

                  {onMinimizeChat && (
                    <button
                      type="button"
                      onClick={onMinimizeChat}
                      title="Minimizar chat"
                      aria-label="Minimizar chat"
                      className="
                        hover:bg-red-900
                        text-white
                        px-2
                        py-1
                        rounded
                        font-bold
                        transition-colors
                      "
                    >
                      —
                    </button>
                  )}

                  {onCloseChat && (
                    <button
                      type="button"
                      onClick={onCloseChat}
                      title="Cerrar chat"
                      aria-label="Cerrar chat"
                      className="
                        hover:bg-red-900
                        text-white
                        px-2
                        py-1
                        rounded
                        font-bold
                        transition-colors
                        text-lg
                        leading-none
                      "
                    >
                      ×
                    </button>
                  )}
                </div>
              </div>

              <div
                className="
                  h-72
                  p-3
                  overflow-y-auto
                  space-y-2.5
                  text-xs
                  bg-gray-50
                  flex
                  flex-col
                "
              >
                {messages && messages.length > 0 ? (
                  messages.map((msg) => (
                    <div
                      key={msg.id}
                      className={`
                        max-w-[85%]
                        p-2.5
                        rounded-lg
                        text-xs
                        leading-relaxed
                        shadow-sm
                        ${
                          msg.sender === 'user'
                            ? `
                              bg-red-700
                              text-white
                              self-end
                              ml-auto
                              rounded-br-none
                            `
                            : `
                              bg-white
                              text-gray-800
                              border
                              border-gray-200
                              self-start
                              rounded-bl-none
                            `
                        }
                      `}
                    >
                      {msg.text}
                    </div>
                  ))
                ) : (
                  <div
                    className="
                      text-gray-400
                      italic
                      text-center
                      my-auto
                      text-[11px]
                    "
                  >
                    Sin mensajes en esta sesión.
                  </div>
                )}

                {loading && (
                  <div className="fiorella-chat__thinking">
                    <span>Fiorella está pensando...</span>
                    {onCancelRequest && (
                      <button
                        type="button"
                        onClick={onCancelRequest}
                      >
                        Detener
                      </button>
                    )}
                  </div>
                )}

                <div ref={messagesEndRef} />
              </div>

              <form
                onSubmit={handleSubmit}
                className="
                  p-2
                  border-t
                  border-gray-200
                  flex
                  gap-1.5
                  bg-white
                "
              >
                <input
                  type="text"
                  value={input}
                  onChange={(e) =>
                    setInput(e.target.value)
                  }
                  placeholder="Escribe un comando..."
                  disabled={loading}
                  autoComplete="off"
                  className="
                    flex-1
                    border
                    border-gray-300
                    rounded
                    px-2.5
                    py-1.5
                    text-xs
                    focus:outline-none
                    focus:border-red-600
                    shadow-inner
                    disabled:bg-gray-100
                  "
                />

                <button
                  type="submit"
                  disabled={loading || !input.trim()}
                  className="
                    bg-red-700
                    text-white
                    px-3
                    py-1.5
                    rounded
                    text-xs
                    font-semibold
                    hover:bg-red-800
                    disabled:opacity-50
                    disabled:cursor-not-allowed
                    shadow
                    transition-colors
                  "
                >
                  {loading ? '...' : 'Enviar'}
                </button>
              </form>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {!isChatOpen && line && (
          <motion.div
            key="fiorella-bubble"
            initial={{ opacity: 0, y: 8, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.96 }}
            transition={{
              duration: reducedMotion ? 0 : 0.22,
              ease: 'easeOut',
            }}
            className="
              bg-white
              text-gray-800
              px-3
              py-1.5
              rounded-lg
              shadow-lg
              border
              border-gray-200
              text-xs
              font-medium
              max-w-xs
              mb-1
            "
          >
            {line}
          </motion.div>
        )}
      </AnimatePresence>

      <motion.div
        onClick={onSpriteClick}
        className="
          cursor-pointer
          flex
          items-center
          justify-center
          relative
        "
        title="Hablar con Fiorella"
        initial={{ opacity: 0, y: 14, scale: 0.9 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{
          duration: reducedMotion ? 0 : 0.35,
          ease: [0.22, 1, 0.36, 1],
        }}
        whileHover={
          reducedMotion ? undefined : { scale: 1.04 }
        }
        whileTap={
          reducedMotion ? undefined : { scale: 0.96 }
        }
      >
        {!imgError ? (
          <span
            className="fiorella-direction"
            style={{
              transform:
                facing === 'left'
                  ? 'scaleX(-1)'
                  : undefined,
            }}
          >
            <motion.img
              src={spriteSrc}
              alt="Fiorella Avatar"
              draggable={false}
              className="
                fiorella-motion
                h-28
                w-auto
                object-contain
                drop-shadow-md
              "
              animate={spriteAnimation}
              onError={() => setImgError(true)}
            />
          </span>
        ) : (
          <div
            className="
              w-14
              h-14
              bg-red-700
              text-white
              rounded-full
              flex
              items-center
              justify-center
              text-2xl
              font-bold
              shadow-lg
              border-2
              border-white
            "
          >
            👩
          </div>
        )}

        <div
          className="
            absolute
            -bottom-1
            -right-1
            w-5
            h-5
            rounded-full
            bg-white
            border
            border-gray-200
            shadow
            flex
            items-center
            justify-center
            text-[10px]
          "
          title="Fiorella AI"
        >
          ✨
        </div>
      </motion.div>

      {onHide && !isChatOpen && (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation()
            onHide()
          }}
          title="Ocultar Fiorella"
          className="
            text-[10px]
            bg-white
            border
            border-gray-200
            text-gray-500
            px-2
            py-0.5
            rounded-full
            shadow-sm
            hover:text-red-700
            hover:border-red-300
            transition-colors
          "
        >
          Ocultar
        </button>
      )}
    </div>
  )
}

export default Fiorella
