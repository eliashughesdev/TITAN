import type {
  DeviceCommand,
} from '../../api/deviceCommandsApi'

export function deviceCommand(
  overrides: Partial<DeviceCommand> & {
    id?: string
    commandType: string
  },
): DeviceCommand {
  const now = new Date().toISOString()

  const base = {
    id:
      overrides.id ??
      'cmd-test',
    deviceId: 'dev-windows-001',
    status: 'Success' as const,
    createdAtUtc: now,
    updatedAtUtc: now,
    sentAtUtc: now,
    deliveredAtUtc: now,
    startedAtUtc: now,
    completedAtUtc: now,
    resultJson: null,
    errorCode: null,
    errorMessage: null,
    deliveryAttempts: 1,
  }

  return {
    ...base,
    ...overrides,
  } as DeviceCommand
}

export function inventoryCommand(): DeviceCommand {
  return deviceCommand({
    id: 'cmd-inventory',
    commandType: 'DEVICE_INVENTORY',
    resultJson: JSON.stringify({
      Device: {
        ComputerName: 'PC-CONTABILIDAD-07',
        UserName: 'CORP\\lrojas',
        DomainName: 'CORP',
        Manufacturer: 'Dell',
        Model: 'Latitude 5540',
        SerialNumber: 'DEL5540X7Y2',
        CpuName: 'Intel(R) Core(TM) i5-1345U',
        TotalMemoryBytes: 17078357504,
        ProductName: 'Windows 11 Pro',
        OperatingSystemVersion: '10.0.22631',
        CurrentBuild: '22631',
        DisplayVersion: '23H2',
        OsArchitecture: 'x64',
        AgentVersion: '1.0.0',
        InstallDateUtc: '2026-01-15T10:12:00Z',
        SystemDrive: 'C:',
        SystemDriveFreeBytes: 118099243008,
      },
      Network: [
        {
          Name: 'Ethernet 2',
          Description: 'Realtek PCIe GbE',
          InterfaceType: 'Ethernet',
          OperationalStatus: 'Up',
          MacAddress: 'D4:3A:2C:11:AB:99',
          Speed: 1000000000,
          IpAddresses: ['10.20.31.45', 'fe80::3d2a:2cff:fe11:ab99'],
        },
        {
          Name: 'Wi-Fi',
          Description: 'Intel Wi-Fi 6E',
          InterfaceType: 'Wireless',
          OperationalStatus: 'Down',
          MacAddress: 'A4:C1:38:55:EF:12',
          Speed: 1201000000,
          IpAddresses: [],
        },
      ],
      Disks: [
        {
          Name: 'C:',
          DriveType: 'Fixed',
          FileSystem: 'NTFS',
          VolumeLabel: 'Sistema',
          TotalBytes: 511101108224,
          FreeBytes: 118099243008,
        },
        {
          Name: 'D:',
          DriveType: 'Fixed',
          FileSystem: 'NTFS',
          VolumeLabel: 'Datos',
          TotalBytes: 1024209547264,
          FreeBytes: 512104773632,
        },
      ],
      Applications: [
        {
          Name: 'Microsoft Edge',
          Version: '131.0.2903.98',
          Publisher: 'Microsoft Corporation',
          UninstallString:
            'MsiExec.exe /X{B9F7D3B2-6F4A-4B4A-9C1A-8E1F2A3B4C5D}',
        },
        {
          Name: '7-Zip 24.09',
          Version: '24.09',
          Publisher: 'Igor Pavlov',
          UninstallString:
            '"C:\\Program Files\\7-Zip\\Uninstall.exe"',
        },
      ],
      Processes: [
        {
          ProcessId: 4,
          Name: 'System',
          WorkingSetBytes: 2621440,
          StartTimeUtc: '2026-10-01T06:00:00Z',
        },
        {
          ProcessId: 8992,
          Name: 'explorer.exe',
          WorkingSetBytes: 141004800,
          StartTimeUtc: '2026-10-10T14:02:11Z',
        },
      ],
      Services: [
        {
          Name: 'WinDefend',
          DisplayName: 'Microsoft Defender Antivirus Service',
          ImagePath: 'C:\\Program Files\\Windows Defender\\MsMpEng.exe',
          StartType: 'Automatic',
          ServiceType: 'Win32OwnProcess',
        },
      ],
      CollectedAtUtc: '2026-10-10T15:00:00Z',
    }),
  })
}

export function securityCommand(): DeviceCommand {
  return deviceCommand({
    commandType: 'SECURITY_STATUS',
    resultJson: JSON.stringify({
      Defender: {
        Available: true,
        RawJson: JSON.stringify({
          RealTimeProtectionEnabled: true,
          AntivirusSignatureVersion: '1.423.1578.0',
        }),
      },
      Firewall: {
        Available: true,
        RawJson: JSON.stringify([
          {
            Name: 'Perfil público',
            Enabled: true,
          },
          {
            Name: 'Perfil privado',
            Enabled: true,
          },
        ]),
      },
      BitLocker: {
        Available: true,
        RawJson: JSON.stringify([
          {
            Volume: 'C:',
            ProtectionStatus: 1,
          },
        ]),
      },
      Tpm: {
        Available: true,
        RawJson: JSON.stringify({
          TpmPresent: true,
          TpmReady: true,
        }),
      },
      SecureBoot: true,
      Uac: true,
      PendingReboot: false,
      RemoteDesktop: false,
    }),
  })
}

