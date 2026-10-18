import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { createPortal } from 'react-dom'
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  History,
  Plus,
  RefreshCw,
  Send,
  ShieldCheck,
  Sparkles,
  Square,
  Trash2,
  WifiOff,
  X,
} from 'lucide-react'

import {
  describeTitanFailure,
  titanAssistantApi,
  toolKindOf,
  type TitanCapabilities,
  type TitanConversationSummary,
  type TitanToolInvocation,
} from '../api/titanAssistantApi'
import {
  useTitanAssistant,
  type TitanAnimationState,
} from '../context/TitanAssistantContext'
import '../styles/titan-agent-chat.css'

type Connection =
  | 'connecting'
  | 'online'
  | 'offline'
  | 'forbidden'

interface ChatLine {
  id: number
  role: 'user' | 'assistant'
  content: string
  invocations?: TitanToolInvocation[]
  cancelled?: boolean
  failed?: boolean
}

interface Props {
  onClose: () => void
}

function prettyModule(module: string): string {
  return module
    .replace(/-/g, ' ')
    .trim() || 'general'
}

function MessageLine({
  line,
}: {
  line: ChatLine
}) {
  const invocations =
    line.invocations ?? []

  return (
    <div
      className={
        'titan-agent-chat__message ' +
        `titan-agent-chat__message--${line.role}` +
        (line.failed
          ? ' titan-agent-chat__message--failed'
          : '')
      }
    >
      <div className="titan-agent-chat__bubble">
        {line.content}
      </div>

      {invocations.length > 0 && (
        <div className="titan-agent-chat__invocations">
          {invocations.map((invocation, index) => (
            <span
              key={`${line.id}-${index}`}
              className={
                'titan-agent-chat__invocation' +
                (invocation.success
                  ? ''
                  : ' titan-agent-chat__invocation--failed')
              }
            >
              {invocation.tool} ·{' '}
              {invocation.durationMs} ms ·{' '}
              {invocation.success
                ? 'OK'
                : (invocation.errorCode ?? 'error')}
            </span>
          ))}
        </div>
      )}

      {line.cancelled && (
        <span className="titan-agent-chat__message-meta">
          Cancelada por el usuario
        </span>
      )}

      <span className="titan-agent-chat__message-meta">
        {line.role === 'user' ? 'Tú' : 'Titan'}
      </span>
    </div>
  )
}

