import { useMemo } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import {
  AlertTriangle, ArrowUpRight, CheckCircle2, Clock3,
  Inbox, Layers3, UserRoundCheck, UsersRound,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import './HelpdeskOperationsOverview.css'

export type OverviewTicket = {
  id: string
  number: string
  subject: string
  status: string
  priority: string
  slaBreached: boolean
}
export type OverviewWorkload = {
  assignedToMe: number
  unassigned: number
  active: number
  agents: Array<{
    userId: string
    name: string
    openTickets: number
    isAvailable: boolean
    capacity: number
  }>
}
type Props = {
  workload: OverviewWorkload | null
  tickets: OverviewTicket[]
  loading: boolean
  onView: (view: 'mine' | 'unassigned' | 'all' | 'kanban') => void
  onOpen: (id: string) => void
  canSeeUnassigned: boolean
  canSeeAll: boolean
  canSeeKanban: boolean
}
export function HelpdeskOperationsOverview({
  workload, tickets, loading, onView, onOpen,
  canSeeUnassigned, canSeeAll, canSeeKanban,
}: Props) {
  const reduced = useReducedMotion()
  const breached = useMemo(
    () => tickets.filter(x => x.slaBreached).length, [tickets],
  )
  const urgent = useMemo(
    () => tickets.filter(x => ['critical', 'urgent', 'high'].includes(x.priority)).length,
    [tickets],
  )
  const available = useMemo(
    () => workload?.agents.filter(a =>
      a.isAvailable && a.capacity > a.openTickets,
    ).length ?? 0,
    [workload],
  )
  const metrics = [
    { title: 'Mi trabajo', number: workload?.assignedToMe, icon: Inbox, tone: 'blue',
      click: () => onView('mine'), enabled: true, description: 'Mis casos activos' },
    { title: 'Sin asignar', number: workload?.unassigned, icon: UserRoundCheck, tone: 'amber',
      click: () => onView('unassigned'), enabled: canSeeUnassigned, description: 'Esperando un técnico' },
    { title: 'Trabajo activo', number: workload?.active, icon: Layers3, tone: 'violet',
      click: () => onView('all'), enabled: canSeeAll, description: 'Tickets en seguimiento' },
    { title: 'Técnicos disponibles', number: available, icon: UsersRound, tone: 'green',
      click: () => onView('kanban'), enabled: canSeeKanban, description: 'Con capacidad de atención' },
  ].filter(item => item.enabled)

  return (
    <section className="hd-overview" aria-label="Resumen de operación Helpdesk">
      <div className="hd-overview__heading">
        <div>
          <span className="hd-overview__eyebrow"><Clock3 size={14}/> CENTRO DE OPERACIONES</span>
          <h2>Visión de la mesa de ayuda</h2>
          <p>Indicadores vivos de tu equipo, accesos rápidos y prioridades de atención.</p>
        </div>
        <Link className="hd-overview__link" to="/helpdesk/seguimiento?workspace=helpdesk">
          Seguimiento SLA <ArrowUpRight size={16}/>
        </Link>
      </div>

      <div className="hd-overview__metrics">
        {metrics.map((m, i) => (
          <motion.button key={m.title} type="button"
            className={`hd-overview__metric hd-overview__metric--${m.tone}`}
            onClick={m.click}
            initial={reduced ? false : { opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.23, delay: reduced ? 0 : i * 0.045 }}
            whileHover={reduced ? undefined : { y: -3 }}
          >
            <span className="hd-overview__metric-icon"><m.icon size={20}/></span>
            <span className="hd-overview__metric-copy">
              <span>{m.title}</span>
              <strong aria-label={`${m.title}: ${m.number ?? 'sin datos'}`}>
                {loading && !workload ? '···' : (m.number ?? '—')}
              </strong>
              <small>{m.description}</small>
            </span>
            <ArrowUpRight className="hd-overview__corner" size={15}/>
          </motion.button>
        ))}
      </div>

      <div className="hd-overview__signals">
        <span className={breached > 0 ? 'hd-overview__signal is-alert' : 'hd-overview__signal'}>
          <AlertTriangle size={16}/> {breached} SLA vencidos en la página actual
        </span>
        <span className="hd-overview__signal">
          <Clock3 size={16}/> {urgent} casos altos o críticos en la página actual
        </span>
        <span className="hd-overview__signal">
          <CheckCircle2 size={16}/> {available} técnicos con capacidad
        </span>
      </div>

      {breached > 0 && (
        <div className="hd-overview__attention">
          <strong>Atención prioritaria</strong>
          <div className="hd-overview__attention-items">
            {tickets.filter(t => t.slaBreached).slice(0, 3).map(t => (
              <button key={t.id} type="button" onClick={() => onOpen(t.id)}>
                <AlertTriangle size={14}/>
                <span>{t.number} · {t.subject}</span>
                <ArrowUpRight size={14}/>
              </button>
            ))}
          </div>
        </div>
      )}
    </section>
  )
}
