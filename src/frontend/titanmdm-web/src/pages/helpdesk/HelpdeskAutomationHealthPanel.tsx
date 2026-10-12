import { useCallback, useEffect, useState } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import { Activity, AlertTriangle, MailCheck, RefreshCw, Route, ShieldCheck } from 'lucide-react'
import axios from 'axios'
import apiClient from '../../api/apiClient'
import { useAuth } from '../../auth/AuthContext'
import { helpdeskPermissions as P, legacyHelpdeskPermissions as L } from '../../auth/helpdeskAccess'

type FollowUp = {
  summary: { active: number; unassigned: number; waitingUser: number; firstOverdue: number; resolutionOverdue: number; approaching: number }
  attentionTotal: number
  generatedAtUtc: string
}
type MailActivity = Record<string, unknown>
type RoutingHealth = Record<string, unknown>

function errorText(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 403) return 'El rol no permite consultar estos diagnósticos.'
    if (error.response?.status === 401) return 'La sesión expiró. Inicia sesión nuevamente.'
    return `El servidor respondió HTTP ${error.response?.status ?? 'sin conexión'}.`
  }
  return error instanceof Error ? error.message : 'Diagnóstico no disponible.'
}

export function HelpdeskAutomationHealthPanel() {
  const { hasPermission } = useAuth()
  const reduced = useReducedMotion()
  const canMail = [P.mailView, P.mailManage, P.adminAccess, L.manage].some(hasPermission)
  const canTestMail = [P.mailManage, P.adminAccess, L.manage].some(hasPermission)
  const canRouting = [P.groupsView, P.groupsManage, P.techniciansView, P.adminAccess, L.manage].some(hasPermission)
  const canFollowUp = [P.slaView, P.agentAccess, P.adminAccess, L.manage, L.ticketComment, L.ticketAssign].some(hasPermission)
  const [follow, setFollow] = useState<FollowUp | null>(null)
  const [mail, setMail] = useState<MailActivity | null>(null)
  const [routing, setRouting] = useState<RoutingHealth | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [mailTest, setMailTest] = useState('')
  const [updated, setUpdated] = useState('')

  const refresh = useCallback(async () => {
    setBusy(true)
    setError('')
    const tasks: Promise<void>[] = []
    if (canFollowUp) tasks.push(apiClient.get<FollowUp>('/helpdesk/follow-up').then(({ data }) => { setFollow(data) }))
    if (canMail) tasks.push(apiClient.get<MailActivity>('/helpdesk/mail/activity/summary').then(({ data }) => { setMail(data) }))
    if (canRouting) tasks.push(apiClient.get<RoutingHealth>('/helpdesk/routing/health').then(({ data }) => { setRouting(data) }))
    const results = await Promise.allSettled(tasks)
    const failures = results.filter(item => item.status === 'rejected') as PromiseRejectedResult[]
    if (failures.length) setError(failures.map(item => errorText(item.reason)).join(' · '))
    setUpdated(new Date().toLocaleTimeString('es-DO'))
    setBusy(false)
  }, [canFollowUp, canMail, canRouting])

  useEffect(() => { void refresh() }, [refresh])

  async function testGraph() {
    if (!canTestMail || busy) return
    setBusy(true)
    setMailTest('')
    try {
      const response = await apiClient.post('/helpdesk/mail/test', {})
      const status = (response.data as { message?: string } | undefined)?.message
      setMailTest(status || 'Se recibió una respuesta correcta de la prueba Graph.')
      await refresh()
    } catch (exception) {
      setMailTest(`Graph: ${errorText(exception)}`)
    } finally { setBusy(false) }
  }

  if (!canFollowUp && !canMail && !canRouting) return null

  return <motion.section
    initial={reduced ? false : { opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
    className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
    aria-label="Estado de automatizaciones Helpdesk">
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2"><Activity size={18} className="text-indigo-600" />
        <div><h2 className="text-base font-bold text-slate-800">Operación automática</h2>
          <p className="text-xs text-slate-500">Correo entrante, asignación y alertas SLA · {updated || 'Consultando'}</p></div>
      </div>
      <div className="flex flex-wrap gap-2">
        {canTestMail && <button type="button" onClick={() => void testGraph()} disabled={busy} className="helpdesk-ui-button helpdesk-ui-button--secondary"><MailCheck size={15} /> Probar Graph</button>}
        <button type="button" disabled={busy} onClick={() => void refresh()} className="helpdesk-ui-button helpdesk-ui-button--secondary"><RefreshCw size={15} /> Actualizar estado</button>
      </div>
    </div>
    <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
      {canFollowUp && <div className="rounded-xl bg-amber-50 p-4">
        <div className="flex items-center gap-2 text-sm font-bold text-amber-900"><AlertTriangle size={16} /> Seguimiento SLA</div>
        <p className="mt-2 text-2xl font-extrabold text-amber-900">{follow?.attentionTotal ?? '—'}</p>
        <p className="text-xs text-amber-900/80">Casos que requieren atención</p>
        <p className="mt-2 text-xs text-amber-900/75">Vencidos: {(follow?.summary.firstOverdue ?? 0) + (follow?.summary.resolutionOverdue ?? 0)} · Próximos: {follow?.summary.approaching ?? '—'}</p>
      </div>}
      {canRouting && <div className="rounded-xl bg-indigo-50 p-4">
        <div className="flex items-center gap-2 text-sm font-bold text-indigo-900"><Route size={16} /> Motor de asignación</div>
        <p className="mt-2 text-sm font-semibold text-indigo-900">{routing ? 'Diagnóstico disponible' : 'Pendiente de consulta'}</p>
        <p className="mt-2 text-xs text-indigo-900/75">Accede a la configuración de routing para reintentos y simulaciones. Sin modificaciones automáticas desde este panel.</p>
      </div>}
      {canMail && <div className="rounded-xl bg-emerald-50 p-4">
        <div className="flex items-center gap-2 text-sm font-bold text-emerald-900"><ShieldCheck size={16} /> Microsoft Graph</div>
        <p className="mt-2 text-sm font-semibold text-emerald-900">{mail ? 'Actividad consultada' : 'Pendiente de consulta'}</p>
        <p className="mt-2 text-xs text-emerald-900/75">La consulta de actividad no confirma por sí sola recepción real de correos. Usa el diagnóstico y un correo controlado.</p>
      </div>}
    </div>
    {mailTest && <p role="status" className="mt-3 rounded-xl bg-slate-50 p-3 text-sm text-slate-700">{mailTest}</p>}
    {error && <p role="alert" className="mt-3 rounded-xl bg-red-50 p-3 text-sm text-red-800">{error}</p>}
  </motion.section>
}
