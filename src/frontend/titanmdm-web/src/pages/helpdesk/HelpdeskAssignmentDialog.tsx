
import {
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  AnimatePresence,
  motion,
  useReducedMotion,
} from 'motion/react'

import {
  AlertCircle,
  CheckCircle2,
  LoaderCircle,
  Search,
  UserRoundCheck,
  UsersRound,
  X,
} from 'lucide-react'

import axios from 'axios'

import apiClient from '../../api/apiClient'

import './HelpdeskAssignmentDialog.css'

export interface HelpdeskAssignmentTicket {
  id: string
  number: string
  subject: string
  assigneeName: string | null
}

export interface HelpdeskAssignmentAgent {
  userId: string
  name: string
  openTickets: number
  isAvailable: boolean
  capacity: number
}

interface Props {
  ticket: HelpdeskAssignmentTicket | null

  agents: HelpdeskAssignmentAgent[]

  onClose: () => void

  onAssigned: (
    ticketId: string,
    agentId: string,
    agentName: string,
  ) => Promise<void> | void
}

function getError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const message = (
      error.response?.data as {
        message?: string
      } | undefined
    )?.message

    if (message) return message

    if (error.response?.status === 403) {
      return 'Tu usuario no tiene permiso para asignar este ticket.'
    }

    if (error.response?.status === 409) {
      return 'La asignación entró en conflicto. Actualiza la bandeja.'
    }

    return `No se completó la asignación (HTTP ${
      error.response?.status ?? 'sin conexión'
    }).`
  }

  return error instanceof Error
    ? error.message
    : 'No fue posible asignar el ticket.'
}

function utilization(
  agent: HelpdeskAssignmentAgent,
): number {
  if (agent.capacity <= 0) return 100

  return Math.min(
    100,
    Math.max(
      0,
      (agent.openTickets / agent.capacity) * 100,
    ),
  )
}