export function updateCommand(): DeviceCommand {
  return deviceCommand({
    commandType: 'WINDOWS_UPDATE_STATUS',
    resultJson: JSON.stringify({
      WindowsUpdateService: {
        Status: 'Running',
        QuerySucceeded: true,
      },
      PendingReboot: false,
      HistoryAvailable: true,
      UpdateHistoryJson: JSON.stringify([
        {
          Title: 'Actualización acumulativa para Windows 11 (KB5044285)',
          Date: '2026-10-02T03:00:00Z',
          ResultCode: 2,
          HResult: 0,
        },
      ]),
      AvailableUpdatesAvailable: true,
      AvailableUpdatesJson: JSON.stringify([
        {
          Title: 'Actualización acumulativa para Windows 11 (KB5044384)',
          KbArticleIds: ['5044384'],
          Severity: 'SecurityUpdate',
          IsDownloaded: true,
          RebootRequired: false,
          EulaAccepted: true,
        },
      ]),
      CollectedAtUtc: '2026-10-10T15:01:00Z',
    }),
  })
}

export function processCommand(): DeviceCommand {
  return deviceCommand({
    commandType: 'PROCESS_INVENTORY',
    resultJson: JSON.stringify([
      {
        ProcessId: 4,
        Name: 'System',
        WorkingSetBytes: 2621440,
        StartTimeUtc: '2026-10-01T06:00:00Z',
      },
      {
        ProcessId: 8992,
        Name: 'explorer.exe',
        WorkingSetBytes: 141004800,
        StartTimeUtc: '2026-10-10T14:02:11Z',
      },
    ]),
  })
}

export function serviceCommand(): DeviceCommand {
  return deviceCommand({
    commandType: 'SERVICE_INVENTORY',
    resultJson: JSON.stringify([
      {
        Name: 'WinDefend',
        DisplayName: 'Microsoft Defender Antivirus Service',
        ImagePath: 'C:\\Program Files\\Windows Defender\\MsMpEng.exe',
        StartType: 'Automatic',
        ServiceType: 'Win32OwnProcess',
      },
      {
        Name: 'Spooler',
        DisplayName: 'Print Spooler',
        ImagePath: 'C:\\Windows\\System32\\spoolsv.exe',
        StartType: 'Automatic',
        ServiceType: 'Win32OwnProcess',
      },
    ]),
  })
}

export function applicationCommand(): DeviceCommand {
  return deviceCommand({
    commandType: 'APP_INVENTORY',
    resultJson: JSON.stringify([
      {
        Name: 'Microsoft Edge',
        Version: '131.0.2903.98',
        Publisher: 'Microsoft Corporation',
        InstallLocation:
          'C:\\Program Files (x86)\\Microsoft\\Edge\\Application',
        UninstallString:
          'MsiExec.exe /X{B9F7D3B2-6F4A-4B4A-9C1A-8E1F2A3B4C5D}',
      },
      {
        Name: '7-Zip 24.09',
        Version: '24.09',
        Publisher: 'Igor Pavlov',
        InstallLocation: 'C:\\Program Files\\7-Zip',
        UninstallString:
          '"C:\\Program Files\\7-Zip\\Uninstall.exe"',
      },
    ]),
  })
}

export function pingCommand(): DeviceCommand {
  return deviceCommand({
    id: 'cmd-ping',
    commandType: 'PING',
    resultJson: JSON.stringify({
      ReceivedAtUtc: '2026-10-10T15:00:05Z',
      AppVersion: '1.0.0',
      Message: 'Agente disponible',
    }),
  })
}

export function failedCommand(): DeviceCommand {
  return deviceCommand({
    id: 'cmd-failed',
    commandType: 'SOFTWARE_INSTALL',
    status: 'Failed',
    resultJson: JSON.stringify({
      Action: 'SOFTWARE_INSTALL',
      Success: false,
      Message:
        'El equipo no cumple los requisitos mínimos de espacio.',
    }),
    errorCode: 'INSUFFICIENT_SPACE',
    errorMessage:
      'El equipo no cumple los requisitos mínimos de espacio.',
    completedAtUtc: new Date().toISOString(),
  })
}

export function mixedHistoryCommands(): DeviceCommand[] {
  const base = '2026-10-10T15:00:00Z'

  return [
    failedCommand(),
    pingCommand(),
    inventoryCommand(),
    deviceCommand({
      id: 'cmd-update',
      commandType: 'WINDOWS_UPDATE_STATUS',
      status: 'Executing',
      createdAtUtc: base,
      updatedAtUtc: base,
      sentAtUtc: null,
      deliveredAtUtc: null,
      startedAtUtc: null,
      completedAtUtc: null,
      resultJson: null,
    }),
  ]
}