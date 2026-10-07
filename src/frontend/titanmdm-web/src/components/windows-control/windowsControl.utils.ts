import type {
  DeviceCommand,
} from '../../api/deviceCommandsApi'

import type {
  JsonObject,
  SecurityView,
  UninstallTarget,
  UpdateHistoryItem,
  UpdateView,
  WindowsApplicationItem,
  WindowsProcessItem,
  WindowsServiceItem,
} from './windowsControl.types'

export function formatDate(
  value:
    string | null | undefined,
): string {
  if (!value) {
    return 'N/D'
  }

  const date =
    new Date(value)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return 'N/D'
  }

  return date.toLocaleString()
}

export function formatBytes(
  value: number,
): string {
  if (
    !Number.isFinite(
      value,
    )
  ) {
    return 'N/D'
  }

  const units = [
    'B',
    'KB',
    'MB',
    'GB',
    'TB',
  ]

  let size =
    value

  let unit =
    0

  while (
    size >= 1024
    &&
    unit <
      units.length - 1
  ) {
    size /= 1024
    unit++
  }

  return `${size.toFixed(
    unit === 0
      ? 0
      : 1,
  )} ${units[unit]}`
}

export function safeParseJson(
  value:
    string | null | undefined,
): unknown {
  if (!value) {
    return null
  }

  try {
    return JSON.parse(
      value,
    )
  } catch {
    return value
  }
}

export function asObject(
  value: unknown,
): JsonObject | null {
  if (
    typeof value ===
      'object'
    &&
    value !==
      null
    &&
    !Array.isArray(
      value,
    )
  ) {
    return value as JsonObject
  }

  return null
}

export function property(
  object:
    JsonObject | null,
  ...names: string[]
): unknown {
  if (!object) {
    return undefined
  }

  for (
    const name
    of names
  ) {
    if (
      Object.prototype
        .hasOwnProperty
        .call(
          object,
          name,
        )
    ) {
      return object[name]
    }
  }

  return undefined
}

export function stringValue(
  object:
    JsonObject | null,
  ...names: string[]
): string | null {
  const value =
    property(
      object,
      ...names,
    )

  return typeof value ===
    'string'
      ? value
      : null
}

export function booleanValue(
  object:
    JsonObject | null,
  ...names: string[]
): boolean | null {
  const value =
    property(
      object,
      ...names,
    )

  return typeof value ===
    'boolean'
      ? value
      : null
}

export function numberValue(
  object:
    JsonObject | null,
  ...names: string[]
): number | null {
  const value =
    property(
      object,
      ...names,
    )

  return (
    typeof value ===
      'number'
    &&
    Number.isFinite(
      value,
    )
  )
    ? value
    : null
}

export function objectValue(
  object:
    JsonObject | null,
  ...names: string[]
): JsonObject | null {
  return asObject(
    property(
      object,
      ...names,
    ),
  )
}

export function latestByType(
  commands:
    DeviceCommand[],
  commandType:
    string,
): DeviceCommand | null {
  return (
    [...commands]
      .filter(
        command =>
          command.commandType ===
          commandType,
      )
      .sort(
        (
          left,
          right,
        ) =>
          new Date(
            right.createdAtUtc,
          ).getTime()
          -
          new Date(
            left.createdAtUtc,
          ).getTime(),
      )[0]
    ??
    null
  )
}

export function commandArray(
  command:
    DeviceCommand | null,
): unknown[] {
  if (
    !command
    ||
    command.status !==
      'Success'
    ||
    !command.resultJson
  ) {
    return []
  }

  const parsed =
    safeParseJson(
      command.resultJson,
    )

  return Array.isArray(
    parsed,
  )
    ? parsed
    : []
}

export function prettyResult(
  command:
    DeviceCommand | null,
): string {
  if (!command) {
    return 'Todavía no existe información para esta consulta.'
  }

  if (
    command.status ===
      'Failed'
    ||
    command.status ===
      'Timeout'
  ) {
    return (
      command.errorMessage
      ??
      command.errorCode
      ??
      'El comando terminó con error.'
    )
  }

  const parsed =
    safeParseJson(
      command.resultJson,
    )

  if (
    parsed ===
    null
  ) {
    return (
      `Estado: ${command.status}\n`
      +
      'Esperando resultado del agente...'
    )
  }

  if (
    typeof parsed ===
    'string'
  ) {
    return parsed
  }

  return JSON.stringify(
    parsed,
    null,
    2,
  )
}

