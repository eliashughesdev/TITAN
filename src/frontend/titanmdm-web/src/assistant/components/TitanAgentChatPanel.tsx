import {
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { createPortal } from 'react-dom'
import {
  Bot,
  ClipboardList,
  Plus,
  Search,
  Send,
  X,
} from 'lucide-react'
import apiClient from '../../api/apiClient'
import '../styles/titan-agent-chat.css'

interface ChatResponse {
  answer: string
  availableActions: string[]
  module: string
}

interface AgentContext {
  tools: string[]
}

interface TicketSummary {
  id: string
  number: string
  subject: string
  status: string
}

interface ActionResponse {
  action: string
  items?: TicketSummary[]
  item?: TicketSummary & {
    description?: string
    priority?: string
  }
  id?: string
  number?: string
  message?: string
}

interface ChatLine {
  id: number
  role: 'user' | 'assistant'
  content: string
}

interface Props {
  firstName: string
  module: string
  onClose: () => void
}

function errorMessage(error: unknown): string {
  const failure = error as {
    response?: {
      status?: number
      data?: { message?: string }
    }
  }

  if (failure.response?.data?.message) {
    return failure.response.data.message
  }

  if (failure.response?.status === 403) {
    return 'Tu cuenta no tiene permiso para esta acción.'
  }

  return 'No se pudo completar la solicitud. Comprueba el backend e inténtalo de nuevo.'
}

export function TitanAgentChatPanel({
  firstName,
  module,
  onClose,
}: Props) {
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)
  const [lines, setLines] = useState<ChatLine[]>([])
  const [error, setError] = useState('')
  const [tools, setTools] = useState<string[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [subject, setSubject] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('general')

  const nextId = useRef(0)
  const mounted = useRef(true)
  const endRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<HTMLTextAreaElement | null>(null)

  useEffect(() => {
    mounted.current = true
    inputRef.current?.focus()

    void apiClient
      .get<AgentContext>('/virtual-agent/context')
      .then(({ data }) => {
        if (mounted.current) {
          setTools(data.tools ?? [])
        }
      })
      .catch(() => {
        // El chat mostrará el error concreto al enviar un mensaje.
        // Sin contexto autorizado no presentamos acciones.
      })

    return () => {
      mounted.current = false
    }
  }, [])

  useEffect(() => {
    endRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'end',
    })
  }, [lines, sending, error, showCreate])

  function addLine(
    role: ChatLine['role'],
    content: string,
  ) {
    const id = ++nextId.current

    setLines((current) => [
      ...current,
      { id, role, content },
    ])
  }

  async function sendChat(
    event?: FormEvent<HTMLFormElement>,
  ) {
    event?.preventDefault()

    const message = input.trim()

    if (message.length < 2 || sending) return

    setInput('')
    setError('')
    setSending(true)
    addLine('user', message)

    try {
      const { data } = await apiClient.post<ChatResponse>(
        '/virtual-agent/chat',
        { message, module },
      )

      if (mounted.current) {
        addLine(
          'assistant',
          data.answer ||
            'No recibí una respuesta. Inténtalo nuevamente.',
        )
      }
    } catch (cause) {
      if (mounted.current) {
        setError(errorMessage(cause))
      }
    } finally {
      if (mounted.current) {
        setSending(false)
      }
    }
  }

  async function executeAction(
    action: string,
    values: Record<string, string> = {},
  ) {
    if (sending || !tools.includes(action)) return

    setError('')
    setSending(true)

    try {
      const { data } = await apiClient.post<ActionResponse>(
        '/virtual-agent/execute',
        { action, ...values },
      )

      if (!mounted.current) return

      if (action === 'helpdesk.my_tickets') {
        const tickets = data.items ?? []

        addLine(
          'assistant',
          tickets.length
            ? [
                `Encontré ${tickets.length} ticket(s) recientes a tu nombre:`,
                ...tickets.map(
                  (ticket) =>
                    `• ${ticket.number} · ${ticket.subject} · ${ticket.status}`,
                ),
              ].join('\n')
            : 'No encontré tickets creados por tu cuenta.',
        )
      } else if (action === 'helpdesk.my_ticket') {
        const ticket = data.item

        addLine(
          'assistant',
          ticket
            ? [
                `${ticket.number} · ${ticket.subject}`,
                `Estado: ${ticket.status}`,
                ticket.priority
                  ? `Prioridad: ${ticket.priority}`
                  : '',
                ticket.description ?? '',
              ]
                .filter(Boolean)
                .join('\n')
            : 'No encontré ese ticket entre tus solicitudes.',
        )
      } else if (action === 'helpdesk.create_ticket') {
        addLine(
          'assistant',
          `Ticket ${data.number ?? ''} creado para tu cuenta. ${
            data.message ?? ''
          }`.trim(),
        )

        setShowCreate(false)
        setSubject('')
        setDescription('')
        setCategory('general')
      }
    } catch (cause) {
      if (mounted.current) {
        setError(errorMessage(cause))
      }
    } finally {
      if (mounted.current) {
        setSending(false)
      }
    }
  }

  function handleInputKeyDown(
    event: KeyboardEvent<HTMLTextAreaElement>,
  ) {
    if (
      event.key === 'Enter' &&
      !event.shiftKey &&
      !event.nativeEvent.isComposing
    ) {
      event.preventDefault()
      void sendChat()
    }
  }

  function handleCreate(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !subject.trim() ||
      !description.trim() ||
      sending
    ) {
      return
    }

    void executeAction('helpdesk.create_ticket', {
      subject: subject.trim(),
      description: description.trim(),
      category: category.trim() || 'general',
    })
  }

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
          <Bot size={25} />
        </div>

        <div className="titan-agent-chat__identity">
          <strong>Titan Assistant</strong>
          <span>
            {firstName} · {module}
          </span>
        </div>

        <button
          className="titan-agent-chat__close"
          type="button"
          aria-label="Cerrar conversación"
          onClick={onClose}
        >
          <X size={18} />
        </button>
      </header>

      <div className="titan-agent-chat__context">
        <span
          className="titan-agent-chat__status-dot"
          aria-hidden="true"
        />
        Acciones limitadas a tu cuenta y permisos
      </div>

      <div
        className="titan-agent-chat__messages"
        role="log"
        aria-label="Mensajes de Titan"
        aria-live="polite"
      >
        <div className="titan-agent-chat__message titan-agent-chat__message--assistant">
          <div className="titan-agent-chat__bubble">
            Hola, {firstName}. Puedo ayudarte con tus
            solicitudes y responder consultas según tus
            permisos.
          </div>
          <span className="titan-agent-chat__message-meta">
            Titan
          </span>
        </div>

        {lines.map((line) => (
          <div
            key={line.id}
            className={
              `titan-agent-chat__message ` +
              `titan-agent-chat__message--${line.role}`
            }
          >
            <div className="titan-agent-chat__bubble">
              {line.content}
            </div>

            <span className="titan-agent-chat__message-meta">
              {line.role === 'user' ? 'Tú' : 'Titan'}
            </span>
          </div>
        ))}

        {sending && (
          <div className="titan-agent-chat__message titan-agent-chat__message--assistant">
            <div className="titan-agent-chat__bubble">
              Procesando tu solicitud…
            </div>
          </div>
        )}

        <div ref={endRef} />
      </div>

      {error && (
        <div className="titan-agent-chat__error" role="alert">
          {error}
        </div>
      )}

      {tools.length > 0 && (
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 7,
            padding: '10px 15px',
            borderTop: '1px solid #e2eaf5',
          }}
          aria-label="Acciones disponibles"
        >
          {tools.includes('helpdesk.my_tickets') && (
            <button
              type="button"
              disabled={sending}
              onClick={() =>
                void executeAction('helpdesk.my_tickets')
              }
            >
              <ClipboardList size={15} /> Mis tickets
            </button>
          )}

          {tools.includes('helpdesk.my_ticket') && (
            <button
              type="button"
              disabled={sending}
              onClick={() => {
                const number = window.prompt(
                  'Pega el identificador completo del ticket:',
                )

                if (number?.trim()) {
                  void executeAction(
                    'helpdesk.my_ticket',
                    { ticketId: number.trim() },
                  )
                }
              }}
            >
              <Search size={15} /> Consultar ticket
            </button>
          )}

          {tools.includes('helpdesk.create_ticket') && (
            <button
              type="button"
              disabled={sending}
              onClick={() =>
                setShowCreate((current) => !current)
              }
            >
              <Plus size={15} /> Crear ticket
            </button>
          )}
        </div>
      )}

      {showCreate && (
        <form
          onSubmit={handleCreate}
          style={{
            display: 'grid',
            gap: 8,
            padding: '12px 15px',
            borderTop: '1px solid #e2eaf5',
          }}
        >
          <strong style={{ fontSize: 12 }}>
            Crear solicitud a tu nombre
          </strong>

          <input
            aria-label="Asunto del ticket"
            value={subject}
            onChange={(event) =>
              setSubject(event.target.value)
            }
            maxLength={250}
            placeholder="Asunto"
            required
          />

          <textarea
            aria-label="Descripción del ticket"
            value={description}
            onChange={(event) =>
              setDescription(event.target.value)
            }
            maxLength={4000}
            rows={3}
            placeholder="Describe el problema"
            required
          />

          <input
            aria-label="Categoría del ticket"
            value={category}
            onChange={(event) =>
              setCategory(event.target.value)
            }
            maxLength={80}
            placeholder="Categoría"
          />

          <button type="submit" disabled={sending}>
            Confirmar creación
          </button>
        </form>
      )}

      <form
        className="titan-agent-chat__composer"
        onSubmit={(event) => void sendChat(event)}
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

        <button
          className="titan-agent-chat__send"
          type="submit"
          aria-label="Enviar mensaje"
          disabled={sending || input.trim().length < 2}
        >
          <Send size={19} />
        </button>
      </form>

      <div className="titan-agent-chat__hint">
        Enter para enviar · Shift + Enter para nueva línea
      </div>
    </section>,
    document.body,
  )
}