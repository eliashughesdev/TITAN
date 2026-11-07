import { useEffect, useMemo, useState } from 'react'
import * as QRCode from 'qrcode'

import { enrollmentApi } from '../../../api/enrollmentApi'
import type { AndroidProvisioningInfo } from '../../../types/enrollment'
import { isLoopbackServer, normalizeServerUrl } from '../utils/agentEnrollmentQr'
import { extractRequestError } from '../utils/enrollmentFormatters'
import {
  buildFactoryProvisioningPayload,
  factoryProvisioningErrors,
  type WifiSecurityType,
} from '../utils/factoryProvisioningQr'

// Misma clave que el QR del agente: el servidor se escribe una sola vez.
const SERVER_STORAGE_KEY = 'titanmdm.enrollment.agentServerUrl'
const SSID_STORAGE_KEY = 'titanmdm.enrollment.factoryWifiSsid'

const browserTimeZone = () => {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
}

/**
 * QR para handheld de fábrica: en la pantalla de bienvenida se toca 6
 * veces, se escanea y el equipo queda administrado, inscrito y con la
 * política de la credencial sin más intervención.
 * La contraseña Wi-Fi solo vive en este formulario: no se envía al servidor.
 */
export function AndroidFactoryProvisioningQr({ token, policyName }: { token: string; policyName?: string | null }) {
  const [info, setInfo] = useState<AndroidProvisioningInfo | null>(null)
  const [infoError, setInfoError] = useState<string | null>(null)
  const [server, setServer] = useState(() => window.localStorage.getItem(SERVER_STORAGE_KEY) ?? window.location.origin)
  const [ssid, setSsid] = useState(() => window.localStorage.getItem(SSID_STORAGE_KEY) ?? '')
  const [password, setPassword] = useState('')
  const [security, setSecurity] = useState<WifiSecurityType>('WPA')
  const [hidden, setHidden] = useState(false)
  const [qr, setQr] = useState<{ payload: string; dataUrl: string } | null>(null)

  useEffect(() => {
    let active = true
    enrollmentApi.getAndroidProvisioning()
      .then(result => {
        if (active) setInfo(result)
      })
      .catch(error => {
        if (active) setInfoError(extractRequestError(error, 'No se pudieron obtener el APK y el checksum del agente.'))
      })
    return () => {
      active = false
    }
  }, [])

  const normalizedServer = normalizeServerUrl(server)

  const input = useMemo(
    () => info && {
      info,
      serverUrl: normalizedServer ?? '',
      enrollmentToken: token,
      wifiSsid: ssid,
      wifiPassword: password,
      wifiSecurityType: security,
      wifiHidden: hidden,
      locale: 'es_DO',
      timeZone: browserTimeZone(),
    },
    [info, normalizedServer, token, ssid, password, security, hidden],
  )

  const errors = useMemo(() => (input ? factoryProvisioningErrors(input) : []), [input])
  const payload = input && errors.length === 0 ? buildFactoryProvisioningPayload(input) : null

  useEffect(() => {
    if (!payload) return
    let active = true
    QRCode.toDataURL(payload, { width: 420, margin: 4, errorCorrectionLevel: 'M' })
      .then(dataUrl => {
        if (active) setQr({ payload, dataUrl })
      })
      .catch(() => {
        if (active) setQr(null)
      })
    return () => {
      active = false
    }
  }, [payload])

  const current = payload && qr?.payload === payload ? qr : null

  return (
    <div className="enrollment-agent-qr enrollment-factory-qr">
      <h3>QR de fábrica (tocar 6 veces la pantalla de bienvenida)</h3>
      <p className="enrollment-factory-qr__hint">
        Para equipos restablecidos de fábrica: Android descarga TitanMDM Agent, lo deja como
        administrador del equipo, se conecta al Wi-Fi y el agente se inscribe solo
        {policyName ? <> y aplica la política <strong>{policyName}</strong></> : null}.
      </p>

      {infoError && <p className="enrollment-agent-qr__warn">{infoError}</p>}

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
      {normalizedServer && isLoopbackServer(normalizedServer) && (
        <p className="enrollment-agent-qr__warn">
          “localhost” no es alcanzable desde el handheld: use la IP LAN o el nombre DNS de este servidor.
        </p>
      )}

      <div className="enrollment-factory-qr__wifi">
        <label className="enrollment-agent-qr__server">
          <span>Red Wi-Fi (SSID)</span>
          <input
            value={ssid}
            onChange={event => {
              setSsid(event.target.value)
              window.localStorage.setItem(SSID_STORAGE_KEY, event.target.value)
            }}
            placeholder="Red del almacén"
            spellCheck={false}
          />
        </label>
        <label className="enrollment-agent-qr__server">
          <span>Seguridad</span>
          <select value={security} onChange={event => setSecurity(event.target.value as WifiSecurityType)}>
            <option value="WPA">WPA/WPA2</option>
            <option value="WEP">WEP</option>
            <option value="NONE">Abierta</option>
          </select>
        </label>
        {security !== 'NONE' && (
          <label className="enrollment-agent-qr__server">
            <span>Contraseña Wi-Fi (no se guarda)</span>
            <input
              type="password"
              value={password}
              onChange={event => setPassword(event.target.value)}
              autoComplete="off"
            />
          </label>
        )}
        <label className="enrollment-factory-qr__check">
          <input type="checkbox" checked={hidden} onChange={event => setHidden(event.target.checked)} />
          <span>Red oculta</span>
        </label>
      </div>

      {info && errors.length > 0 && (
        <ul className="enrollment-agent-qr__warn">
          {errors.map(message => <li key={message}>{message}</li>)}
        </ul>
      )}

      {current && (
        <figure className="enrollment-agent-qr__code">
          <img src={current.dataUrl} alt="QR de aprovisionamiento de fábrica TitanMDM" width={420} height={420} />
          <figcaption>
            Equipo de fábrica → pantalla de bienvenida → tocar 6 veces → escanear.
            {info?.versionName ? ` Agente ${info.versionName}.` : ''}
          </figcaption>
          <a className="enrollment-copy-button" href={current.dataUrl} download="titanmdm-qr-fabrica.png">
            Descargar PNG
          </a>
        </figure>
      )}
    </div>
  )
}