export function HelpdeskAssignmentDialog({
  ticket,
  agents,
  onClose,
  onAssigned,
}: Props) {
  const reducedMotion = useReducedMotion()

  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!ticket) {
      setSearch('')
      setSelectedId('')
      setError('')
    }
  }, [ticket])

  useEffect(() => {
    if (!ticket) return

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !saving) {
        onClose()
      }
    }

    window.addEventListener('keydown', onKeyDown)

    return () => {
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [ticket, saving, onClose])

  const visibleAgents = useMemo(() => {
    const term = search.trim().toLowerCase()

    return agents
      .filter(agent =>
        agent.name.toLowerCase().includes(term),
      )
      .sort((a, b) => {
        const availableA =
          a.isAvailable && a.openTickets < a.capacity

        const availableB =
          b.isAvailable && b.openTickets < b.capacity

        if (availableA !== availableB) {
          return availableA ? -1 : 1
        }

        const diff = utilization(a) - utilization(b)

        return diff !== 0
          ? diff
          : a.name.localeCompare(b.name)
      })
  }, [agents, search])

  const selectedAgent = agents.find(
    agent => agent.userId === selectedId,
  )

  const canSubmit = Boolean(
    ticket &&
    selectedAgent &&
    selectedAgent.isAvailable &&
    selectedAgent.capacity > selectedAgent.openTickets &&
    !saving,
  )

  async function assign() {
    if (!ticket || !selectedAgent || !canSubmit) {
      return
    }

    setSaving(true)
    setError('')

    try {
      await apiClient.post(
        `/helpdesk/tickets/${encodeURIComponent(ticket.id)}/assign`,
        {
          assigneeUserId: selectedAgent.userId,
        },
      )

      await onAssigned(
        ticket.id,
        selectedAgent.userId,
        selectedAgent.name,
      )

      onClose()
    } catch (exception) {
      setError(getError(exception))
    } finally {
      setSaving(false)
    }
  }

  return (
    <AnimatePresence>
      {ticket && (
        <motion.div
          key={ticket.id}
          className="hd-assign__overlay"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{
            duration: reducedMotion ? 0 : 0.18,
          }}
          onMouseDown={event => {
            if (
              event.target === event.currentTarget &&
              !saving
            ) {
              onClose()
            }
          }}
        >
          <motion.section
            role="dialog"
            aria-modal="true"
            aria-labelledby="hd-assign-title"
            aria-describedby="hd-assign-description"
            className="hd-assign__dialog"
            initial={
              reducedMotion
                ? false
                : {
                    opacity: 0,
                    y: 22,
                    scale: 0.98,
                  }
            }
            animate={{
              opacity: 1,
              y: 0,
              scale: 1,
            }}
            exit={
              reducedMotion
                ? undefined
                : {
                    opacity: 0,
                    y: 12,
                    scale: 0.98,
                  }
            }
            transition={{
              duration: reducedMotion ? 0 : 0.25,
              ease: [0.22, 1, 0.36, 1],
            }}
          >
            <header className="hd-assign__header">
              <div className="hd-assign__heading">
                <span className="hd-assign__header-icon">
                  <UsersRound size={21} />
                </span>

                <div>
                  <span className="hd-assign__eyebrow">
                    GESTIÓN DE ASIGNACIONES
                  </span>

                  <h2 id="hd-assign-title">
                    Asignar técnico
                  </h2>

                  <p id="hd-assign-description">
                    Selecciona un técnico para la solicitud
                    {' '}
                    <strong>{ticket.number}</strong>.
                  </p>
                </div>
              </div>

              <button
                type="button"
                className="hd-assign__close"
                onClick={onClose}
                disabled={saving}
                aria-label="Cerrar asignación"
              >
                <X size={20} />
              </button>
            </header>

            <div className="hd-assign__ticket">
              <span>Ticket seleccionado</span>

              <strong>{ticket.subject}</strong>

              <small>
                Asignado actualmente:
                {' '}
                {ticket.assigneeName ?? 'Sin asignar'}
              </small>
            </div>

            <div className="hd-assign__body">
              <label
                htmlFor="hd-assign-search"
                className="hd-assign__search"
              >
                <Search size={17} />

                <input
                  id="hd-assign-search"
                  value={search}
                  onChange={event =>
                    setSearch(event.target.value)
                  }
                  placeholder="Buscar técnico..."
                  autoComplete="off"
                  autoFocus
                />
              </label>

              <div className="hd-assign__summary">
                <span>
                  {visibleAgents.length}
                  {' '}
                  técnicos
                </span>

                <span>
                  Ordenados por disponibilidad y carga
                </span>
              </div>

              <div className="hd-assign__agents">
                {visibleAgents.length === 0 && (
                  <div className="hd-assign__empty">
                    <UsersRound size={28} />
                    <strong>
                      No encontramos técnicos
                    </strong>
                    <span>
                      Prueba otro nombre o revisa la
                      configuración de grupos TIC.
                    </span>
                  </div>
                )}

                {visibleAgents.map(agent => {
                  const percentage = utilization(agent)

                  const full =
                    agent.capacity <= agent.openTickets

                  const selectable =
                    agent.isAvailable &&
                    !full

                  const selected =
                    selectedId === agent.userId

                  return (
                    <button
                      type="button"
                      key={agent.userId}
                      className={[
                        'hd-assign__agent',
                        selected ? 'is-selected' : '',
                        !selectable ? 'is-unavailable' : '',
                      ].join(' ')}
                      aria-pressed={selected}
                      disabled={!selectable || saving}
                      onClick={() =>
                        setSelectedId(agent.userId)
                      }
                    >
                      <span className="hd-assign__avatar">
                        {agent.name
                          .trim()
                          .split(/\s+/)
                          .slice(0, 2)
                          .map(part => part.charAt(0))
                          .join('')
                          .toUpperCase()}
                      </span>

                      <span className="hd-assign__agent-content">
                        <span className="hd-assign__agent-top">
                          <strong>{agent.name}</strong>

                          <span
                            className={
                              selectable
                                ? 'hd-assign__status is-available'
                                : 'hd-assign__status'
                            }
                          >
                            {selectable
                              ? 'Disponible'
                              : full
                                ? 'Capacidad completa'
                                : 'No disponible'}
                          </span>
                        </span>

                        <span className="hd-assign__agent-bottom">
                          {agent.openTickets} activos
                          {' '}
                          · capacidad {agent.capacity}
                        </span>

                        <span
                          className="hd-assign__progress"
                          aria-hidden="true"
                        >
                          <span
                            style={{
                              width: `${percentage}%`,
                            }}
                          />
                        </span>
                      </span>

                      {selected ? (
                        <CheckCircle2
                          size={21}
                          className="hd-assign__selected-icon"
                        />
                      ) : (
                        <UserRoundCheck
                          size={20}
                          className="hd-assign__select-icon"
                        />
                      )}
                    </button>
                  )
                })}
              </div>

              {error && (
                <div
                  className="hd-assign__error"
                  role="alert"
                >
                  <AlertCircle size={18} />
                  <span>{error}</span>
                </div>
              )}
            </div>

            <footer className="hd-assign__footer">
              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--secondary"
                onClick={onClose}
                disabled={saving}
              >
                Cancelar
              </button>

              <button
                type="button"
                className="helpdesk-ui-button helpdesk-ui-button--primary"
                disabled={!canSubmit}
                onClick={() => void assign()}
              >
                {saving ? (
                  <LoaderCircle
                    size={17}
                    className="hd-assign__spinner"
                  />
                ) : (
                  <UserRoundCheck size={17} />
                )}

                {saving
                  ? 'Asignando...'
                  : 'Confirmar asignación'}
              </button>
            </footer>
          </motion.section>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
