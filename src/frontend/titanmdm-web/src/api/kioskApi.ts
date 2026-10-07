import {
  deviceCommandsApi,
  type DeviceCommand,
} from './deviceCommandsApi'

import {
  policiesApi,
  type Policy,
  type PolicyAssignment,
  type PolicyDetails,
  type PolicyPlatform,
} from './policiesApi'

export type KioskMode =
  | 'singleApp'
  | 'multiApp'

export interface AndroidKioskApplication {
  packageName: string
  displayName: string

  installType:
    | 'FORCE_INSTALLED'
    | 'AVAILABLE'
    | 'REQUIRED_FOR_SETUP'

  defaultApp: boolean
}

export interface AndroidKioskConfiguration {
  titanProfileType:
    'kiosk'

  kioskMode:
    KioskMode

  applications:
    AndroidKioskApplication[]

  systemNavigation: {
    homeButton: boolean
    overviewButton: boolean
    statusBar: boolean
    notifications: boolean
  }

  deviceRestrictions: {
    factoryResetDisabled: boolean
    safeBootDisabled: boolean
    screenCaptureDisabled: boolean
    usbFileTransferDisabled: boolean
    outgoingCallsDisabled: boolean
    smsDisabled: boolean
    bluetoothDisabled: boolean
    cameraDisabled: boolean
  }

  display: {
    screenTimeoutSeconds: number
    stayOnWhilePluggedIn: boolean
  }
}

export interface WindowsKioskApplication {
  displayName: string

  appUserModelId?:
    string

  desktopAppPath?:
    string
}

export interface WindowsKioskRestrictions {
  showTaskbar: boolean

  blockTaskManager:
    boolean

  blockSettings:
    boolean

  blockCommandPrompt:
    boolean

  blockRemovableStorage:
    boolean
}

export interface WindowsKioskConfiguration {
  titanProfileType:
    'kiosk'

  kioskMode:
    KioskMode

  account:
    string

  applications:
    WindowsKioskApplication[]

  restrictions:
    WindowsKioskRestrictions
}

export interface CreateAndroidKioskProfile {
  name: string
  description?: string

  configuration:
    AndroidKioskConfiguration
}

export interface CreateWindowsKioskProfile {
  name: string
  description?: string

  configuration:
    WindowsKioskConfiguration
}

function isKioskPolicy(
  configurationJson:
    string,
): boolean {
  try {
    const configuration =
      JSON.parse(
        configurationJson,
      )

    return (
      configuration
        ?.titanProfileType ===
      'kiosk'
    )
  } catch {
    return false
  }
}

function buildAndroidPolicy(
  configuration:
    AndroidKioskConfiguration,
) {
  const applications =
    configuration
      .applications
      .map(
        application => ({
          packageName:
            application.packageName,

          installType:
            application.installType,

          lockTaskAllowed:
            true,

          defaultPermissionPolicy:
            'GRANT',
        }),
      )

  const defaultApp =
    configuration
      .applications
      .find(
        application =>
          application.defaultApp,
      )

  return {
    titanProfileType:
      'kiosk',

    kioskMode:
      configuration.kioskMode,

    applications,

    persistentPreferredActivities:
      defaultApp
        ? [
            {
              receiverActivity:
                `${defaultApp.packageName}/.MainActivity`,

              actions: [
                'android.intent.action.MAIN',
              ],

              categories: [
                'android.intent.category.HOME',
                'android.intent.category.DEFAULT',
              ],
            },
          ]
        : [],

    keyguardDisabled:
      true,

    statusBarDisabled:
      !configuration
        .systemNavigation
        .statusBar,

    screenCaptureDisabled:
      configuration
        .deviceRestrictions
        .screenCaptureDisabled,

    factoryResetDisabled:
      configuration
        .deviceRestrictions
        .factoryResetDisabled,

    safeBootDisabled:
      configuration
        .deviceRestrictions
        .safeBootDisabled,

    usbFileTransferDisabled:
      configuration
        .deviceRestrictions
        .usbFileTransferDisabled,

    outgoingCallsDisabled:
      configuration
        .deviceRestrictions
        .outgoingCallsDisabled,

    smsDisabled:
      configuration
        .deviceRestrictions
        .smsDisabled,

    bluetoothDisabled:
      configuration
        .deviceRestrictions
        .bluetoothDisabled,

    cameraDisabled:
      configuration
        .deviceRestrictions
        .cameraDisabled,

    maximumTimeToLock:
      configuration
        .display
        .screenTimeoutSeconds
      *
      1000,

    stayOnPluggedModes:
      configuration
        .display
        .stayOnWhilePluggedIn
        ? [
            'AC',
            'USB',
            'WIRELESS',
          ]
        : [],
  }
}

