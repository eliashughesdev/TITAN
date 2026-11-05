/*
 * Barra de soporte remoto de nivel comercial (estilo AnyDesk / RDP):
 * calidad, resolucion y FPS del stream, Ctrl+Alt+Supr real (SendSAS por
 * el servicio SYSTEM), atajos de teclado, transferencia de archivos con
 * progreso y RTT medido en ms.
 */
import { useCallback, useEffect, useRef, useState } from 'react'
import { Gauge, Keyboard, Send, ShieldAlert, Upload, X } from 'lucide-react'

import type {
  RemoteStreamPreset,
  RemoteStreamProfile,
  RemoteSupportSignalRClient,
} from '../../api/remoteSupportSignalR'
import './RemoteEnterpriseToolbar.css'

const PRESETS: Array<{ value: RemoteStreamPreset; label: string; hint: string }> = [
  { value: 'auto', label: 'Automático', hint: 'Se adapta a la red' },
  { value: 'speed', label: 'Velocidad', hint: 'Menor latencia' },
  { value: 'balanced', label: 'Equilibrado', hint: 'Uso general' },
  { value: 'quality', label: 'Calidad', hint: 'Texto nítido' },
]

const RESOLUTIONS = [
  { value: 0, label: 'Resolución auto' },
  { value: 1280, label: '1280 px' },
  { value: 1600, label: '1600 px' },
  { value: 1920, label: '1920 px (Full HD)' },
  { value: 3840, label: 'Nativa (hasta 4K)' },
]

const FPS = [
  { value: 0, label: 'FPS auto' },
  { value: 15, label: '15 FPS' },
  { value: 30, label: '30 FPS' },
  { value: 60, label: '60 FPS' },
]

const VK = { shift: 0x10, ctrl: 0x11, alt: 0x12, tab: 0x09, esc: 0x1b, win: 0x5b, r: 0x52, d: 0x44, e: 0x45 } as const

/* Combinaciones que Windows SI acepta por SendInput (no incluye SAS ni Win+L). */
export const KEY_COMBOS: Array<{ id: string; label: string; keys: number[] }> = [
  { id: 'win', label: 'Inicio (Win)', keys: [VK.win] },
  { id: 'alt-tab', label: 'Cambiar ventana (Alt+Tab)', keys: [VK.alt, VK.tab] },
  { id: 'task-manager', label: 'Administrador de tareas (Ctrl+Shift+Esc)', keys: [VK.ctrl, VK.shift, VK.esc] },
  { id: 'run', label: 'Ejecutar (Win+R)', keys: [VK.win, VK.r] },
  { id: 'desktop', label: 'Mostrar escritorio (Win+D)', keys: [VK.win, VK.d] },
  { id: 'explorer', label: 'Explorador (Win+E)', keys: [VK.win, VK.e] },
]

const RTT_INTERVAL_MS = 2000

export function rttLevel(rtt: number | null): 'good' | 'fair' | 'poor' | 'unknown' {
  if (rtt === null) return 'unknown'
  if (rtt < 80) return 'good'
  if (rtt < 200) return 'fair'
  return 'poor'
}

