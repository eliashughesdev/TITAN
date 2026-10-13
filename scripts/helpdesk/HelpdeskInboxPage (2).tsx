import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import axios from 'axios'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { ArrowRight, CheckCircle2, ClipboardList, LayoutGrid, Plus, RefreshCw, Search, Users, X, AlertTriangle, UserRoundCheck } from 'lucide-react'
import apiClient from '../../api/apiClient'
import { useAuth } from '../../auth/AuthContext'
import { helpdeskPermissions as P, legacyHelpdeskPermissions as L } from '../../auth/helpdeskAccess'
import { HelpdeskCreateRequest } from './HelpdeskCreateRequest'
import { HelpdeskAutomationHealthPanel } from './HelpdeskAutomationHealthPanel'
import { HelpdeskKanbanBoard, type KanbanTicket } from './HelpdeskKanbanBoard'
import './HelpdeskPages.css'
import './HelpdeskWorkPage.css'
import './HelpdeskInboxWorkflow.css'

type View = 'mine' | 'unassigned' | 'all' | 'kanban'
type Ticket = KanbanTicket & { requesterName: string; assigneeName: string | null }
type ListResult = { items: Ticket[]; total: number; page: number; pageSize: number }
type Agent = { userId: string; name: string; openTickets: number; isAvailable: boolean; capacity: number }
type Workload = { assignedToMe: number; unassigned: number; active: number; agents: Agent[] }
type KanbanStatus = 'new' | 'open' | 'inprogress' | 'pendinguser'

const statusNames: Record<string, string> = {
  new: 'Nuevo', open: 'Abierto', inprogress: 'En proceso',
  pendinguser: 'Esperando usuario', resolved: 'Resuelto', closed: 'Cerrado',
}
const priorityNames: Record<string, string> = {
  low: 'Baja', medium: 'Media', high: 'Alta', critical: 'Crítica', urgent: 'Urgente',
}
const formatDate = (value: string) => {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Sin fecha' : date.toLocaleString('es-DO', { dateStyle: 'short', timeStyle: 'short' })
}
const messageFor = (error: unknown) => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string } | undefined
    return data?.message || (error.response?.status === 403
      ? 'No tienes permiso para esta operación.'
      : `No se pudo completar la operación (HTTP ${error.response?.status ?? 'sin conexión'}).`)
  }
  return error instanceof Error ? error.message : 'No se pudo completar la operación.'
}
const validView = (value: string | null): View =>
  value === 'unassigned' || value === 'all' || value === 'kanban' ? value : 'mine'

