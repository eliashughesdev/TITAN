import { describe, expect, it } from 'vitest'

import {
  buildFactoryProvisioningPayload,
  factoryProvisioningErrors,
  type FactoryProvisioningInput,
} from './factoryProvisioningQr'

const input: FactoryProvisioningInput = {
  info: {
    componentName: 'com.titanmdm.agent/com.titanmdm.agent.admin.TitanDeviceAdminReceiver',
    downloadUrl: 'https://raw.githubusercontent.com/eliashughesdev/TITAN/main/deploy/app-debug.apk',
    signatureChecksum: 'gl3pIraiyGi6XKNtLaHjZDcoIBPPgDLWorUdzmRfEcA',
  },
  serverUrl: 'http://172.21.20.14:8020/',
  enrollmentToken: ' a1b2c3d4e5f6a7b8c9d0 ',
  wifiSsid: ' Planta ',
  wifiPassword: 'clave-segura',
  wifiSecurityType: 'WPA',
  locale: 'es_DO',
  timeZone: 'America/Santo_Domingo',
}

describe('QR de aprovisionamiento de fábrica', () => {
  it('genera JSON de una línea con las claves de Android Enterprise y los extras del agente', () => {
    const text = buildFactoryProvisioningPayload(input)
    const json = JSON.parse(text)

    expect(text).not.toMatch(/\n/)
    expect(json['android.app.extra.PROVISIONING_DEVICE_ADMIN_COMPONENT_NAME']).toBe(input.info.componentName)
    expect(json['android.app.extra.PROVISIONING_DEVICE_ADMIN_PACKAGE_DOWNLOAD_LOCATION']).toBe(input.info.downloadUrl)
    expect(json['android.app.extra.PROVISIONING_DEVICE_ADMIN_SIGNATURE_CHECKSUM']).toBe(input.info.signatureChecksum)
    expect(json['android.app.extra.PROVISIONING_LEAVE_ALL_SYSTEM_APPS_ENABLED']).toBe(true)
    expect(json['android.app.extra.PROVISIONING_WIFI_SSID']).toBe('Planta')
    expect(json['android.app.extra.PROVISIONING_WIFI_PASSWORD']).toBe('clave-segura')
    expect(json['android.app.extra.PROVISIONING_TIME_ZONE']).toBe('America/Santo_Domingo')
    expect(json['android.app.extra.PROVISIONING_ADMIN_EXTRAS_BUNDLE']).toEqual({
      server_url: 'http://172.21.20.14:8020',
      enrollment_token: 'a1b2c3d4e5f6a7b8c9d0',
    })
    expect(Object.keys(json).some(key => key.endsWith('_URL'))).toBe(false)
  })

  it('omite Wi-Fi si no hay SSID y la contraseña en redes abiertas', () => {
    const noWifi = JSON.parse(buildFactoryProvisioningPayload({ ...input, wifiSsid: '' }))
    expect(noWifi['android.app.extra.PROVISIONING_WIFI_SSID']).toBeUndefined()
    expect(noWifi['android.app.extra.PROVISIONING_WIFI_PASSWORD']).toBeUndefined()

    const open = JSON.parse(buildFactoryProvisioningPayload({ ...input, wifiSecurityType: 'NONE' }))
    expect(open['android.app.extra.PROVISIONING_WIFI_SECURITY_TYPE']).toBe('NONE')
    expect(open['android.app.extra.PROVISIONING_WIFI_PASSWORD']).toBeUndefined()
  })

  it('valida los datos antes de generar el QR', () => {
    expect(factoryProvisioningErrors(input)).toEqual([])
    expect(factoryProvisioningErrors({ ...input, wifiPassword: '' })).toContain('La red Wi-Fi requiere contraseña.')
    expect(factoryProvisioningErrors({ ...input, enrollmentToken: '' })).toContain('Genere una credencial para incluir el token.')
    expect(factoryProvisioningErrors({ ...input, wifiSsid: 'Café' }).join(' ')).toMatch(/ASCII/)
    expect(factoryProvisioningErrors({ ...input, info: { ...input.info, downloadUrl: 'http://x/app.apk' } }))
      .toContain('La URL del APK debe ser https.')
  })
})
