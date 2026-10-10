
import { useMemo, useState } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Clock3,
  FilePlus2,
  MapPin,
  RefreshCw,
  Server,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react'

import { helpdeskApi } from '../../../api/helpdeskApi'
import type { DeviceHealthItem } from '../lib/dashboardApi'

import './PunchClockAttentionCenter.css'

type Props = {
  devices: DeviceHealthItem[]
  onViewAll: () => void
  onIncidentCreated?: () => void
}

type ClockInfo = DeviceHealthItem & {
  location?: string | null
  lastSync?: string | null
}

function formatSync(value?: string | null) {
  if (!value) return 'No disponible'
  const date = new Date(value)

  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString('es-DO', {
        dateStyle: 'short',
        timeStyle: 'short',
      })
}

export function PunchClockAttentionCenter({
  devices,
  onViewAll,
  onIncidentCreated,
}: Props) {
  const reducedMotion = useReducedMotion()

  const [incidentDevice, setIncidentDevice] =
    useState<ClockInfo | null>(null)

  const [incidentDescription, setIncidentDescription] =
    useState('')

  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const problemDevices = useMemo(
    () => devices.filter(device =>
      !device.online ||
      (device.latencyMs != null && device.latencyMs > 1200),
    ),
    [devices],
  )

  function openIncident(device: ClockInfo) {
    setIncidentDevice(device)
    setIncidentDescription(
      `Revisar conectividad del reloj biométrico "${device.name}".`,
    )
    setMessage('')
    setError('')
  }

  function closeIncident() {
    if (submitting) return
    setIncidentDevice(null)
    setError('')
  }

  async function createIncident() {
    if (!incidentDevice || submitting) return

    if (!incidentDescription.trim()) {
      setError('Escribe una descripción de la incidencia.')
      return
    }

    setSubmitting(true)
    setError('')

    try {
      const details = [
        incidentDescription.trim(),
        '',
        `Dispositivo: ${incidentDevice.name}`,
        `Estado: ${incidentDevice.online ? 'Online con latencia' : 'Offline'}`,
        `Latencia: ${
          incidentDevice.latencyMs == null
            ? 'No disponible'
            : `${incidentDevice.latencyMs} ms`
        }`,
        `Ponches hoy: ${incidentDevice.punchesToday}`,
      ].join('\n')

      await helpdeskApi.createTicket({
        subject: `Incidencia biométrica: ${incidentDevice.name}`,
        description: details,
        type: 'Incident',
        priority: incidentDevice.online ? 'Medium' : 'High',
        category: 'Biometría',
        source: 'Web',
      })

      setMessage(
        `Incidencia registrada para ${incidentDevice.name}.`,
      )
      setIncidentDevice(null)
      onIncidentCreated?.()
    } catch {
      setError(
        'No se pudo registrar la incidencia. Verifica tus permisos de Mesa de ayuda y vuelve a intentarlo.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="pclock-center">
      <header className="pclock-center-header">
        <div className="pclock-heading">
          <div className="pclock-heading-icon">
            <AlertTriangle size={21} />
          </div>
          <div>
            <span className="pclock-eyebrow">
              MONITOREO DE INFRAESTRUCTURA
            </span>
            <h2>Relojes que requieren atención</h2>
            <p>
              Incidencias de conexión y rendimiento de relojes.
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={onViewAll}
          className="pclock-view-all"
        >
          Ver todos <ArrowRight size={16} />
        </button>
      </header>

      {message && (
        <div className="pclock-success" role="status">
          <CheckCircle2 size={17} />
          {message}
        </div>
      )}

      {problemDevices.length === 0 ? (
        <div className="pclock-all-clear">
          <CheckCircle2 size={35} />
          <strong>Sin incidencias de conectividad</strong>
          <p>
            Los relojes reportados no presentan alertas.
          </p>
        </div>
      ) : (
        <div className="pclock-grid">
          {problemDevices.slice(0, 9).map((device, index) => {
            const info = device as ClockInfo
            const isSlow = info.online

            return (
              <motion.article
                key={`${info.name}-${index}`}
                className="pclock-card"
                initial={
                  reducedMotion
                    ? false
                    : { opacity: 0, y: 10 }
                }
                animate={{ opacity: 1, y: 0 }}
                transition={{
                  duration: reducedMotion ? 0 : 0.24,
                  delay: reducedMotion ? 0 : Math.min(index, 5) * 0.035,
                }}
              >
                <div className="pclock-card-top">
                  <span className="pclock-device-icon">
                    {isSlow
                      ? <Wifi size={19} />
                      : <WifiOff size={19} />}
                  </span>

                  <span
                    className={
                      `pclock-device-status ${
                        isSlow ? 'slow' : 'offline'
                      }`
                    }
                  >
                    {isSlow ? 'Alta latencia' : 'Offline'}
                  </span>
                </div>

                <h3 title={info.name}>{info.name}</h3>

                <div className="pclock-card-details">
                  <div>
                    <MapPin size={14} />
                    <span>
                      {info.location || 'Ubicación no registrada'}
                    </span>
                  </div>

                  <div>
                    <Clock3 size={14} />
                    <span>
                      Última sincronización: {formatSync(info.lastSync)}
                    </span>
                  </div>

                  <div>
                    <Server size={14} />
                    <span>
                      Latencia: {
                        info.latencyMs == null
                          ? 'No disponible'
                          : `${info.latencyMs} ms`
                      }
                    </span>
                  </div>

                  <div>
                    <CheckCircle2 size={14} />
                    <span>
                      {info.punchesToday} ponches hoy
                    </span>
                  </div>
                </div>

                <div className="pclock-card-actions">
                  <button
                    type="button"
                    className="pclock-restart"
                    disabled
                    title="Pendiente: comando seguro de reinicio ZKTeco y autorización del servidor"
                  >
                    <RefreshCw size={14} />
                    Reiniciar
                  </button>

                  <button
                    type="button"
                    className="pclock-incident"
                    onClick={() => openIncident(info)}
                  >
                    <FilePlus2 size={14} />
                    Registrar incidencia
                  </button>
                </div>
              </motion.article>
            )
          })}
        </div>
      )}

      {incidentDevice && (
        <div
          className="pclock-dialog-backdrop"
          role="presentation"
          onMouseDown={event => {
            if (event.target === event.currentTarget) {
              closeIncident()
            }
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="pclock-incident-title"
            className="pclock-dialog"
          >
            <div className="pclock-dialog-header">
              <div>
                <span className="pclock-eyebrow">
                  MESA DE AYUDA / BIOMETRÍA
                </span>
                <h3 id="pclock-incident-title">
                  Registrar incidencia
                </h3>
              </div>

              <button
                type="button"
                aria-label="Cerrar"
                onClick={closeIncident}
                disabled={submitting}
              >
                <X size={19} />
              </button>
            </div>

            <p className="pclock-dialog-device">
              {incidentDevice.name}
            </p>

            <label
              htmlFor="pclock-incident-description"
              className="pclock-dialog-label"
            >
              Descripción del problema
            </label>

            <textarea
              id="pclock-incident-description"
              rows={5}
              value={incidentDescription}
              onChange={event =>
                setIncidentDescription(event.target.value)}
              maxLength={2000}
            />

            {error && (
              <p className="pclock-dialog-error" role="alert">
                {error}
              </p>
            )}

            <div className="pclock-dialog-actions">
              <button
                type="button"
                onClick={closeIncident}
                disabled={submitting}
              >
                Cancelar
              </button>

              <button
                type="button"
                className="pclock-dialog-submit"
                onClick={() => void createIncident()}
                disabled={submitting}
              >
                {submitting ? 'Registrando...' : 'Crear ticket'}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
