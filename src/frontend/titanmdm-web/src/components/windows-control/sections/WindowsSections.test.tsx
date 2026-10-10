import {
  Laptop,
} from 'lucide-react'
import {
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import {
  render,
  screen,
} from '@testing-library/react'

import type {
  DeviceDetails,
} from '../../../types/device'

import {
  normalizeApplications,
  normalizeProcesses,
  normalizeServices,
  parseSecurity,
  parseUpdate,
} from '../windowsControl.utils'

import {
  applicationCommand,
  inventoryCommand,
  mixedHistoryCommands,
  processCommand,
  securityCommand,
  serviceCommand,
  updateCommand,
} from '../../../test/fixtures/deviceCommands'

import {
  WindowsInventorySection,
} from './WindowsInventorySection'

import {
  WindowsCommandHistorySection,
} from './WindowsCommandHistorySection'

import {
  WindowsSecuritySection,
} from './WindowsSecuritySection'

import {
  WindowsUpdateSection,
} from './WindowsUpdateSection'

import {
  WindowsProcessesSection,
} from './WindowsProcessesSection'

import {
  WindowsServicesSection,
} from './WindowsServicesSection'

import {
  WindowsSoftwareSection,
} from './WindowsSoftwareSection'

const noop =
  vi.fn(async () => {})

const setState =
  () => undefined

describe('WindowsInventorySection', () => {
  it('regresión: con un DEVICE_INVENTORY real el tab NO queda en blanco', () => {
    const { container } =
      render(
        <WindowsInventorySection
          commands={[inventoryCommand()]}
          sendCommand={noop}
        />,
      )

    expect(
      screen.getByText(
        'PC-CONTABILIDAD-07',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getAllByText('C:'),
    ).not.toHaveLength(0)

    expect(
      screen.getAllByText(
        /10.20.31.45/,
      ),
    ).not.toHaveLength(0)

    expect(
      screen.getByText(
        'Sin dirección',
      ),
    ).toBeInTheDocument()

    expect(
      container.querySelector(
        '.windows-command-details',
      ),
    ).not.toBeNull()
  })

  it('muestra un estado vacío cuando aún no hay datos', () => {
    render(
      <WindowsInventorySection
        commands={[]}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getByText(
        /Todavía no existe información/,
      ),
    ).toBeInTheDocument()
  })
})

describe('WindowsCommandHistorySection', () => {
  it('regresión: NO muestra JSON crudo como vista principal', () => {
    const { container } =
      render(
        <WindowsCommandHistorySection
          commands={mixedHistoryCommands()}
        />,
      )

    expect(
      screen.getByText(
        'DEVICE_INVENTORY',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText(
        'PC-CONTABILIDAD-07',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getAllByText(
        'Agente disponible',
      ),
    ).not.toHaveLength(0)

    expect(
      screen.getAllByText(
        'El equipo no cumple los requisitos mínimos de espacio.',
      ),
    ).not.toHaveLength(0)

    const primaryPre =
      container.querySelector(
        '.windows-command-item > pre',
      )

    expect(
      primaryPre,
    ).toBeNull()
  })
})

describe('Persistencias de las demás pestañas', () => {
  it('Seguridad: no falla y presenta tarjetas', () => {
    render(
      <WindowsSecuritySection
        security={parseSecurity(securityCommand())}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getAllByText('Protegido'),
    ).not.toHaveLength(0)
  })

  it('Actualizaciones: no falla y presenta resumen', () => {
    render(
      <WindowsUpdateSection
        update={parseUpdate(updateCommand())}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getAllByText(
        /KB5044384/,
      ),
    ).not.toHaveLength(0)
  })

  it('Procesos: no falla', () => {
    render(
      <WindowsProcessesSection
        processes={normalizeProcesses(processCommand())}
        filter=""
        setFilter={setState as never}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getByText(
        'explorer.exe',
      ),
    ).toBeInTheDocument()
  })

  it('Servicios: no falla', () => {
    render(
      <WindowsServicesSection
        services={normalizeServices(serviceCommand())}
        filter=""
        setFilter={setState as never}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getByText(
        'Print Spooler',
      ),
    ).toBeInTheDocument()
  })

  it('Software: no falla y lista aplicaciones', () => {
    render(
      <WindowsSoftwareSection
        applications={normalizeApplications(applicationCommand())}
        filter=""
        setFilter={setState as never}
        packagePath=""
        setPackagePath={setState as never}
        packageSha256=""
        setPackageSha256={setState as never}
        packageArguments=""
        setPackageArguments={setState as never}
        uninstallTarget={{
          name: '',
          productCode: '',
          executable: '',
          arguments: '',
        }}
        setUninstallTarget={setState as never}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getByText(
        'Microsoft Edge',
      ),
    ).toBeInTheDocument()
  })
})

describe('WindowsOverviewSection (tab Resumen)', () => {
  it('no falla con un endpoint básico', async () => {
    const { WindowsOverviewSection } =
      await import('./WindowsOverviewSection')
    const device =
      {
        id: 'dev-windows-001',
        deviceName: 'PC-CONTABILIDAD-07',
        platform: 'Windows',
        operatingSystem: 'Windows 11 Pro',
        operatingSystemVersion: '23H2',
        agentVersion: '1.0.0',
        ipAddress: '10.20.31.45',
        lastSeenAtUtc: '2026-10-10T15:00:00Z',
        serialNumber: 'DEL5540X7Y2',
        macAddress: 'D4:3A:2C:11:AB:99',
        assignedUser: 'lrojas',
        department: 'Contabilidad',
      } as unknown as DeviceDetails

    render(
      <WindowsOverviewSection
        device={device}
        definitions={[
          {
            type: 'DEVICE_INVENTORY',
            label: 'Inventario',
            description:
              'Recopila inventario completo',
            icon: <Laptop size={16} />,
          },
        ]}
        sendingCommand={null}
        sendCommand={noop}
      />,
    )

    expect(
      screen.getByText(
        'PC-CONTABILIDAD-07',
      ),
    ).toBeInTheDocument()
  })
})