export function normalizeProcesses(
  command:
    DeviceCommand | null,
): WindowsProcessItem[] {
  return commandArray(
    command,
  )
    .map(
      value => {
        const item =
          asObject(
            value,
          )

        return {
          processId:
            numberValue(
              item,
              'ProcessId',
              'processId',
            )
            ??
            0,

          name:
            stringValue(
              item,
              'Name',
              'name',
            )
            ??
            'Unknown',

          workingSetBytes:
            numberValue(
              item,
              'WorkingSetBytes',
              'workingSetBytes',
            )
            ??
            0,

          startTimeUtc:
            stringValue(
              item,
              'StartTimeUtc',
              'startTimeUtc',
            ),
        }
      },
    )
    .filter(
      item =>
        item.processId >
        0,
    )
}

export function normalizeServices(
  command:
    DeviceCommand | null,
): WindowsServiceItem[] {
  return commandArray(
    command,
  )
    .map(
      value => {
        const item =
          asObject(
            value,
          )

        return {
          name:
            stringValue(
              item,
              'Name',
              'name',
            )
            ??
            '',

          displayName:
            stringValue(
              item,
              'DisplayName',
              'displayName',
            ),

          imagePath:
            stringValue(
              item,
              'ImagePath',
              'imagePath',
            ),

          startType:
            stringValue(
              item,
              'StartType',
              'startType',
            ),

          serviceType:
            stringValue(
              item,
              'ServiceType',
              'serviceType',
            ),
        }
      },
    )
    .filter(
      item =>
        item.name.length >
        0,
    )
}

export function normalizeApplications(
  command:
    DeviceCommand | null,
): WindowsApplicationItem[] {
  return commandArray(
    command,
  )
    .map(
      value => {
        const item =
          asObject(
            value,
          )

        return {
          name:
            stringValue(
              item,
              'Name',
              'name',
            )
            ??
            '',

          version:
            stringValue(
              item,
              'Version',
              'version',
            ),

          publisher:
            stringValue(
              item,
              'Publisher',
              'publisher',
            ),

          installLocation:
            stringValue(
              item,
              'InstallLocation',
              'installLocation',
            ),

          uninstallString:
            stringValue(
              item,
              'UninstallString',
              'uninstallString',
            ),
        }
      },
    )
    .filter(
      item =>
        item.name.length >
        0,
    )
}

function parseEmbeddedObject(
  snapshot:
    JsonObject | null,
): JsonObject | null {
  const raw =
    stringValue(
      snapshot,
      'RawJson',
      'rawJson',
    )

  return asObject(
    safeParseJson(
      raw,
    ),
  )
}

