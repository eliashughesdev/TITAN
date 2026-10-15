export type LauncherInstallType =
  | 'AVAILABLE'
  | 'FORCE_INSTALLED'
  | 'REQUIRED_FOR_SETUP'

export interface LauncherApplication {
  packageName: string
  label: string
  installType: LauncherInstallType
  visible: boolean
}

export interface LauncherDraft {
  companyName: string
  welcomeMessage: string
  primaryColor: string
  backgroundColor: string
  packageName: string
  activityName: string
  requiredForSetup: boolean
  schemaVersion: 1
  applications: LauncherApplication[]
}

export const DEFAULT_PRIMARY_COLOR = '#16A36A'
export const DEFAULT_BACKGROUND_COLOR = '#0B1720'

export const createLauncherDraft = (): LauncherDraft => ({
  companyName: 'TitanMDM Enterprise',
  welcomeMessage: 'Dispositivo corporativo',
  primaryColor: DEFAULT_PRIMARY_COLOR,
  backgroundColor: DEFAULT_BACKGROUND_COLOR,
  packageName: 'com.titanmdm.agent',
  activityName: 'com.titanmdm.agent.launcher.TitanLauncherActivity',
  requiredForSetup: true,
  schemaVersion: 1,
  applications: [],
})

const brandingColorPattern = /^#[0-9A-Fa-f]{6}$/

export function isValidBrandingColor(value: string): boolean {
  return brandingColorPattern.test(value.trim())
}

export function normalizeBrandingColor(
  value: unknown,
  fallback: string,
): string {
  return typeof value === 'string' && isValidBrandingColor(value)
    ? value.trim().toUpperCase()
    : fallback
}

export function buildLauncherConfiguration(draft: LauncherDraft) {
  return {
    schemaVersion: 2,
    titanProfileType: 'titan-launcher' as const,
    android: {
      kiosk: {
        enabled: false,
        applicationId: '',
      },
      applications: {
        allowAppInstallation: true,
        allowAppUninstallation: false,
        playStoreMode: 'WHITELIST' as const,
        packages: draft.applications.map((application) => ({
          packageName: application.packageName.trim(),
          label: application.label.trim() || application.packageName.trim(),
          installType: application.visible
            ? application.installType
            : 'BLOCKED' as const,
          visible: application.visible,
        })),
      },
      launcher: {
        enabled: true,
        packageName: draft.packageName.trim(),
        activityName: draft.activityName.trim(),
        companyName: draft.companyName.trim(),
        welcomeMessage: draft.welcomeMessage.trim(),
        primaryColor: draft.primaryColor.trim().toUpperCase(),
        backgroundColor: draft.backgroundColor.trim().toUpperCase(),
        requiredForSetup: draft.requiredForSetup,
        schemaVersion: 1,
      },
    },
  }
}

function asObject(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null
}

function asString(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback
}

function isInstallType(value: unknown): value is LauncherInstallType {
  return value === 'AVAILABLE' ||
    value === 'FORCE_INSTALLED' ||
    value === 'REQUIRED_FOR_SETUP'
}

export function parseLauncherConfiguration(
  configurationJson: string,
): LauncherDraft | null {
  try {
    const root = asObject(JSON.parse(configurationJson))
    const android = asObject(root?.android)
    const launcher = asObject(android?.launcher)

    if (launcher?.enabled !== true) return null

    const defaults = createLauncherDraft()
    const applications = asObject(android?.applications)
    const packages = Array.isArray(applications?.packages)
      ? applications.packages
      : []

    return {
      companyName: asString(launcher.companyName, defaults.companyName),
      welcomeMessage: asString(launcher.welcomeMessage, defaults.welcomeMessage),
      primaryColor: normalizeBrandingColor(launcher.primaryColor, defaults.primaryColor),
      backgroundColor: normalizeBrandingColor(launcher.backgroundColor, defaults.backgroundColor),
      packageName: asString(launcher.packageName, defaults.packageName),
      activityName: asString(launcher.activityName, defaults.activityName),
      requiredForSetup: typeof launcher.requiredForSetup === 'boolean'
        ? launcher.requiredForSetup
        : defaults.requiredForSetup,
      schemaVersion: 1,
      applications: packages.flatMap((value) => {
        const application = asObject(value)
        const packageName = asString(application?.packageName, '').trim()
        if (!packageName || packageName === asString(launcher.packageName, defaults.packageName)) {
          return []
        }

        const rawInstallType = application?.installType
        const visible = application?.visible !== false && rawInstallType !== 'BLOCKED'
        return [{
          packageName,
          label: asString(application?.label, packageName),
          installType: isInstallType(rawInstallType) ? rawInstallType : 'AVAILABLE',
          visible,
        }]
      }),
    }
  } catch {
    return null
  }
}

export function moveLauncherApplication(
  applications: LauncherApplication[],
  index: number,
  direction: -1 | 1,
): LauncherApplication[] {
  const destination = index + direction
  if (index < 0 || destination < 0 || destination >= applications.length) {
    return applications
  }

  const next = [...applications]
  ;[next[index], next[destination]] = [next[destination], next[index]]
  return next
}
