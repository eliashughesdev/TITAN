import { describe, expect, it } from 'vitest'
import {
  buildLauncherConfiguration,
  contrastRatio,
  createLauncherDraft,
  hasReadableContrast,
  isValidBrandingColor,
  isValidLauncherResourceUrl,
  moveLauncherApplication,
  normalizeBrandingColor,
  parseLauncherConfiguration,
} from './launcherModel'

describe('Titan Launcher configuration', () => {
  it('builds schema v1 launcher data under root.android', () => {
    const configuration = buildLauncherConfiguration({
      ...createLauncherDraft(),
      companyName: 'Acme',
      applications: [
        {
          packageName: 'com.acme.pos',
          label: 'POS',
          installType: 'FORCE_INSTALLED',
          visible: true,
        },
        {
          packageName: 'com.acme.admin',
          label: 'Admin',
          installType: 'AVAILABLE',
          visible: false,
        },
      ],
    })

    expect(configuration.titanProfileType).toBe('titan-launcher')
    expect(configuration.android.launcher.schemaVersion).toBe(2)
    expect(configuration.android.kiosk.enabled).toBe(false)
    expect(configuration.android.applications.packages.map((app) => app.installType))
      .toEqual(['FORCE_INSTALLED', 'BLOCKED'])
    expect(parseLauncherConfiguration(JSON.stringify(configuration))?.companyName)
      .toBe('Acme')
  })

  it('preserves application ordering when moving rows', () => {
    const apps = ['one', 'two', 'three'].map((packageName) => ({
      packageName,
      label: packageName,
      installType: 'AVAILABLE' as const,
      visible: true,
    }))

    expect(moveLauncherApplication(apps, 1, -1).map((app) => app.packageName))
      .toEqual(['two', 'one', 'three'])
    expect(moveLauncherApplication(apps, 0, -1)).toBe(apps)
  })

  it('round-trips validated branding colors', () => {
    const configuration = buildLauncherConfiguration({
      ...createLauncherDraft(),
      primaryColor: '#16a36a',
      backgroundColor: '#0b1720',
    })

    expect(configuration.android.launcher.primaryColor).toBe('#16A36A')
    expect(configuration.android.launcher.backgroundColor).toBe('#0B1720')

    const parsed = parseLauncherConfiguration(JSON.stringify(configuration))
    expect(parsed?.primaryColor).toBe('#16A36A')
    expect(parsed?.backgroundColor).toBe('#0B1720')
  })

  it('falls back to defaults for invalid branding colors', () => {
    expect(isValidBrandingColor('#16A36A')).toBe(true)
    expect(isValidBrandingColor('#16a36')).toBe(false)
    expect(isValidBrandingColor('16A36A')).toBe(false)
    expect(isValidBrandingColor('red')).toBe(false)

    const defaults = createLauncherDraft()
    expect(normalizeBrandingColor('not-a-color', defaults.primaryColor))
      .toBe(defaults.primaryColor)

    const configuration = buildLauncherConfiguration(defaults)
    const tampered = JSON.stringify(configuration).replace('#16A36A', 'red')
    expect(parseLauncherConfiguration(tampered)?.primaryColor)
      .toBe(defaults.primaryColor)
  })

  it('reads previous profiles that omit the new branding fields', () => {
    const legacy = JSON.stringify({
      android: {
        launcher: {
          enabled: true,
          schemaVersion: 1,
          companyName: 'Legado',
        },
        applications: { packages: [] },
      },
    })

    const parsed = parseLauncherConfiguration(legacy)
    expect(parsed?.companyName).toBe('Legado')
    expect(parsed?.wallpaperUrl).toBe('')
    expect(parsed?.gridColumns).toBe(4)
    expect(parsed?.profileId).toBe('')
  })

  it('accepts public image URLs and rejects unsafe resources', () => {
    expect(isValidLauncherResourceUrl('https://cdn.empresa.com/launcher/fondo.webp')).toBe(true)
    expect(isValidLauncherResourceUrl('')).toBe(true)
    expect(isValidLauncherResourceUrl('http://cdn.empresa.com/fondo.png')).toBe(false)
    expect(isValidLauncherResourceUrl('file:///tmp/fondo.png')).toBe(false)
    expect(isValidLauncherResourceUrl('https://127.0.0.1/fondo.png')).toBe(false)
    expect(isValidLauncherResourceUrl('https://10.0.0.8/logo.png')).toBe(false)
    expect(isValidLauncherResourceUrl('https://user:pass@cdn.empresa.com/logo.png')).toBe(false)
    expect(isValidLauncherResourceUrl('data:image/png;base64,AAAA')).toBe(false)
  })

  it('accepts a private asset token and measures contrast', () => {
    expect(isValidLauncherResourceUrl('titan-asset:11111111-1111-1111-1111-111111111111')).toBe(true)
    expect(hasReadableContrast('#F4F7F8', '#0B1720')).toBe(true)
    expect(hasReadableContrast('#777777', '#767676')).toBe(false)
    expect(contrastRatio('#000000', '#FFFFFF')).toBeGreaterThan(20)
  })

  it('rejects grid columns outside 3, 4 and 5 when parsing', () => {
    const configuration = buildLauncherConfiguration(createLauncherDraft())
    const tampered = JSON.stringify(configuration).replace('"gridColumns":4', '"gridColumns":9')
    expect(parseLauncherConfiguration(tampered)?.gridColumns).toBe(4)
  })
})
