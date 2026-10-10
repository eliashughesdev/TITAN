import {
  describe,
  expect,
  it,
} from 'vitest'
import {
  render,
  screen,
} from '@testing-library/react'

import {
  CommandResultView,
} from './CommandResultView'

import {
  applicationCommand,
  failedCommand,
  inventoryCommand,
  pingCommand,
  processCommand,
  securityCommand,
  serviceCommand,
  updateCommand,
} from '../../test/fixtures/deviceCommands'

describe('CommandResultView', () => {
  it('muestra el inventario como tablas legibles', () => {
    render(
      <CommandResultView command={inventoryCommand()} />,
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
  })

  it('muestra seguridad como tarjetas legibles', () => {
    render(
      <CommandResultView command={securityCommand()} />,
    )

    expect(
      screen.getAllByText('Protegido'),
    ).not.toHaveLength(0)
    expect(
      screen.getByText(
        '2 perfiles',
      ),
    ).toBeInTheDocument()
  })

  it('muestra actualizaciones con tabla de disponibles', () => {
    render(
      <CommandResultView command={updateCommand()} />,
    )

    expect(
      screen.getAllByText(
        /KB5044384/,
      ),
    ).not.toHaveLength(0)
  })

  it('muestra procesos, servicios y aplicaciones en tablas', () => {
    render(
      <CommandResultView command={processCommand()} />,
    )
    expect(
      screen.getByText(
        'explorer.exe',
      ),
    ).toBeInTheDocument()

    render(
      <CommandResultView command={serviceCommand()} />,
    )
    expect(
      screen.getByText(
        'Print Spooler',
      ),
    ).toBeInTheDocument()

    render(
      <CommandResultView command={applicationCommand()} />,
    )
    expect(
      screen.getByText(
        'Microsoft Edge',
      ),
    ).toBeInTheDocument()
  })

  it('muestra comandos genéricos como campos legibles, no JSON crudo', () => {
    render(
      <CommandResultView command={pingCommand()} />,
    )

    expect(
      screen.getByText(
        'Agente disponible',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Received At Utc',
      ),
    ).toBeInTheDocument()
  })

  it('muestra el error de un comando fallido', () => {
    render(
      <CommandResultView command={failedCommand()} />,
    )

    expect(
      screen.getAllByText(
        'El equipo no cumple los requisitos mínimos de espacio.',
      ),
    ).not.toHaveLength(0)
  })

  it('mantiene el JSON técnico únicamente bajo detalle colapsado', () => {
    render(
      <CommandResultView command={inventoryCommand()} />,
    )

    const details =
      document.querySelector(
        '.windows-command-details pre.windows-control-json',
      )

    expect(details).not.toBeNull()
    expect(
      (details as HTMLElement).textContent,
    ).toContain('PC-CONTABILIDAD-07')
  })
})