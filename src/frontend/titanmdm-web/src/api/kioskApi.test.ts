import { describe, expect, it } from 'vitest'
import { buildAndroidKioskPolicy, isKioskPolicy } from './kioskApi'

describe('buildAndroidKioskPolicy', () => {
  it('builds the canonical root.android compiler contract', () => {
    const policy = buildAndroidKioskPolicy({
      application: {
        packageName: ' com.example.pos ',
        displayName: ' Point of sale ',
      },
      disableStatusBar: true,
      disableKeyguard: true,
      deviceRestrictions: {
        factoryResetDisabled: true,
        safeBootDisabled: true,
        screenCaptureDisabled: true,
        usbFileTransferDisabled: true,
        bluetoothDisabled: false,
        cameraDisabled: false,
      },
    })

    expect(policy.schemaVersion).toBe(2)
    expect(policy.titanProfileType).toBe('kiosk')
    expect(policy.android.kiosk).toEqual({
      enabled: true,
      applicationId: 'com.example.pos',
      disableStatusBar: true,
      disableKeyguard: true,
    })
    expect(policy.android.applications.packages[0]).toEqual({
      packageName: 'com.example.pos',
      label: 'Point of sale',
      installType: 'KIOSK',
    })
    expect(isKioskPolicy(JSON.stringify(policy))).toBe(true)
  })
})
