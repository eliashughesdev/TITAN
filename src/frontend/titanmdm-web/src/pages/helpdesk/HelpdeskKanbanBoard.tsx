import {
  useState,
  type DragEvent,
} from 'react'

import {
  AlertTriangle,
  GripVertical,
} from 'lucide-react'

import './HelpdeskKanbanBoard.css'

export type KanbanTicket = {
  id: string
  number: string
  subject: string
  status: string
  priority: string
  category: string
  requesterName: string
  assigneeName: string | null
  updatedAtUtc: string
  slaBreached: boolean
}

type KanbanStatus =
  | 'new'
  | 'open'
  | 'inprogress'
  | 'pendinguser'

type Props = {
  tickets:
    KanbanTicket[]

  canTransition:
    boolean

  movingTicketId:
    string | null

  onOpen:
    (
      id: string,
    ) => void

  onTransition:
    (
      ticket:
        KanbanTicket,

      targetStatus:
        KanbanStatus,
    ) => Promise<void>

  formatDate:
    (
      value: string,
    ) => string
}

const PRIORITY:
  Record<string, string> = {
    low:
      'Baja',

    medium:
      'Media',

    high:
      'Alta',

    critical:
      'Crítica',

    urgent:
      'Urgente',
  }

const COLUMNS:
  {
    key: KanbanStatus
    label: string
    description: string
  }[] = [
    {
      key:
        'new',

      label:
        'Nuevos',

      description:
        'Pendientes de atención',
    },

    {
      key:
        'open',

      label:
        'Abiertos',

      description:
        'Aceptados por TIC',
    },

    {
      key:
        'inprogress',

      label:
        'En proceso',

      description:
        'Trabajo activo',
    },

    {
      key:
        'pendinguser',

      label:
        'Esperando usuario',

      description:
        'SLA pausado',
    },
  ]

function canMove(
  current:
    string,

  target:
    KanbanStatus,
) {
  if (
    current ===
    target
  ) {
    return false
  }

  if (
    current ===
    'new'
  ) {
    return (
      target ===
        'open'
      ||
      target ===
        'inprogress'
      ||
      target ===
        'pendinguser'
    )
  }

  if (
    current ===
    'open'
  ) {
    return (
      target ===
        'inprogress'
      ||
      target ===
        'pendinguser'
    )
  }

  if (
    current ===
    'inprogress'
  ) {
    return (
      target ===
        'open'
      ||
      target ===
        'pendinguser'
    )
  }

  if (
    current ===
    'pendinguser'
  ) {
    return (
      target ===
        'open'
      ||
      target ===
        'inprogress'
    )
  }

  return false
}

