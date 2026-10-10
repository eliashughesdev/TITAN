import {
  describe,
  expect,
  it,
} from 'vitest'

import {
  parseInventory,
  resultSummary,
  scalarFields,
} from './windowsControl.utils'

import {
  failedCommand,
  inventoryCommand,
  pingCommand,
} from '../../test/fixtures/deviceCommands'

describe('parseInventory', () => {
  it('normaliza discos y adaptadores del snapshot real del agente (PascalCase)', () => {
    const inventory =
      parseInventory(
        inventoryCommand(),
      )

    expect(
      inventory.available,
    ).toBe(true)

    expect(
      inventory.disks.length,
    ).toBe(2)

    expect(
      inventory.disks[0].name,
    ).toBe('C:')

    expect(
      inventory.disks[0].totalBytes,
    ).toBe(511101108224)

    expect(
      inventory.disks[1].name,
    ).toBe('D:')

    expect(
      inventory.network.length,
    ).toBe(2)

    expect(
      inventory.network[0].name,
    ).toBe('Ethernet 2')

    expect(
      inventory.network[0].ipAddresses,
    ).toEqual([
      '10.20.31.45',
      'fe80::3d2a:2cff:fe11:ab99',
    ])

    expect(
      inventory.network[1].ipAddresses,
    ).toEqual([])

    expect(
      inventory.device?.computerName,
    ).toBe('PC-CONTABILIDAD-07')

    expect(
      inventory.device?.serialNumber,
    ).toBe('DEL5540X7Y2')
  })

  it('no produce una tabla de red rota cuando no hay adaptadores', () => {
    const inventory =
      parseInventory(
        inventoryCommand(),
      )

    expect(
      Array.isArray(
        inventory.network,
      ),
    ).toBe(true)
  })

  it('devuelve vacío cuando no hay resultado', () => {
    const inventory =
      parseInventory(null)

    expect(
      inventory.available,
    ).toBe(false)

    expect(
      inventory.disks,
    ).toEqual([])

    expect(
      inventory.network,
    ).toEqual([])
  })
})

describe('scalarFields', () => {
  it('excluye objetos y arreglos anidados', () => {
    const fields =
      scalarFields({
        Message:
          'Agente disponible',
        Accepted: true,
        Attempts: 3,
        Extra: {
          nested: 'x',
        },
        List: [1, 2],
      })

    expect(
      fields.map(
        field =>
          field.label,
      ),
    ).toEqual([
      'Message',
      'Accepted',
      'Attempts',
    ])
  })

  it('convierte valores nulos a N/D', () => {
    const fields =
      scalarFields({
        Optional: null,
        Empty: '',
      })

    expect(
      fields.every(
        field =>
          field.value ===
          'N/D',
      ),
    ).toBe(true)
  })
})

describe('resultSummary', () => {
  it('extrae el mensaje de un comando correcto', () => {
    expect(
      resultSummary(
        pingCommand(),
      ),
    ).toBe('Agente disponible')
  })

  it('extrae el error de un comando fallido', () => {
    expect(
      resultSummary(
        failedCommand(),
      ),
    ).toBe(
      'El equipo no cumple los requisitos mínimos de espacio.',
    )
  })

  it('devuelve null cuando no hay resultado', () => {
    expect(
      resultSummary(null),
    ).toBeNull()
  })
})