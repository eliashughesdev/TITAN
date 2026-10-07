export type JsonRecord =
  Record<string, unknown>

export interface WindowsDeviceInventory {
  computerName: string
  userName: string
  domainName: string
  operatingSystem: string
  operatingSystemVersion: string
  osArchitecture: string
  processArchitecture: string
  framework: string
  processorCount: number | null
  is64BitOperatingSystem: boolean | null
  machineGuid: string
  productName: string
  displayVersion: string
  currentBuild: string
  installDateUtc: string | null
  systemDrive: string
  systemDriveTotalBytes: number | null
  systemDriveFreeBytes: number | null
  agentVersion: string
}

export interface WindowsNetworkAdapter {
  name: string
  description: string
  interfaceType: string
  operationalStatus: string
  macAddress: string
  speed: number | null
  ipAddresses: string[]
  gateways: string[]
  dnsServers: string[]
}

export interface WindowsApplication {
  name: string
  version: string
  publisher: string
  installLocation: string
  uninstallString: string
}

export function parseJson(
  value: string | null | undefined,
): unknown {
  if (!value) {
    return null
  }

  try {
    return JSON.parse(value)
  } catch {
    return value
  }
}

function asRecord(
  value: unknown,
): JsonRecord | null {
  if (
    typeof value === 'object'
    &&
    value !== null
    &&
    !Array.isArray(value)
  ) {
    return value as JsonRecord
  }

  return null
}

function findValue(
  record: JsonRecord | null,
  ...names: string[]
): unknown {
  if (!record) {
    return undefined
  }

  for (const name of names) {
    if (name in record) {
      return record[name]
    }

    const actualKey =
      Object.keys(record)
        .find(
          key =>
            key.toLowerCase()
            ===
            name.toLowerCase(),
        )

    if (actualKey) {
      return record[actualKey]
    }
  }

  return undefined
}

function toStringValue(
  value: unknown,
): string {
  if (
    value === null
    ||
    value === undefined
  ) {
    return ''
  }

  return String(value)
}

function toNumberValue(
  value: unknown,
): number | null {
  if (
    typeof value === 'number'
    &&
    Number.isFinite(value)
  ) {
    return value
  }

  const parsed =
    Number(value)

  return Number.isFinite(parsed)
    ? parsed
    : null
}

function toBooleanValue(
  value: unknown,
): boolean | null {
  if (typeof value === 'boolean') {
    return value
  }

  if (
    String(value).toLowerCase()
    ===
    'true'
  ) {
    return true
  }

  if (
    String(value).toLowerCase()
    ===
    'false'
  ) {
    return false
  }

  return null
}

function toStringArray(
  value: unknown,
): string[] {
  if (!Array.isArray(value)) {
    return []
  }

  return value
    .map(
      item =>
        toStringValue(item),
    )
    .filter(Boolean)
}

export function getInventoryDevice(
  raw: unknown,
): WindowsDeviceInventory | null {
  const root =
    asRecord(raw)

  const device =
    asRecord(
      findValue(
        root,
        'Device',
        'device',
      ),
    )
    ??
    root

  if (!device) {
    return null
  }

  return {
    computerName:
      toStringValue(
        findValue(
          device,
          'ComputerName',
        ),
      ),

    userName:
      toStringValue(
        findValue(
          device,
          'UserName',
        ),
      ),

    domainName:
      toStringValue(
        findValue(
          device,
          'DomainName',
        ),
      ),

    operatingSystem:
      toStringValue(
        findValue(
          device,
          'OperatingSystem',
        ),
      ),

    operatingSystemVersion:
      toStringValue(
        findValue(
          device,
          'OperatingSystemVersion',
        ),
      ),

    osArchitecture:
      toStringValue(
        findValue(
          device,
          'OsArchitecture',
        ),
      ),

    processArchitecture:
      toStringValue(
        findValue(
          device,
          'ProcessArchitecture',
        ),
      ),

    framework:
      toStringValue(
        findValue(
          device,
          'Framework',
        ),
      ),

    processorCount:
      toNumberValue(
        findValue(
          device,
          'ProcessorCount',
        ),
      ),

    is64BitOperatingSystem:
      toBooleanValue(
        findValue(
          device,
          'Is64BitOperatingSystem',
        ),
      ),

    machineGuid:
      toStringValue(
        findValue(
          device,
          'MachineGuid',
        ),
      ),

    productName:
      toStringValue(
        findValue(
          device,
          'ProductName',
        ),
      ),

    displayVersion:
      toStringValue(
        findValue(
          device,
          'DisplayVersion',
        ),
      ),

    currentBuild:
      toStringValue(
        findValue(
          device,
          'CurrentBuild',
        ),
      ),

    installDateUtc:
      toStringValue(
        findValue(
          device,
          'InstallDateUtc',
        ),
      )
      ||
      null,

    systemDrive:
      toStringValue(
        findValue(
          device,
          'SystemDrive',
        ),
      ),

    systemDriveTotalBytes:
      toNumberValue(
        findValue(
          device,
          'SystemDriveTotalBytes',
        ),
      ),

    systemDriveFreeBytes:
      toNumberValue(
        findValue(
          device,
          'SystemDriveFreeBytes',
        ),
      ),

    agentVersion:
      toStringValue(
        findValue(
          device,
          'AgentVersion',
        ),
      ),
  }
}

