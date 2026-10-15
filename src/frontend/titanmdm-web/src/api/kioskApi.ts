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
}

export interface AndroidKioskConfiguration {
  application: AndroidKioskApplication

  deviceRestrictions: {
    factoryResetDisabled: boolean
    safeBootDisabled: boolean
    screenCaptureDisabled: boolean
    usbFileTransferDisabled: boolean
    bluetoothDisabled: boolean
    cameraDisabled: boolean
  }

  disableStatusBar: boolean
  disableKeyguard: boolean
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

export function isKioskPolicy(
  configurationJson:
    string,
): boolean {
  try {
    const configuration =
      JSON.parse(
        configurationJson,
      )

    return configuration?.titanProfileType === 'kiosk'
  } catch {
    return false
  }
}

export function buildAndroidKioskPolicy(
  configuration:
    AndroidKioskConfiguration,
) {
  return {
    schemaVersion: 2,
    titanProfileType: 'kiosk' as const,
    android: {
      kiosk: {
        enabled: true,
        applicationId:
          configuration.application.packageName.trim(),
        disableStatusBar:
          configuration.disableStatusBar,
        disableKeyguard:
          configuration.disableKeyguard,
      },
      applications: {
        playStoreMode: 'WHITELIST' as const,
        packages: [
          {
            packageName:
              configuration.application.packageName.trim(),
            label:
              configuration.application.displayName.trim(),
            installType: 'KIOSK' as const,
          },
        ],
      },
      restrictions: {
        blockFactoryReset:
          configuration.deviceRestrictions.factoryResetDisabled,
        blockSafeBoot:
          configuration.deviceRestrictions.safeBootDisabled,
        blockScreenCapture:
          configuration.deviceRestrictions.screenCaptureDisabled,
        blockUsbFileTransfer:
          configuration.deviceRestrictions.usbFileTransferDisabled,
        blockBluetooth:
          configuration.deviceRestrictions.bluetoothDisabled,
        blockCamera:
          configuration.deviceRestrictions.cameraDisabled,
      },
    },
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
            buildAndroidKioskPolicy(
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