export function HelpdeskKanbanBoard({
  tickets,
  canTransition,
  movingTicketId,
  onOpen,
  onTransition,
  formatDate,
}: Props) {
  const [
    draggingId,
    setDraggingId,
  ] =
    useState<string | null>(
      null,
    )

  const [
    overStatus,
    setOverStatus,
  ] =
    useState<KanbanStatus | null>(
      null,
    )

  function beginDrag(
    event:
      DragEvent<HTMLDivElement>,

    ticket:
      KanbanTicket,
  ) {
    if (!canTransition) {
      event.preventDefault()
      return
    }

    setDraggingId(
      ticket.id,
    )

    event.dataTransfer.effectAllowed =
      'move'

    event.dataTransfer.setData(
      'text/plain',
      ticket.id,
    )
  }

  function endDrag() {
    setDraggingId(
      null,
    )

    setOverStatus(
      null,
    )
  }

  function allowDrop(
    event:
      DragEvent<HTMLElement>,

    status:
      KanbanStatus,
  ) {
    if (!canTransition) {
      return
    }

    event.preventDefault()

    event.dataTransfer.dropEffect =
      'move'

    setOverStatus(
      status,
    )
  }

  async function drop(
    event:
      DragEvent<HTMLElement>,

    targetStatus:
      KanbanStatus,
  ) {
    event.preventDefault()

    const id =
      event.dataTransfer.getData(
        'text/plain',
      )
      ||
      draggingId

    setOverStatus(
      null,
    )

    if (
      !id
      ||
      !canTransition
    ) {
      return
    }

    const ticket =
      tickets.find(
        item =>
          item.id ===
          id,
      )

    if (
      !ticket
      ||
      !canMove(
        ticket.status,
        targetStatus,
      )
    ) {
      setDraggingId(
        null,
      )

      return
    }

    try {
      await onTransition(
        ticket,
        targetStatus,
      )
    }
    finally {
      setDraggingId(
        null,
      )
    }
  }

  return (
    <>
      {!canTransition && (
        <div
          className="hdkb__readonly"
        >
          <AlertTriangle
            size={15}
          />

          Puedes consultar el Kanban,
          pero tu rol no permite
          cambiar estados.
        </div>
      )}

      <div
        className="helpdesk-kanban"
      >
        {COLUMNS.map(
          column => {
            const columnTickets =
              tickets.filter(
                ticket =>
                  ticket.status ===
                  column.key,
              )

            return (
              <section
                key={
                  column.key
                }
                className={
                  `helpdesk-kanban__column ` +
                  `helpdesk-kanban__column--${column.key}` +
                  (
                    overStatus ===
                      column.key
                      ? ' hdkb__column--over'
                      : ''
                  )
                }
                onDragOver={
                  event =>
                    allowDrop(
                      event,
                      column.key,
                    )
                }
                onDragLeave={
                  () => {
                    if (
                      overStatus ===
                      column.key
                    ) {
                      setOverStatus(
                        null,
                      )
                    }
                  }
                }
                onDrop={
                  event =>
                    void drop(
                      event,
                      column.key,
                    )
                }
              >
                <header>
                  <div>
                    <strong>
                      {
                        column.label
                      }
                    </strong>

                    <small
                      className="hdkb__column-description"
                    >
                      {
                        column.description
                      }
                    </small>
                  </div>

                  <span>
                    {
                      columnTickets.length
                    }
                  </span>
                </header>

                <div
                  className="helpdesk-kanban__cards"
                >
                  {columnTickets.length ===
                    0 ? (
                    <p
                      className="helpdesk-kanban__empty"
                    >
                      Sin tickets.
                    </p>
                  ) : (
                    columnTickets.map(
                      item => (
                        <div
                          key={
                            item.id
                          }
                          draggable={
                            canTransition
                            &&
                            movingTicketId !==
                              item.id
                          }
                          className={
                            `helpdesk-kanban__card ` +
                            (
                              draggingId ===
                                item.id
                                ? 'hdkb__card--dragging'
                                : ''
                            ) +
                            (
                              movingTicketId ===
                                item.id
                                ? ' hdkb__card--saving'
                                : ''
                            )
                          }
                          onDragStart={
                            event =>
                              beginDrag(
                                event,
                                item,
                              )
                          }
                          onDragEnd={
                            endDrag
                          }
                        >
                          <div
                            className="hdkb__drag-row"
                          >
                            {canTransition && (
                              <GripVertical
                                size={14}
                              />
                            )}

                            <button
                              type="button"
                              className="hdkb__ticket-number"
                              onClick={
                                () =>
                                  onOpen(
                                    item.id,
                                  )
                              }
                            >
                              {
                                item.number
                              }
                            </button>

                            <span
                              className={
                                `helpdesk-kanban__priority ` +
                                `helpdesk-kanban__priority--${item.priority}`
                              }
                            >
                              {
                                PRIORITY[
                                  item.priority
                                ]
                                ??
                                item.priority
                              }
                            </span>
                          </div>

                          <button
                            type="button"
                            className="hdkb__subject"
                            onClick={
                              () =>
                                onOpen(
                                  item.id,
                                )
                            }
                          >
                            {
                              item.subject
                            }
                          </button>

                          <small>
                            {
                              item.category
                            }
                          </small>

                          <div
                            className="helpdesk-kanban__meta"
                          >
                            <span>
                              {
                                item.assigneeName
                                ??
                                'Sin asignar'
                              }
                            </span>

                            <span>
                              {
                                formatDate(
                                  item.updatedAtUtc,
                                )
                              }
                            </span>
                          </div>

                          {item.slaBreached && (
                            <span
                              className="helpdesk-kanban__sla"
                            >
                              SLA vencido
                            </span>
                          )}

                          {movingTicketId ===
                            item.id && (
                            <span
                              className="hdkb__saving-label"
                            >
                              Actualizando…
                            </span>
                          )}
                        </div>
                      ),
                    )
                  )}
                </div>
              </section>
            )
          },
        )}
      </div>
    </>
  )
}