export function TitanAgentChatPanel({
  onClose,
}: Props) {
  const {
    user,
    page,
    setAnimation,
    clearMessage,
  } = useTitanAssistant()

  const [capabilities, setCapabilities] =
    useState<TitanCapabilities | null>(null)
  const [connection, setConnection] =
    useState<Connection>('connecting')
  const [connectionError, setConnectionError] =
    useState('')
  const [lines, setLines] = useState<ChatLine[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')
  const [conversationId, setConversationId] =
    useState<string | null>(null)
  const [history, setHistory] = useState<
    TitanConversationSummary[]
  >([])
  const [loadingHistory, setLoadingHistory] =
    useState(false)
  const [screen, setScreen] =
    useState<'chat' | 'history'>('chat')

  const nextId = useRef(0)
  const mounted = useRef(true)
  const abortRef = useRef<AbortController | null>(null)
  const avatarTimer = useRef<number | null>(null)
  const endRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<HTMLTextAreaElement | null>(null)

  const firstName = user?.firstName ?? 'usuario'
  const moduleName = prettyModule(page.module)

  const clearAvatarTimer = useCallback(() => {
    if (avatarTimer.current !== null) {
      window.clearTimeout(avatarTimer.current)
      avatarTimer.current = null
    }
  }, [])

  const setAvatar = useCallback(
    (
      state: TitanAnimationState,
      resetAfter?: number,
    ) => {
      clearAvatarTimer()
      setAnimation(state, 'user')

      if (resetAfter) {
        avatarTimer.current = window.setTimeout(() => {
          avatarTimer.current = null
          clearMessage()
        }, resetAfter)
      }
    },
    [clearAvatarTimer, setAnimation, clearMessage],
  )

  const loadCapabilities = useCallback(async () => {
    try {
      const caps =
        await titanAssistantApi.getCapabilities()

      if (!mounted.current) return

      setCapabilities(caps)

      if (!caps.enabled || !caps.available) {
        setConnection('offline')
        setConnectionError(
          caps.enabled
            ? 'El proveedor de IA no está disponible.'
            : 'El asistente no está habilitado.',
        )
      } else {
        setConnection('online')
      }
    } catch (cause) {
      if (!mounted.current) return

      const failure = describeTitanFailure(cause)

      setConnection(
        failure.kind === 'forbidden'
          ? 'forbidden'
          : 'offline',
      )
      setConnectionError(failure.message)
    }
  }, [])

  const retry = useCallback(() => {
    setConnection('connecting')
    setConnectionError('')
    void loadCapabilities()
  }, [loadCapabilities])

  useEffect(() => {
    mounted.current = true
    inputRef.current?.focus()
    void loadCapabilities()

    return () => {
      mounted.current = false
      abortRef.current?.abort()
      clearAvatarTimer()
      clearMessage()
    }
  }, [
    loadCapabilities,
    clearAvatarTimer,
    clearMessage,
  ])

  useEffect(() => {
    endRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'end',
    })
  }, [lines, sending, error, screen])

  const addLine = useCallback(
    (
      role: ChatLine['role'],
      content: string,
      extra?: Partial<
        Omit<ChatLine, 'id' | 'role' | 'content'>
      >,
    ) => {
      const id = ++nextId.current

      setLines((current) => [
        ...current,
        { id, role, content, ...extra },
      ])
    },
    [],
  )

  const send = useCallback(
    async (event?: FormEvent<HTMLFormElement>) => {
      event?.preventDefault()

      const message = input.trim()

      if (message.length < 2 || sending) return

      setInput('')
      setError('')
      setSending(true)

      const controller = new AbortController()
      abortRef.current = controller

      addLine('user', message)
      setAvatar('thinking')

      try {
        const response =
          await titanAssistantApi.sendMessage(
            {
              conversationId,
              message,
              module: page.module,
            },
            controller.signal,
          )

        if (!mounted.current) return

        setConversationId(response.conversationId)

        addLine(
          'assistant',
          response.answer &&
            response.answer.trim()
            ? response.answer
            : 'No recibí una respuesta del asistente.',
          {
            invocations: response.toolInvocations,
            cancelled: response.cancelled,
          },
        )

        if (response.cancelled) {
          setAvatar('warning', 2600)
        } else {
          setAvatar('talking', 2600)
        }

        setConnection(
          response.provider === 'none'
            ? 'offline'
            : 'online',
        )
      } catch (cause) {
        if (!mounted.current) return

        const failure = describeTitanFailure(cause)

        if (failure.kind === 'cancelled') {
          addLine('assistant', 'Consulta cancelada.', {
            cancelled: true,
          })
          setAvatar('idle', 1200)
        } else {
          setError(failure.message)
          addLine('assistant', failure.message, {
            failed: true,
          })
          setAvatar('error', 3400)

          if (
            failure.kind === 'unavailable' ||
            failure.kind === 'network'
          ) {
            setConnection('offline')
          }

          if (
            failure.kind === 'forbidden' ||
            failure.kind === 'unauthorized'
          ) {
            setConnection('forbidden')
          }
        }
      } finally {
        if (mounted.current) {
          setSending(false)
          abortRef.current = null
        }
      }
    },
    [
      input,
      sending,
      conversationId,
      page.module,
      addLine,
      setAvatar,
    ],
  )

  const cancel = useCallback(() => {
    abortRef.current?.abort()
  }, [])

  const openHistory = useCallback(async () => {
    setScreen('history')
    setLoadingHistory(true)
    setError('')

    try {
      const items =
        await titanAssistantApi.listConversations()

      if (mounted.current) {
        setHistory(items)
      }
    } catch (cause) {
      if (mounted.current) {
        setError(
          describeTitanFailure(cause).message,
        )
      }
    } finally {
      if (mounted.current) {
        setLoadingHistory(false)
      }
    }
  }, [])

  const openConversation = useCallback(
    async (id: string) => {
      setLoadingHistory(true)
      setError('')

      try {
        const conversation =
          await titanAssistantApi.getConversation(id)

        if (!mounted.current) return

        const restored: ChatLine[] =
          conversation.entries.map(
            (entry, index) => ({
              id: index + 1,
              role:
                entry.role === 'user'
                  ? 'user'
                  : 'assistant',
              content: entry.content,
              invocations: entry.toolInvocations,
            }),
          )

        nextId.current = restored.length
        setLines(restored)
        setConversationId(conversation.id)
        setScreen('chat')
      } catch (cause) {
        if (mounted.current) {
          setError(
            describeTitanFailure(cause).message,
          )
        }
      } finally {
        if (mounted.current) {
          setLoadingHistory(false)
        }
      }
    },
    [],
  )

  const removeConversation = useCallback(
    async (id: string) => {
      try {
        await titanAssistantApi.deleteConversation(id)

        if (!mounted.current) return

        setHistory((current) =>
          current.filter((item) => item.id !== id),
        )

        if (conversationId === id) {
          setConversationId(null)
          setLines([])
        }
      } catch (cause) {
        if (mounted.current) {
          setError(
            describeTitanFailure(cause).message,
          )
        }
      }
    },
    [conversationId],
  )

  const startNew = useCallback(() => {
    abortRef.current?.abort()
    setConversationId(null)
    setLines([])
    setError('')
    setInput('')
    setScreen('chat')
    inputRef.current?.focus()
  }, [])

  function handleInputKeyDown(
    event: KeyboardEvent<HTMLTextAreaElement>,
  ) {
    if (
      event.key === 'Enter' &&
      !event.shiftKey &&
      !event.nativeEvent.isComposing
    ) {
      event.preventDefault()
      void send()
    }
  }

  const statusText =
    connection === 'connecting'
      ? 'Conectando con el asistente…'
      : connection === 'forbidden'
        ? 'Asistente no habilitado para tu cuenta'
        : connection === 'offline'
          ? 'Sin conexión con el asistente'
          : `${capabilities?.provider ?? 'Asistente'} · ${
              capabilities?.model || 'modelo activo'
            }`

  const toolCount = capabilities?.tools.length ?? 0

  return createPortal(
    <section
      className="titan-agent-chat"
      data-titan-blocking="true"
      aria-label="Conversación con Titan"
    >
      <header className="titan-agent-chat__header">
        <div
          className="titan-agent-chat__avatar"
          aria-hidden="true"
        >
          <Bot size={24} />
        </div>

        <div className="titan-agent-chat__identity">
          <strong>Titan Assistant</strong>
          <span>
            {firstName} · {moduleName}
          </span>
        </div>

        {screen === 'chat' ? (
          <button
            className="titan-agent-chat__icon"
            type="button"
            aria-label="Historial de conversaciones"
            onClick={() => void openHistory()}
          >
            <History size={17} />
          </button>
        ) : (
          <button
            className="titan-agent-chat__icon"
            type="button"
            aria-label="Volver a la conversación"
            onClick={() => setScreen('chat')}
          >
            <ArrowLeft size={18} />
          </button>
        )}

        <button
          className="titan-agent-chat__close"
          type="button"
          aria-label="Cerrar conversación"
          onClick={onClose}
        >
          <X size={18} />
        </button>
      </header>

      <div
        className={
          'titan-agent-chat__status ' +
          `titan-agent-chat__status--${connection}`
        }
      >
        <span
          className="titan-agent-chat__status-dot"
          aria-hidden="true"
        />
        <span className="titan-agent-chat__status-text">
          {statusText}
        </span>

        {toolCount > 0 && (
          <span className="titan-agent-chat__status-tools">
            <ShieldCheck size={12} />
            {toolCount} consultas
          </span>
        )}
      </div>

      {connection === 'offline' &&
        connectionError && (
          <div
            className="titan-agent-chat__connection"
            role="status"
          >
            <WifiOff size={13} />
            <span>{connectionError}</span>

            <button
              type="button"
              onClick={retry}
            >
              <RefreshCw size={12} />
              Reintentar
            </button>
          </div>
        )}

      {connection === 'forbidden' && (
        <div
          className="titan-agent-chat__connection titan-agent-chat__connection--denied"
          role="alert"
        >
          <AlertCircle size={13} />
          <span>{connectionError}</span>
        </div>
      )}

      {screen === 'history' ? (
        <div
          className="titan-agent-chat__history"
          role="list"
          aria-label="Conversaciones anteriores"
        >
          <button
            className="titan-agent-chat__history-new"
            type="button"
            onClick={startNew}
          >
            <Plus size={14} />
            Nueva conversación
          </button>

          {loadingHistory && (
            <div className="titan-agent-chat__history-empty">
              Cargando historial…
            </div>
          )}

          {!loadingHistory &&
            history.length === 0 && (
              <div className="titan-agent-chat__history-empty">
                Sin conversaciones previas en esta
                sesión.
              </div>
            )}

          {history.map((item) => (
            <div
              key={item.id}
              className="titan-agent-chat__history-item"
            >
              <button
                type="button"
                className="titan-agent-chat__history-open"
                onClick={() =>
                  void openConversation(item.id)
                }
              >
                <strong>
                  {item.title || 'Conversación'}
                </strong>
                <span>
                  {item.messageCount} mensajes
                </span>
              </button>

              <button
                type="button"
                className="titan-agent-chat__history-delete"
                aria-label="Eliminar conversación"
                onClick={() =>
                  void removeConversation(item.id)
                }
              >
                <Trash2 size={14} />
              </button>
            </div>
          ))}
        </div>
      ) : (
        <>
          <div
            className="titan-agent-chat__messages"
            role="log"
            aria-live="polite"
            aria-label="Mensajes de Titan"
          >
            {lines.length === 0 && !sending && (
              <div className="titan-agent-chat__empty">
                <Sparkles size={16} />
                <div>
                  Hola, {firstName}. Puedo consultar
                  información autorizada para tu cuenta
                  en el módulo {moduleName}.
                </div>
              </div>
            )}

            {lines.map((line) => (
              <MessageLine
                key={line.id}
                line={line}
              />
            ))}

            {sending && (
              <div className="titan-agent-chat__message titan-agent-chat__message--assistant">
                <div className="titan-agent-chat__bubble titan-agent-chat__thinking">
                  <span className="titan-agent-chat__thinking-dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  Procesando tu consulta…
                </div>
              </div>
            )}

            <div ref={endRef} />
          </div>

          {error && (
            <div
              className="titan-agent-chat__error"
              role="alert"
            >
              <AlertCircle size={14} />
              <span>{error}</span>
            </div>
          )}

          {toolCount > 0 && (
            <div
              className="titan-agent-chat__tools"
              aria-label="Consultas autorizadas"
            >
              {capabilities?.tools.map((tool) => (
                <span
                  key={tool.name}
                  className={
                    'titan-agent-chat__tool ' +
                    `titan-agent-chat__tool--${toolKindOf(
                      tool.kind,
                    )}`
                  }
                  title={tool.description}
                >
                  {tool.title}
                </span>
              ))}
            </div>
          )}

          <form
            className="titan-agent-chat__composer"
            onSubmit={(event) => void send(event)}
          >
            <textarea
              ref={inputRef}
              className="titan-agent-chat__input"
              aria-label="Mensaje para Titan"
              value={input}
              onChange={(event) =>
                setInput(event.target.value)
              }
              onKeyDown={handleInputKeyDown}
              maxLength={1500}
              rows={1}
              placeholder="Escribe tu consulta…"
            />

            {sending ? (
              <button
                className="titan-agent-chat__send titan-agent-chat__send--stop"
                type="button"
                aria-label="Cancelar consulta"
                onClick={cancel}
              >
                <Square size={16} />
              </button>
            ) : (
              <button
                className="titan-agent-chat__send"
                type="submit"
                aria-label="Enviar mensaje"
                disabled={input.trim().length < 2}
              >
                <Send size={18} />
              </button>
            )}
          </form>

          <div className="titan-agent-chat__hint">
            Enter para enviar · Shift + Enter para nueva
            línea · Solo consultas autorizadas
          </div>
        </>
      )}
    </section>,
    document.body,
  )
}
