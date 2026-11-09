import { useState } from 'react'
import { LockKeyhole, X } from 'lucide-react'
import './LockDeviceModal.css'

export interface LockDeviceForm {
  pin: string
  message: string
  phone: string
}

interface Props {
  open: boolean
  busy?: boolean
  deviceName?: string
  includePhone?: boolean
  onCancel: () => void
  onConfirm: (form: LockDeviceForm) => void
}

/** El formulario se monta al abrir: el PIN no queda en memoria entre usos. */
export function LockDeviceModal({ open, ...props }: Props) {
  return open ? <LockDeviceDialog {...props} /> : null
}

function LockDeviceDialog({
  busy = false,
  deviceName,
  includePhone = false,
  onCancel,
  onConfirm,
}: Omit<Props, 'open'>) {
  const [pin, setPin] = useState('')
  const [message, setMessage] = useState('')
  const [phone, setPhone] = useState('')
  const [error, setError] = useState<string | null>(null)

  function submit() {
    const trimmedPin = pin.trim()
    if (trimmedPin && !/^\d{4,16}$/.test(trimmedPin)) {
      setError('El PIN temporal debe tener entre 4 y 16 dígitos.')
      return
    }

    setError(null)
    onConfirm({
      pin: trimmedPin,
      message: message.trim(),
      phone: phone.trim(),
    })
  }

  return (
    <div className="lock-modal-backdrop" role="presentation">
      <div
        className="lock-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="lock-device-title"
      >
        <header>
          <div>
            <span>Bloqueo remoto</span>
            <h3 id="lock-device-title">
              Bloquear {deviceName?.trim() || 'el dispositivo'}
            </h3>
          </div>
          <button type="button" aria-label="Cerrar" onClick={onCancel} disabled={busy}>
            <X size={16} />
          </button>
        </header>

        <form
          className="lock-form"
          onSubmit={event => {
            event.preventDefault()
            submit()
          }}
        >
          <p>
            El PIN y el mensaje son opcionales. Con un PIN, solo quien lo
            conozca podrá desbloquear el equipo. El mensaje se muestra en la
            pantalla de bloqueo, incluso tras reiniciar, hasta que TI quite el
            bloqueo. El PIN no queda visible en el historial.
          </p>

          <label>
            <span>PIN temporal</span>
            <input
              type="password"
              inputMode="numeric"
              autoComplete="new-password"
              value={pin}
              disabled={busy}
              maxLength={16}
              placeholder="4 a 16 dígitos, opcional"
              onChange={event => setPin(event.target.value.replace(/\D/g, ''))}
            />
          </label>

          <label>
            <span>Mensaje en pantalla</span>
            <textarea
              value={message}
              disabled={busy}
              maxLength={240}
              rows={3}
              placeholder="Ej. Equipo reportado como extraviado. Contacte a TI."
              onChange={event => setMessage(event.target.value)}
            />
          </label>

          {includePhone && (
            <label>
              <span>Teléfono de recuperación</span>
              <input
                type="tel"
                value={phone}
                disabled={busy}
                maxLength={32}
                placeholder="Opcional"
                onChange={event => setPhone(event.target.value)}
              />
            </label>
          )}

          {error && (
            <p className="lock-form-error" role="alert">
              {error}
            </p>
          )}

          <footer>
            <button type="button" onClick={onCancel} disabled={busy}>
              Cancelar
            </button>
            <button type="submit" className="danger" disabled={busy}>
              <LockKeyhole size={14} />
              {busy ? 'Bloqueando...' : 'Bloquear ahora'}
            </button>
          </footer>
        </form>
      </div>
    </div>
  )
}
