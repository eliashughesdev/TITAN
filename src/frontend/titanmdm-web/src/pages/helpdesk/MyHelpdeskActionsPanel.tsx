import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import axios from 'axios'
import apiClient from '../../api/apiClient'

export function MyHelpdeskActionsPanel() {
  const { ticketId } = useParams()
  const navigate = useNavigate()

  const [status, setStatus] = useState('')
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    setStatus('')
    setError('')
    setReason('')

    if (ticketId) {
      void apiClient
        .get<{ status: string }>(
          `/my/helpdesk/tickets/${ticketId}`,
          { signal: controller.signal },
        )
        .then(result => {
          if (!controller.signal.aborted) {
            setStatus(result.data.status)
          }
        })
        .catch(() => {})
    }

    return () => controller.abort()
  }, [ticketId])

  async function act(confirm: boolean) {
    if (!ticketId || saving) return

    setSaving(true)
    setError('')

    try {
      await apiClient.post(
        `/my/helpdesk/actions/${ticketId}/${
          confirm ? 'confirm' : 'reopen'
        }`,
        confirm ? {} : { reason },
      )

      navigate('/my-support?workspace=helpdesk')
    } catch (ex) {
      setError(
        axios.isAxiosError(ex) &&
          typeof ex.response?.data?.message === 'string'
          ? ex.response.data.message
          : 'No se pudo completar la acción.',
      )
    } finally {
      setSaving(false)
    }
  }

  if (!['resolved', 'closed'].includes(status)) return null

  return (
    <section
      className="titan-page helpdesk-page"
      aria-label="Confirmar solución o reabrir"
    >
      <h2>¿La solución resolvió tu solicitud?</h2>

      {error && <p role="alert">{error}</p>}

      {status === 'resolved' && (
        <button
          type="button"
          className="helpdesk-ui-button helpdesk-ui-button--primary"
          disabled={saving}
          onClick={() => void act(true)}
        >
          Sí, confirmar solución
        </button>
      )}

      <p>
        Si el problema continúa, explica el motivo para
        reabrirlo dentro del plazo permitido.
      </p>

      <label htmlFor="requester-reopen-reason">
        Motivo de reapertura
      </label>

      <textarea
        id="requester-reopen-reason"
        rows={3}
        maxLength={2000}
        value={reason}
        disabled={saving}
        onChange={event => setReason(event.target.value)}
        style={{
          display: 'block',
          width: '100%',
          maxWidth: 800,
          margin: '12px 0',
        }}
      />

      <button
        type="button"
        className="helpdesk-ui-button"
        disabled={saving || reason.trim().length < 10}
        onClick={() => void act(false)}
      >
        {saving
          ? 'Procesando…'
          : 'El problema continúa: reabrir'}
      </button>
    </section>
  )
}