const formatBytes = (bytes: number) =>
  bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`

interface Props {
  client: RemoteSupportSignalRClient | null
  sessionId: string
  connected: boolean
  ownsControl: boolean
  allowKeyboard: boolean
  allowFileTransfer: boolean
  onError: (message: string) => void
  onNotice?: (message: string) => void
}

export function RemoteEnterpriseToolbar({
  client,
  sessionId,
  connected,
  ownsControl,
  allowKeyboard,
  allowFileTransfer,
  onError,
  onNotice,
}: Props) {
  const [profile, setProfile] = useState<RemoteStreamProfile>({ preset: 'auto', maxWidth: 0, maxFps: 0 })
  const [rtt, setRtt] = useState<number | null>(null)
  const [busy, setBusy] = useState<'profile' | 'sas' | 'keys' | null>(null)
  const [transfer, setTransfer] = useState<{ name: string; sent: number; total: number } | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const abortRef = useRef<AbortController | null>(null)

  const canControl = connected && ownsControl
  const canType = canControl && allowKeyboard

  // RTT continuo mientras la sesion esta conectada.
  useEffect(() => {
    if (!client || !connected) {
      setRtt(null)
      return
    }

    let active = true

    const measure = async () => {
      try {
        const value = await client.ping()
        if (active) setRtt(value)
      } catch {
        if (active) setRtt(null)
      }
    }

    void measure()
    const timer = window.setInterval(() => void measure(), RTT_INTERVAL_MS)

    return () => {
      active = false
      window.clearInterval(timer)
    }
  }, [client, connected])

  // Cancelar una transferencia en curso al salir de la sesion.
  useEffect(() => () => abortRef.current?.abort(), [sessionId])

  const applyProfile = useCallback(
    async (next: RemoteStreamProfile) => {
      if (!client) return
      const previous = profile
      setProfile(next)
      setBusy('profile')
      try {
        await client.setStreamProfile(sessionId, next)
      } catch (error) {
        setProfile(previous)
        onError(error instanceof Error ? error.message : 'No se pudo cambiar el perfil de transmisión.')
      } finally {
        setBusy(null)
      }
    },
    [client, onError, profile, sessionId],
  )

  const sendSas = async () => {
    if (!client) return
    setBusy('sas')
    try {
      await client.sendSecureAttention(sessionId)
      onNotice?.('Ctrl+Alt+Supr enviado al servicio del equipo.')
    } catch (error) {
      onError(error instanceof Error ? error.message : 'No se pudo enviar Ctrl+Alt+Supr.')
    } finally {
      setBusy(null)
    }
  }

  const sendCombo = async (keys: number[]) => {
    if (!client) return
    setBusy('keys')
    try {
      for (const key of keys) await client.keyboard(sessionId, key, true)
      for (const key of [...keys].reverse()) await client.keyboard(sessionId, key, false)
    } catch (error) {
      onError(error instanceof Error ? error.message : 'No se pudo enviar la combinación.')
    } finally {
      setBusy(null)
    }
  }

  const sendFile = async (file: File) => {
    if (!client) return
    const controller = new AbortController()
    abortRef.current = controller
    setTransfer({ name: file.name, sent: 0, total: file.size })

    try {
      const result = await client.sendFile(
        sessionId,
        file,
        progress => setTransfer({ name: progress.fileName, sent: progress.sentBytes, total: progress.totalBytes }),
        controller.signal,
      )
      onNotice?.(`Archivo entregado: ${result.savedAs} (Descargas\\TitanMDM del usuario).`)
    } catch (error) {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        onError(error instanceof Error ? error.message : 'No se pudo transferir el archivo.')
      }
    } finally {
      abortRef.current = null
      setTransfer(null)
    }
  }

  const level = rttLevel(rtt)
  const percent = transfer && transfer.total > 0 ? Math.round((transfer.sent / transfer.total) * 100) : 0

  return (
    <div className="wr-enterprise" role="toolbar" aria-label="Herramientas de soporte remoto">
      <div className="wr-enterprise__group">
        <Gauge size={15} aria-hidden="true" />
        <select
          aria-label="Perfil de calidad"
          value={profile.preset}
          disabled={!canControl || busy === 'profile'}
          onChange={event => void applyProfile({ ...profile, preset: event.target.value as RemoteStreamPreset })}
        >
          {PRESETS.map(preset => (
            <option key={preset.value} value={preset.value} title={preset.hint}>{preset.label}</option>
          ))}
        </select>
        <select
          aria-label="Resolución máxima"
          value={profile.maxWidth}
          disabled={!canControl || busy === 'profile'}
          onChange={event => void applyProfile({ ...profile, maxWidth: Number(event.target.value) })}
        >
          {RESOLUTIONS.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
        <select
          aria-label="FPS máximos"
          value={profile.maxFps}
          disabled={!canControl || busy === 'profile'}
          onChange={event => void applyProfile({ ...profile, maxFps: Number(event.target.value) })}
        >
          {FPS.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
      </div>

      <div className="wr-enterprise__group">
        <button
          type="button"
          className="wr-enterprise__sas"
          disabled={!canType || busy !== null}
          onClick={() => void sendSas()}
          title="Secure Attention Sequence real, ejecutada por el servicio SYSTEM del agente"
        >
          <ShieldAlert size={15} />
          Ctrl+Alt+Supr
        </button>

        <label className="wr-enterprise__keys">
          <Keyboard size={15} aria-hidden="true" />
          <select
            aria-label="Enviar combinación de teclas"
            value=""
            disabled={!canType || busy !== null}
            onChange={event => {
              const combo = KEY_COMBOS.find(item => item.id === event.target.value)
              if (combo) void sendCombo(combo.keys)
            }}
          >
            <option value="">Atajos…</option>
            {KEY_COMBOS.map(combo => <option key={combo.id} value={combo.id}>{combo.label}</option>)}
          </select>
        </label>
      </div>

      <div className="wr-enterprise__group">
        <input
          ref={fileInputRef}
          type="file"
          hidden
          onChange={event => {
            const file = event.target.files?.[0]
            event.target.value = ''
            if (file) void sendFile(file)
          }}
        />
        {transfer ? (
          <div className="wr-enterprise__transfer" aria-live="polite">
            <Send size={14} aria-hidden="true" />
            <span className="wr-enterprise__transfer-name" title={transfer.name}>{transfer.name}</span>
            <progress max={100} value={percent} aria-label="Progreso de transferencia" />
            <span>{percent}% · {formatBytes(transfer.sent)} / {formatBytes(transfer.total)}</span>
            <button type="button" aria-label="Cancelar transferencia" onClick={() => abortRef.current?.abort()}>
              <X size={14} />
            </button>
          </div>
        ) : (
          <button
            type="button"
            disabled={!canControl || !allowFileTransfer}
            title={allowFileTransfer ? 'Enviar archivo al equipo (máx. 200 MB)' : 'La sesión no permite transferencia de archivos'}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload size={15} />
            Enviar archivo
          </button>
        )}
      </div>

      <span
        className={`wr-enterprise__rtt wr-enterprise__rtt--${level}`}
        title="Ida y vuelta técnico ↔ servidor TitanMDM"
        aria-label={rtt === null ? 'Latencia no disponible' : `Latencia ${rtt} milisegundos`}
      >
        <span className="wr-enterprise__rtt-dot" aria-hidden="true" />
        {rtt === null ? '— ms' : `${rtt} ms`}
      </span>
    </div>
  )
}