export function getNetworkAdapters(
  raw: unknown,
): WindowsNetworkAdapter[] {
  const root =
    asRecord(raw)

  const source =
    Array.isArray(raw)
      ? raw
      : findValue(
          root,
          'Network',
        )

  if (!Array.isArray(source)) {
    return []
  }

  return source
    .map(
      value => {
        const record =
          asRecord(value)

        if (!record) {
          return null
        }

        return {
          name:
            toStringValue(
              findValue(
                record,
                'Name',
              ),
            ),

          description:
            toStringValue(
              findValue(
                record,
                'Description',
              ),
            ),

          interfaceType:
            toStringValue(
              findValue(
                record,
                'InterfaceType',
              ),
            ),

          operationalStatus:
            toStringValue(
              findValue(
                record,
                'OperationalStatus',
              ),
            ),

          macAddress:
            toStringValue(
              findValue(
                record,
                'MacAddress',
              ),
            ),

          speed:
            toNumberValue(
              findValue(
                record,
                'Speed',
              ),
            ),

          ipAddresses:
            toStringArray(
              findValue(
                record,
                'IpAddresses',
              ),
            ),

          gateways:
            toStringArray(
              findValue(
                record,
                'Gateways',
              ),
            ),

          dnsServers:
            toStringArray(
              findValue(
                record,
                'DnsServers',
              ),
            ),
        }
      },
    )
    .filter(
      (
        item,
      ): item is
        WindowsNetworkAdapter =>
        item !== null,
    )
}

export function getApplications(
  raw: unknown,
): WindowsApplication[] {
  const root =
    asRecord(raw)

  const source =
    Array.isArray(raw)
      ? raw
      : findValue(
          root,
          'Applications',
        )

  if (!Array.isArray(source)) {
    return []
  }

  return source
    .map(
      value => {
        const record =
          asRecord(value)

        if (!record) {
          return null
        }

        const name =
          toStringValue(
            findValue(
              record,
              'Name',
            ),
          )

        if (!name) {
          return null
        }

        return {
          name,

          version:
            toStringValue(
              findValue(
                record,
                'Version',
              ),
            ),

          publisher:
            toStringValue(
              findValue(
                record,
                'Publisher',
              ),
            ),

          installLocation:
            toStringValue(
              findValue(
                record,
                'InstallLocation',
              ),
            ),

          uninstallString:
            toStringValue(
              findValue(
                record,
                'UninstallString',
              ),
            ),
        }
      },
    )
    .filter(
      (
        item,
      ): item is
        WindowsApplication =>
        item !== null,
    )
}

export function formatBytes(
  bytes: number | null,
): string {
  if (
    bytes === null
    ||
    bytes < 0
  ) {
    return 'N/D'
  }

  if (bytes === 0) {
    return '0 B'
  }

  const units = [
    'B',
    'KB',
    'MB',
    'GB',
    'TB',
  ]

  const power =
    Math.min(
      Math.floor(
        Math.log(bytes)
        /
        Math.log(1024),
      ),
      units.length - 1,
    )

  const value =
    bytes
    /
    Math.pow(
      1024,
      power,
    )

  return `${value.toFixed(
    power >= 3
      ? 1
      : 0,
  )} ${units[power]}`
}

export function formatSpeed(
  bitsPerSecond: number | null,
): string {
  if (
    bitsPerSecond === null
    ||
    bitsPerSecond <= 0
  ) {
    return 'N/D'
  }

  if (
    bitsPerSecond >=
    1_000_000_000
  ) {
    return `${
      bitsPerSecond
      /
      1_000_000_000
    } Gbps`
  }

  return `${
    Math.round(
      bitsPerSecond
      /
      1_000_000,
    )
  } Mbps`
}

export function formatDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return 'N/D'
  }

  const date =
    new Date(value)

  return Number.isNaN(
    date.getTime(),
  )
    ? 'N/D'
    : date.toLocaleString()
}

export function extractProductCode(
  uninstallString: string,
): string | null {
  const match =
    uninstallString.match(
      /\{[0-9a-fA-F-]{36}\}/,
    )

  return match?.[0] ?? null
}

export function extractExecutable(
  uninstallString: string,
): string | null {
  if (!uninstallString) {
    return null
  }

  const quoted =
    uninstallString.match(
      /^"([^"]+\.exe)"/i,
    )

  if (quoted?.[1]) {
    return quoted[1]
  }

  const plain =
    uninstallString.match(
      /^(.+?\.exe)(?:\s|$)/i,
    )

  return plain?.[1]
    ?.trim()
    ??
    null
}

export function getStorageUsage(
  total: number | null,
  free: number | null,
) {
  if (
    total === null
    ||
    free === null
    ||
    total <= 0
  ) {
    return {
      used: null,
      percent: 0,
    }
  }

  const used =
    Math.max(
      total - free,
      0,
    )

  return {
    used,
    percent:
      Math.min(
        100,
        Math.round(
          used
          /
          total
          *
          100,
        ),
      ),
  }
}