async function getKioskProfiles(
  platform:
    PolicyPlatform,
): Promise<Policy[]> {
  const policies =
    await policiesApi
      .getAll(
        platform,
      )

  if (
    policies.length ===
    0
  ) {
    return []
  }

  const details =
    await Promise.all(
      policies.map(
        policy =>
          policiesApi
            .getById(
              policy.id,
            ),
      ),
    )

  const kioskIds =
    new Set(
      details
        .filter(
          policy =>
            isKioskPolicy(
              policy
                .configurationJson,
            ),
        )
        .map(
          policy =>
            policy.id,
        ),
    )

  return policies.filter(
    policy =>
      kioskIds.has(
        policy.id,
      ),
  )
}

export const kioskApi = {
  async getAndroidProfiles():
    Promise<Policy[]> {
    return getKioskProfiles(
      'Android',
    )
  },

  async getWindowsProfiles():
    Promise<Policy[]> {
    return getKioskProfiles(
      'Windows',
    )
  },

  async getProfile(
    policyId: string,
  ): Promise<PolicyDetails> {
    return policiesApi
      .getById(
        policyId,
      )
  },

  async createAndroidProfile(
    request:
      CreateAndroidKioskProfile,
  ): Promise<PolicyDetails> {
    return policiesApi
      .create({
        name:
          request.name,

        description:
          request.description,

        platform:
          'Android',

        configurationJson:
          JSON.stringify(
            buildAndroidPolicy(
              request.configuration,
            ),
          ),
      })
  },

  async createWindowsProfile(
    request:
      CreateWindowsKioskProfile,
  ): Promise<PolicyDetails> {
    return policiesApi
      .create({
        name:
          request.name,

        description:
          request.description,

        platform:
          'Windows',

        configurationJson:
          JSON.stringify(
            request.configuration,
          ),
      })
  },

  async activateAndroid(
    policyId: string,
  ) {
    await policiesApi
      .activate(
        policyId,
      )

    return policiesApi
      .publishAndroid(
        policyId,
      )
  },

  async activateWindows(
    policyId: string,
  ): Promise<void> {
    await policiesApi
      .activate(
        policyId,
      )
  },

  async assignAndroid(
    policyId: string,
    deviceId: string,
  ) {
    return policiesApi
      .assignAndroid(
        policyId,
        deviceId,
      )
  },

  async assignWindows(
    policyId: string,
    deviceId: string,
  ): Promise<
    PolicyAssignment[]
  > {
    return policiesApi
      .assign(
        policyId,
        [
          deviceId,
        ],
      )
  },

  async getWindowsStatus(
    deviceId: string,
  ): Promise<DeviceCommand> {
    return deviceCommandsApi
      .create({
        deviceId,

        commandType:
          'WINDOWS_KIOSK_STATUS',

        payloadJson:
          '{}',

        expirationMinutes:
          15,
      })
  },

  async removeWindowsKiosk(
    deviceId: string,
  ): Promise<DeviceCommand> {
    return deviceCommandsApi
      .create({
        deviceId,

        commandType:
          'WINDOWS_KIOSK_REMOVE',

        payloadJson:
          '{}',

        expirationMinutes:
          30,
      })
  },
}