export function parseSecurity(
  command:
    DeviceCommand | null,
): SecurityView {
  const empty:
    SecurityView = {
      available: false,

      defenderAvailable: false,
      defenderRealTime: null,
      defenderVersion: null,

      firewallAvailable: false,
      firewallEnabledProfiles: null,

      bitLockerAvailable: false,
      bitLockerProtected: null,

      tpmAvailable: false,
      tpmPresent: null,
      tpmReady: null,

      secureBoot: null,
      uacEnabled: null,
      pendingReboot: null,
      remoteDesktopEnabled: null,
    }

  if (
    !command
    ||
    command.status !==
      'Success'
  ) {
    return empty
  }

  const root =
    asObject(
      safeParseJson(
        command.resultJson,
      ),
    )

  if (!root) {
    return empty
  }

  const defender =
    objectValue(
      root,
      'Defender',
      'defender',
    )

  const firewall =
    objectValue(
      root,
      'Firewall',
      'firewall',
    )

  const bitLocker =
    objectValue(
      root,
      'BitLocker',
      'bitLocker',
    )

  const tpm =
    objectValue(
      root,
      'Tpm',
      'tpm',
    )

  const defenderRaw =
    parseEmbeddedObject(
      defender,
    )

  const tpmRaw =
    parseEmbeddedObject(
      tpm,
    )

  const firewallRaw =
    safeParseJson(
      stringValue(
        firewall,
        'RawJson',
        'rawJson',
      ),
    )

  const firewallProfiles =
    Array.isArray(
      firewallRaw,
    )
      ? firewallRaw
      : firewallRaw
        ? [firewallRaw]
        : []

  const bitLockerRaw =
    safeParseJson(
      stringValue(
        bitLocker,
        'RawJson',
        'rawJson',
      ),
    )

  const bitLockerVolumes =
    Array.isArray(
      bitLockerRaw,
    )
      ? bitLockerRaw
      : bitLockerRaw
        ? [bitLockerRaw]
        : []

  const enabledProfiles =
    firewallProfiles
      .map(
        value =>
          asObject(
            value,
          ),
      )
      .filter(
        value =>
          booleanValue(
            value,
            'Enabled',
            'enabled',
          ) ===
          true,
      )
      .length

  const protectedVolumes =
    bitLockerVolumes
      .map(
        value =>
          asObject(
            value,
          ),
      )
      .filter(
        value => {
          const status =
            property(
              value,
              'ProtectionStatus',
              'protectionStatus',
            )

          return (
            status ===
              1
            ||
            String(
              status ??
              '',
            )
              .toLowerCase()
              .includes(
                'on',
              )
          )
        },
      )
      .length

  return {
    available: true,

    defenderAvailable:
      booleanValue(
        defender,
        'Available',
        'available',
      )
      ??
      false,

    defenderRealTime:
      booleanValue(
        defenderRaw,
        'RealTimeProtectionEnabled',
        'realTimeProtectionEnabled',
      ),

    defenderVersion:
      stringValue(
        defenderRaw,
        'AntivirusSignatureVersion',
        'antivirusSignatureVersion',
      ),

    firewallAvailable:
      booleanValue(
        firewall,
        'Available',
        'available',
      )
      ??
      false,

    firewallEnabledProfiles:
      firewallProfiles.length >
      0
        ? enabledProfiles
        : null,

    bitLockerAvailable:
      booleanValue(
        bitLocker,
        'Available',
        'available',
      )
      ??
      false,

    bitLockerProtected:
      bitLockerVolumes.length >
      0
        ? protectedVolumes >
          0
        : null,

    tpmAvailable:
      booleanValue(
        tpm,
        'Available',
        'available',
      )
      ??
      false,

    tpmPresent:
      booleanValue(
        tpmRaw,
        'TpmPresent',
        'tpmPresent',
      ),

    tpmReady:
      booleanValue(
        tpmRaw,
        'TpmReady',
        'tpmReady',
      ),

    secureBoot:
      booleanValue(
        root,
        'SecureBootEnabled',
        'secureBootEnabled',
      ),

    uacEnabled:
      booleanValue(
        root,
        'UacEnabled',
        'uacEnabled',
      ),

    pendingReboot:
      booleanValue(
        root,
        'PendingReboot',
        'pendingReboot',
      ),

    remoteDesktopEnabled:
      booleanValue(
        root,
        'RemoteDesktopEnabled',
        'remoteDesktopEnabled',
      ),
  }
}

function normalizeUpdateHistory(
  raw:
    string | null,
): UpdateHistoryItem[] {
  const parsed =
    safeParseJson(
      raw,
    )

  const items =
    Array.isArray(
      parsed,
    )
      ? parsed
      : parsed
        ? [parsed]
        : []

  return items.map(
    value => {
      const item =
        asObject(
          value,
        )

      return {
        title:
          stringValue(
            item,
            'Title',
            'title',
          )
          ??
          'Windows Update',

        date:
          stringValue(
            item,
            'Date',
            'date',
          ),

        resultCode:
          numberValue(
            item,
            'ResultCode',
            'resultCode',
          ),

        hResult:
          numberValue(
            item,
            'HResult',
            'hResult',
          ),
      }
    },
  )
}

function normalizeAvailableUpdates(
  raw:
    string | null,
): import(
  './windowsControl.types'
).AvailableWindowsUpdate[] {
  const parsed =
    safeParseJson(
      raw,
    )

  const items =
    Array.isArray(
      parsed,
    )
      ? parsed
      : parsed
        ? [parsed]
        : []

  return items.map(
    value => {
      const item =
        asObject(
          value,
        )

      const kbValue =
        property(
          item,
          'kbArticleIds',
          'KbArticleIds',
        )

      const kbArticleIds =
        Array.isArray(
          kbValue,
        )
          ? kbValue.map(
              value =>
                String(
                  value,
                ),
            )
          : []

      return {
        title:
          stringValue(
            item,
            'title',
            'Title',
          )
          ??
          'Windows Update',

        kbArticleIds,

        severity:
          stringValue(
            item,
            'severity',
            'Severity',
          ),

        rebootRequired:
          booleanValue(
            item,
            'rebootRequired',
            'RebootRequired',
          ),

        isDownloaded:
          booleanValue(
            item,
            'isDownloaded',
            'IsDownloaded',
          ),

        eulaAccepted:
          booleanValue(
            item,
            'eulaAccepted',
            'EulaAccepted',
          ),
      }
    },
  )
}

