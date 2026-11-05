import { useEffect, useState } from 'react'
import * as QRCode from 'qrcode'

import {
  buildAgentEnrollmentPayload,
  isLoopbackServer,
  normalizeServerUrl,
} from '../utils/agentEnrollmentQr'

const SERVER_STORAGE_KEY = 'titanmdm.enrollment.agentServerUrl'

function defaultServer() {
  return window.localStorage.getItem(SERVER_STORAGE_KEY) ?? window.location.origin
}

/**
 * QR de inscripcion interna: servidor + token. El handheld lo lee con el
 * lector Honeywell o la camara desde el TitanMDM Android Agent.
 */
export function AndroidAgentEnrollmentQr({ token }: { token: string }) {
  const [server, setServer] = useState(defaultServer)
  const [qr, setQr] = useState<{ dataUrl: string; payload: string } | null>(null)
  const normalized = normalizeServerUrl(server)

  useEffect(() => {
    if (!normalized) return
    let active = true
    const payload = buildAgentEnrollmentPayload(normalized, token)
    QRCode.toDataURL(payload, { width: 300, margin: 2, errorCorrectionLevel: 'M' })
      .then(dataUrl => {
        if (active) setQr({ dataUrl, payload })
      })
      .catch(() => {
        if (active) setQr(null)
      })
    return () => {
      active = false
    }
  }, [normalized, token])

  const current = normalized && qr?.payload === buildAgentEnrollmentPayload(normalized, token) ? qr : null

  return (
    <div className="enrollment-agent-qr">
      <label className="enrollment-agent-qr__server">
        <span>URL del servidor que verá el dispositivo</span>
        <input
          value={server}
          onChange={event => {
            setServer(event.target.value)
            window.localStorage.setItem(SERVER_STORAGE_KEY, event.target.value)
          }}
          placeholder="http://192.168.1.20:8020"
          spellCheck={false}
        />
      </label>

      {!normalized && <p className="enrollment-agent-qr__warn">Escriba una URL http(s) válida.</p>}
      {normalized && isLoopbackServer(normalized) && (
        <p className="enrollment-agent-qr__warn">
          “localhost” no es alcanzable desde el handheld: use la IP LAN o el nombre DNS de este servidor.
        </p>
      )}

      {current && (
        <figure className="enrollment-agent-qr__code">
          <img src={current.dataUrl} alt="QR de inscripción TitanMDM Android Agent" width={300} height={300} />
          <figcaption>
            Abra TitanMDM Agent en el dispositivo y escanee este código (gatillo Honeywell o “Escanear QR con la cámara”).
          </figcaption>
        </figure>
      )}
    </div>
  )
}