export function HelpdeskInboxPage() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const { hasPermission } = useAuth()
  const reducedMotion = useReducedMotion()

  const allowed = useCallback((...permissions: string[]) => permissions.some(p => hasPermission(p)), [hasPermission])
  const canCreate = allowed(P.requestCreate, L.ticketCreate, P.adminAccess)
  const canMine = allowed(P.inboxMyWork, P.agentAccess, P.adminAccess)
  const canUnassigned = allowed(P.inboxUnassigned, P.adminAccess)
  const canAll = allowed(P.inboxAll, P.adminAccess)
  const canKanban = allowed(P.kanbanView, P.agentAccess, P.adminAccess)
  const canAssign = allowed(P.ticketAssign, P.ticketTake, P.adminAccess, L.ticketAssign, L.manage)
  const canTransition = allowed(P.ticketTransition, P.adminAccess, L.ticketComment, L.manage)

  const requestedView = validView(params.get('view'))
  const authorizedView: View = requestedView === 'mine' && !canMine
    ? (canUnassigned ? 'unassigned' : canAll ? 'all' : canKanban ? 'kanban' : 'mine')
    : requestedView === 'unassigned' && !canUnassigned ? 'mine'
    : requestedView === 'all' && !canAll ? 'mine'
    : requestedView === 'kanban' && !canKanban ? 'mine' : requestedView

  const [view, setView] = useState<View>(authorizedView)
  const [tickets, setTickets] = useState<Ticket[]>([])
  const [workload, setWorkload] = useState<Workload | null>(null)
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [input, setInput] = useState(params.get('search') ?? '')
  const [search, setSearch] = useState(params.get('search') ?? '')
  const [status, setStatus] = useState(params.get('status') ?? '')
  const [priority, setPriority] = useState(params.get('priority') ?? '')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [workloadError, setWorkloadError] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [movingTicketId, setMovingTicketId] = useState<string | null>(null)
  const [assigning, setAssigning] = useState<Ticket | null>(null)
  const [agentId, setAgentId] = useState('')
  const [agentSearch, setAgentSearch] = useState('')
  const [savingAssignment, setSavingAssignment] = useState(false)
  const [assignmentError, setAssignmentError] = useState('')

  const pageSize = view === 'kanban' ? 100 : 25
  const pages = Math.max(1, Math.ceil(total / pageSize))

  useEffect(() => {
    if (view !== authorizedView) { setView(authorizedView); setPage(1) }
  }, [view, authorizedView])

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    setError('')
    try {
      const response = await apiClient.get<ListResult>('/helpdesk/workload/tickets', {
        params: {
          view, search: search || undefined,
          status: view === 'kanban' ? undefined : status || undefined,
          priority: priority || undefined, page, pageSize,
        },
        signal,
      })
      if (!signal?.aborted) {
        setTickets(response.data.items ?? [])
        setTotal(response.data.total ?? 0)
      }
    } catch (exception) {
      if (!signal?.aborted) { setError(messageFor(exception)); setTickets([]); setTotal(0) }
    } finally { if (!signal?.aborted) setLoading(false) }
  }, [view, search, status, priority, page, pageSize])

  const loadWorkload = useCallback(async () => {
    try {
      const response = await apiClient.get<Workload>('/helpdesk/workload')
      setWorkload(response.data)
      setWorkloadError(false)
    } catch { setWorkloadError(true) }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])
  useEffect(() => { void loadWorkload() }, [loadWorkload])

  const tabs = useMemo(() => [
    canMine ? { key: 'mine' as const, name: 'Mi trabajo', count: workload?.assignedToMe } : null,
    canUnassigned ? { key: 'unassigned' as const, name: 'Sin asignar', count: workload?.unassigned } : null,
    canAll ? { key: 'all' as const, name: 'Todos', count: workload?.active } : null,
    canKanban ? { key: 'kanban' as const, name: 'Kanban', count: workload?.active } : null,
  ].filter((tab): tab is NonNullable<typeof tab> => tab !== null), [canMine, canUnassigned, canAll, canKanban, workload])

  function updateUrl(next: { view?: View; search?: string; status?: string; priority?: string }) {
    const copy = new URLSearchParams(params)
    copy.set('workspace', 'helpdesk')
    for (const [key, value] of Object.entries(next)) {
      if (!value || (key === 'view' && value === 'mine')) copy.delete(key)
      else copy.set(key, value)
    }
    setParams(copy, { replace: true })
  }

  function changeView(next: View) {
    setView(next); setPage(1); setError(''); setNotice('')
    updateUrl({ view: next })
  }

  function openTicket(id: string) {
    const current = new URLSearchParams(params)
    current.set('workspace', 'helpdesk')
    const returnTo = `/helpdesk?${current.toString()}`
    navigate(`/helpdesk/tickets/${encodeURIComponent(id)}?workspace=helpdesk&returnTo=${encodeURIComponent(returnTo)}`)
  }

  async function refresh() {
    setNotice('')
    await Promise.all([load(), loadWorkload()])
  }

  function applySearch(event?: FormEvent) {
    event?.preventDefault()
    const term = input.trim()
    setSearch(term); setPage(1); updateUrl({ search: term })
  }

  async function transitionFromKanban(ticket: KanbanTicket, next: KanbanStatus) {
    if (!canTransition || movingTicketId || ticket.status === next) return
    setMovingTicketId(ticket.id); setError(''); setNotice('')
    try {
      await apiClient.post(`/helpdesk/tickets/${encodeURIComponent(ticket.id)}/transition`, { status: next })
      setTickets(previous => previous.map(item => item.id === ticket.id
        ? { ...item, status: next, updatedAtUtc: new Date().toISOString() } : item))
      setNotice(`${ticket.number}: ${statusNames[next]}.`)
      await loadWorkload()
    } catch (exception) { setError(messageFor(exception)); await load() }
    finally { setMovingTicketId(null) }
  }

  const agents = useMemo(() => (workload?.agents ?? []).filter(a =>
    a.name.toLowerCase().includes(agentSearch.trim().toLowerCase()))
    .sort((a, b) => Number(b.isAvailable) - Number(a.isAvailable) ||
      (a.capacity > 0 ? a.openTickets / a.capacity : 1) - (b.capacity > 0 ? b.openTickets / b.capacity : 1)),
  [workload, agentSearch])
  const selectedAgent = agents.find(a => a.userId === agentId)
  const canSubmitAssignment = Boolean(selectedAgent?.isAvailable && selectedAgent.capacity > selectedAgent.openTickets && !savingAssignment)

  function startAssignment(ticket: Ticket) {
    setAgentId(''); setAgentSearch(''); setAssignmentError(''); setAssigning(ticket)
    void loadWorkload()
  }

  async function assignTicket() {
    if (!assigning || !selectedAgent || !canAssign || !canSubmitAssignment) return
    setSavingAssignment(true); setAssignmentError('')
    try {
      await apiClient.post(`/helpdesk/tickets/${encodeURIComponent(assigning.id)}/assign`, { assigneeUserId: selectedAgent.userId })
      const number = assigning.number
      setAssigning(null)
      setNotice(`${number} asignado a ${selectedAgent.name}.`)
      await Promise.all([load(), loadWorkload()])
    } catch (exception) { setAssignmentError(messageFor(exception)) }
    finally { setSavingAssignment(false) }
  }

  return (
    <main className="titan-page helpdesk-page helpdesk-inbox helpdesk-work">
      <header className="helpdesk-inbox__header">
        <div>
          <span className="helpdesk-inbox__eyebrow">
            {view === 'kanban' ? <LayoutGrid size={15} /> : <ClipboardList size={15} />}
            OPERACIÓN TIC
          </span>
          <h1>{view === 'kanban' ? 'Tablero Kanban' : 'Bandeja de Mesa de Ayuda'}</h1>
          <p>Gestiona solicitudes, asignaciones, prioridades y tiempos de atención desde una sola pantalla.</p>
        </div>
        <div className="helpdesk-inbox__header-actions">
          <button type="button" className="helpdesk-ui-button helpdesk-ui-button--secondary" disabled={loading} onClick={() => void refresh()}>
            <RefreshCw size={16} /> Actualizar
          </button>
          {canCreate && <button type="button" className="helpdesk-ui-button helpdesk-ui-button--primary" onClick={() => setShowCreate(true)}><Plus size={16} /> Nuevo ticket</button>}
        </div>
      </header>

      {notice && <div className="hd-workflow-notice" role="status"><CheckCircle2 size={16} /> {notice}</div>}
      {error && <div className="helpdesk-inbox__error" role="alert">{error}<button type="button" onClick={() => void refresh()}>Reintentar</button></div>}
      {workloadError && <div className="helpdesk-inbox__error" role="alert"><AlertTriangle size={16} /> No se pudo actualizar la carga de técnicos.</div>}

      <HelpdeskAutomationHealthPanel />

      <nav className="helpdesk-work__tabs" aria-label="Bandejas de tickets">
        {tabs.map(tab => <button key={tab.key} type="button" aria-pressed={view === tab.key}
          className={view === tab.key ? 'is-active' : ''} onClick={() => changeView(tab.key)}>
          {tab.name}{tab.count !== undefined && <strong>{tab.count}</strong>}
        </button>)}
      </nav>

      <div className={`helpdesk-work__layout${view === 'kanban' ? ' helpdesk-work__layout--kanban' : ''}`}>
        <section className="helpdesk-inbox__panel">
          <form className="helpdesk-inbox__filters" onSubmit={applySearch} aria-label="Filtrar tickets">
            <label className="helpdesk-inbox__search"><Search size={16} /><input value={input} onChange={e => setInput(e.target.value)} placeholder="Número, asunto o categoría" aria-label="Buscar tickets" /></label>
            {view !== 'kanban' && <select value={status} aria-label="Estado" onChange={e => { setStatus(e.target.value); setPage(1); updateUrl({ status: e.target.value }) }}>
              <option value="">Todos los estados</option>{Object.entries(statusNames).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
            </select>}
            <select value={priority} aria-label="Prioridad" onChange={e => { setPriority(e.target.value); setPage(1); updateUrl({ priority: e.target.value }) }}>
              <option value="">Todas las prioridades</option>{Object.entries(priorityNames).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
            </select>
            <button type="submit" className="helpdesk-ui-button helpdesk-ui-button--primary">Buscar</button>
            <button type="button" className="helpdesk-inbox__clear" onClick={() => {
              setInput(''); setSearch(''); setStatus(''); setPriority(''); setPage(1)
              updateUrl({ search: '', status: '', priority: '' })
            }}>Limpiar</button>
          </form>

          {view === 'kanban' ? (
            <>
              {loading ? <div className="helpdesk-inbox__empty">Cargando tablero…</div> :
                <HelpdeskKanbanBoard tickets={tickets} canTransition={canTransition} movingTicketId={movingTicketId}
                  onOpen={openTicket} onTransition={transitionFromKanban} formatDate={formatDate} />}
              <footer className="helpdesk-inbox__footer"><span>{total} tickets activos · hasta 100 por página</span>
                <div><button type="button" disabled={loading || page <= 1} onClick={() => setPage(p => p - 1)}>Anterior</button>
                  <span> {page}/{pages} </span><button type="button" disabled={loading || page >= pages} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
              </footer>
            </>
          ) : (
            <>
              <div className="helpdesk-inbox__panel-heading"><div><h2>{view === 'mine' ? 'Asignadas a mí' : view === 'unassigned' ? 'Pendientes de asignación' : 'Todas las solicitudes'}</h2><p>Consulta, asigna y atiende casos.</p></div><span className="helpdesk-inbox__total">{total} resultados</span></div>
              <div className="helpdesk-inbox__table-wrap"><table className="helpdesk-inbox__table"><thead><tr>
                <th>Solicitud</th><th>Estado</th><th>Prioridad</th><th>Solicitante</th><th>Asignado</th><th>Actualizado</th><th>Acciones</th>
              </tr></thead><tbody>
                {loading ? <tr><td colSpan={7} className="helpdesk-inbox__empty">Cargando…</td></tr> :
                  tickets.length === 0 ? <tr><td colSpan={7} className="helpdesk-inbox__empty">Sin solicitudes en esta vista.</td></tr> :
                    tickets.map(ticket => <tr key={ticket.id}>
                      <td><button type="button" className="helpdesk-inbox__ticket-link" onClick={() => openTicket(ticket.id)}><span>{ticket.number}</span><strong>{ticket.subject}</strong>{ticket.slaBreached && <small>SLA vencido</small>}</button></td>
                      <td><span className={`helpdesk-inbox__badge helpdesk-inbox__badge--${ticket.status}`}>{statusNames[ticket.status] ?? ticket.status}</span></td>
                      <td>{priorityNames[ticket.priority] ?? ticket.priority}</td><td>{ticket.requesterName}</td><td>{ticket.assigneeName ?? 'Sin asignar'}</td><td>{formatDate(ticket.updatedAtUtc)}</td>
                      <td><div className="flex items-center gap-2"><button type="button" className="helpdesk-inbox__open" aria-label={`Abrir ${ticket.number}`} onClick={() => openTicket(ticket.id)}><ArrowRight size={17} /></button>
                        {canAssign && ticket.status !== 'closed' && ticket.status !== 'resolved' && <button type="button" className="helpdesk-ui-button helpdesk-ui-button--secondary" onClick={() => startAssignment(ticket)}><Users size={15} /> Asignar</button>}</div></td>
                    </tr>)}
              </tbody></table></div>
              <footer className="helpdesk-inbox__footer"><span>Página {page} de {pages}</span><div className="flex gap-2">
                <button type="button" disabled={loading || page <= 1} onClick={() => setPage(p => p - 1)}>Anterior</button>
                <button type="button" disabled={loading || page >= pages} onClick={() => setPage(p => p + 1)}>Siguiente</button>
              </div></footer>
            </>
          )}
        </section>

        {view !== 'kanban' && <aside className="helpdesk-work__agents" aria-label="Carga del equipo">
          <div><Users size={19} /><div><h2>Equipo TIC</h2><p>Carga activa y disponibilidad</p></div></div>
          {!workload ? <p>{workloadError ? 'Carga no disponible.' : 'Cargando equipo…'}</p> :
            workload.agents.length === 0 ? <p>No hay técnicos configurados.</p> : workload.agents.map(agent =>
              <article key={agent.userId}><div><strong>{agent.name}</strong><span className={agent.isAvailable ? 'is-available' : ''}>{agent.isAvailable ? 'Disponible' : 'No disponible'}</span></div>
                <p>{agent.openTickets} activos · capacidad {agent.capacity}</p></article>)}
        </aside>}
      </div>

      {showCreate && canCreate && <HelpdeskCreateRequest console onCancel={() => setShowCreate(false)} onCreated={id => { setShowCreate(false); openTicket(id) }} />}

      <AnimatePresence>
        {assigning && canAssign && <motion.div className="fixed inset-0 z-[1200] grid place-items-center bg-slate-950/60 p-4 backdrop-blur-md" role="presentation"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          onMouseDown={event => { if (event.target === event.currentTarget && !savingAssignment) setAssigning(null) }}>
          <motion.section role="dialog" aria-modal="true" aria-labelledby="hd-assignment-title"
            className="flex w-full max-w-2xl flex-col overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-2xl"
            initial={reducedMotion ? false : { opacity: 0, y: 20, scale: .98 }} animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={reducedMotion ? undefined : { opacity: 0, y: 10 }} transition={{ duration: reducedMotion ? 0 : .22 }}>
            <header className="flex items-start justify-between gap-4 border-b border-slate-200 bg-gradient-to-r from-indigo-50 to-sky-50 p-5">
              <div><div className="text-xs font-bold tracking-widest text-indigo-600">ASIGNACIÓN TIC</div>
                <h2 id="hd-assignment-title" className="mt-1 text-xl font-bold text-slate-900">Asignar {assigning.number}</h2>
                <p className="mt-1 text-sm text-slate-600">{assigning.subject}</p></div>
              <button type="button" onClick={() => setAssigning(null)} disabled={savingAssignment} aria-label="Cerrar" className="rounded-xl p-2 text-slate-600 hover:bg-white"><X size={20} /></button>
            </header>
            <div className="min-h-0 space-y-3 overflow-y-auto p-5">
              <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3"><Search size={16} />
                <input className="min-h-11 flex-1 border-0 bg-transparent outline-none" value={agentSearch} onChange={e => setAgentSearch(e.target.value)} placeholder="Buscar técnico" autoFocus />
              </label>
              <div className="max-h-80 space-y-2 overflow-y-auto">
                {agents.length === 0 && <p className="p-4 text-sm text-slate-600">No hay técnicos disponibles para esta búsqueda.</p>}
                {agents.map(agent => {
                  const selectable = agent.isAvailable && agent.capacity > agent.openTickets
                  const selected = agent.userId === agentId
                  return <button type="button" key={agent.userId} disabled={!selectable || savingAssignment} aria-pressed={selected}
                    className={`flex w-full items-center justify-between gap-4 rounded-xl border p-3 text-left transition ${selected ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 hover:border-indigo-300'} disabled:opacity-50`}
                    onClick={() => setAgentId(agent.userId)}>
                    <div><strong className="text-sm text-slate-800">{agent.name}</strong><p className="text-xs text-slate-500">{agent.openTickets} activos · máximo {agent.capacity}</p></div>
                    {selected ? <CheckCircle2 size={20} className="text-indigo-600" /> : <span className="text-xs text-slate-500">{selectable ? 'Disponible' : 'No disponible'}</span>}
                  </button>
                })}
              </div>
              {assignmentError && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{assignmentError}</p>}
            </div>
            <footer className="flex justify-end gap-3 border-t border-slate-200 p-4">
              <button type="button" disabled={savingAssignment} className="helpdesk-ui-button helpdesk-ui-button--secondary" onClick={() => setAssigning(null)}>Cancelar</button>
              <button type="button" disabled={!canSubmitAssignment} className="helpdesk-ui-button helpdesk-ui-button--primary" onClick={() => void assignTicket()}><UserRoundCheck size={16} /> {savingAssignment ? 'Asignando…' : 'Confirmar asignación'}</button>
            </footer>
          </motion.section>
        </motion.div>}
      </AnimatePresence>
    </main>
  )
}
