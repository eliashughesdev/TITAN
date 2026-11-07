/*
 * QR de aprovisionamiento de fábrica (Android Enterprise, pantalla de
 * bienvenida: tocar 6 veces). Android descarga el APK del agente, lo deja
 * como Device Owner y le entrega ADMIN_EXTRAS_BUNDLE; con server_url y
 * enrollment_token el agente se inscribe solo (ProvisioningConfig.kt) y
 * el servidor le asigna la política de la credencial.
 */
import type { AndroidProvisioningInfo } from '../../../types/enrollment'

export type WifiSecurityType = 'WPA' | 'WEP' | 'NONE'

export interface FactoryProvisioningInput {
  info: Pick<AndroidProvisioningInfo, 'componentName' | 'downloadUrl' | 'signatureChecksum'>
  serverUrl: string
  enrollmentToken: string
  wifiSsid?: string
  wifiPassword?: string
  wifiSecurityType?: WifiSecurityType
  wifiHidden?: boolean
  locale?: string
  timeZone?: string
}

const PREFIX = 'android.app.extra.PROVISIONING_'

/** Solo ASCII imprimible: el asistente de Android rechaza el QR con otros caracteres. */
const PRINTABLE_ASCII = /^[\x20-\x7E]*$/

export function factoryProvisioningErrors(input: FactoryProvisioningInput): string[] {
  const errors: string[] = []
  if (!input.info.signatureChecksum) errors.push('Falta el checksum de firma del APK del agente.')
  if (!/^https:\/\//.test(input.info.downloadUrl)) errors.push('La URL del APK debe ser https.')
  if (!input.serverUrl) errors.push('Indique la URL del servidor que verá el dispositivo.')
  if (!input.enrollmentToken) errors.push('Genere una credencial para incluir el token.')

  const ssid = input.wifiSsid?.trim() ?? ''
  const security = input.wifiSecurityType ?? 'WPA'
  if (ssid && security !== 'NONE' && !input.wifiPassword) {
    errors.push('La red Wi-Fi requiere contraseña.')
  }
  for (const [label, value] of [
    ['SSID', ssid],
    ['Contraseña Wi-Fi', input.wifiPassword ?? ''],
    ['Servidor', input.serverUrl],
  ] as const) {
    if (!PRINTABLE_ASCII.test(value)) errors.push(`${label} contiene caracteres no admitidos (use solo ASCII).`)
  }
  return errors
}

/** JSON de una sola línea, en el orden y con las claves que exige Android. */
export function buildFactoryProvisioningPayload(input: FactoryProvisioningInput): string {
  const payload: Record<string, unknown> = {
    [`${PREFIX}DEVICE_ADMIN_COMPONENT_NAME`]: input.info.componentName,
    [`${PREFIX}DEVICE_ADMIN_PACKAGE_DOWNLOAD_LOCATION`]: input.info.downloadUrl,
    [`${PREFIX}DEVICE_ADMIN_SIGNATURE_CHECKSUM`]: input.info.signatureChecksum,
    [`${PREFIX}LEAVE_ALL_SYSTEM_APPS_ENABLED`]: true,
  }

  if (input.locale) payload[`${PREFIX}LOCALE`] = input.locale
  if (input.timeZone) payload[`${PREFIX}TIME_ZONE`] = input.timeZone

  const ssid = input.wifiSsid?.trim()
  if (ssid) {
    const security = input.wifiSecurityType ?? 'WPA'
    payload[`${PREFIX}WIFI_SSID`] = ssid
    payload[`${PREFIX}WIFI_SECURITY_TYPE`] = security
    if (security !== 'NONE' && input.wifiPassword) payload[`${PREFIX}WIFI_PASSWORD`] = input.wifiPassword
    if (input.wifiHidden) payload[`${PREFIX}WIFI_HIDDEN`] = true
  }

  payload[`${PREFIX}ADMIN_EXTRAS_BUNDLE`] = {
    server_url: input.serverUrl.replace(/\/+$/, ''),
    enrollment_token: input.enrollmentToken.trim(),
  }

  return JSON.stringify(payload)
}