export function parseUpdate(
  command:
    DeviceCommand | null,
): UpdateView {
  const empty:
    UpdateView = {
      available:
        false,

      serviceStatus:
        'Unknown',

      serviceQuerySucceeded:
        false,

      pendingReboot:
        null,

      historyAvailable:
        false,

      history:
        [],

      availableUpdatesAvailable:
        false,

      availableUpdates:
        [],

      collectedAtUtc:
        null,
    }

  if (
    !command
    ||
    command.status !==
      'Success'
  ) {
    return empty
  }

  const root =
    asObject(
      safeParseJson(
        command.resultJson,
      ),
    )

  if (!root) {
    return empty
  }

  const service =
    objectValue(
      root,
      'WindowsUpdateService',
      'windowsUpdateService',
    )

  return {
    available:
      true,

    serviceStatus:
      stringValue(
        service,
        'Status',
        'status',
      )
      ??
      'Unknown',

    serviceQuerySucceeded:
      booleanValue(
        service,
        'QuerySucceeded',
        'querySucceeded',
      )
      ??
      false,

    pendingReboot:
      booleanValue(
        root,
        'PendingReboot',
        'pendingReboot',
      ),

    historyAvailable:
      booleanValue(
        root,
        'HistoryAvailable',
        'historyAvailable',
      )
      ??
      false,

    history:
      normalizeUpdateHistory(
        stringValue(
          root,
          'UpdateHistoryJson',
          'updateHistoryJson',
        ),
      ),

    availableUpdatesAvailable:
      booleanValue(
        root,
        'AvailableUpdatesAvailable',
        'availableUpdatesAvailable',
      )
      ??
      false,

    availableUpdates:
      normalizeAvailableUpdates(
        stringValue(
          root,
          'AvailableUpdatesJson',
          'availableUpdatesJson',
        ),
      ),

    collectedAtUtc:
      stringValue(
        root,
        'CollectedAtUtc',
        'collectedAtUtc',
      ),
  }
}

export function extractUninstallTarget(
  application:
    WindowsApplicationItem,
): UninstallTarget {
  const uninstall =
    application
      .uninstallString
      ?.trim()
    ??
    ''

  const guid =
    uninstall.match(
      /\{[0-9a-fA-F-]{36}\}/,
    )?.[0]
    ??
    ''

  if (
    guid
    &&
    uninstall
      .toLowerCase()
      .includes(
        'msiexec',
      )
  ) {
    return {
      name:
        application.name,

      productCode:
        guid,

      executable:
        '',

      arguments:
        '',
    }
  }

  if (
    uninstall.startsWith(
      '"',
    )
  ) {
    const closing =
      uninstall.indexOf(
        '"',
        1,
      )

    if (
      closing >
      1
    ) {
      return {
        name:
          application.name,

        productCode:
          '',

        executable:
          uninstall.slice(
            1,
            closing,
          ),

        arguments:
          uninstall
            .slice(
              closing + 1,
            )
            .trim(),
      }
    }
  }

  const executableIndex =
    uninstall
      .toLowerCase()
      .indexOf(
        '.exe',
      )

  if (
    executableIndex >=
    0
  ) {
    return {
      name:
        application.name,

      productCode:
        '',

      executable:
        uninstall.slice(
          0,
          executableIndex + 4,
        ),

      arguments:
        uninstall
          .slice(
            executableIndex + 4,
          )
          .trim(),
    }
  }

  return {
    name:
      application.name,

    productCode:
      '',

    executable:
      '',

    arguments:
      '',
  }
}

export function getErrorMessage(
  error: unknown,
  fallback: string,
): string {
  if (
    typeof error ===
      'object'
    &&
    error !==
      null
    &&
    'response' in
      error
  ) {
    const response =
      (
        error as {
          response?: {
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (
      response
        ?.data
        ?.message
    ) {
      return response
        .data
        .message
    }
  }

  if (
    error instanceof
    Error
    &&
    error.message
  ) {
    return error.message
  }

  return fallback
}