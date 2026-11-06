import { describe, expect, it } from 'vitest'
import { availableRp4Printers, isRp4Printer, printerLabel } from './deliveryPrinter'
import type { Asset, CatalogItem } from './inventoryApi'

const asset = (overrides: Partial<Asset>): Asset => ({
  id: 'id',
  assetTag: 'TAG',
  name: 'Equipo',
  status: 'disponible',
  condition: 'bueno',
  deleted: false,
  createdAt: '2026-11-06T00:00:00Z',
  ...overrides,
})

const categories: CatalogItem[] = [
  { id: 'cat-rp4', name: 'RP4D' },
  { id: 'cat-hh', name: 'Hand Held' },
]

describe('deliveryPrinter', () => {
  it('reconoce la RP4 por categoría, nombre o modelo', () => {
    expect(isRp4Printer(asset({}), 'RP4D')).toBe(true)
    expect(isRp4Printer(asset({ name: 'Honeywell rp4 #3' }))).toBe(true)
    expect(isRp4Printer(asset({ hardwareModel: 'RP4' }))).toBe(true)
    expect(isRp4Printer(asset({ name: 'Zebra ZQ520' }), 'Impresora')).toBe(false)
  })

  it('solo ofrece RP4 libres, distintas de la Handheld y ordenadas', () => {
    const list = availableRp4Printers(
      [
        asset({ id: 'p2', assetTag: 'RP4-002', categoryId: 'cat-rp4' }),
        asset({ id: 'p1', assetTag: 'RP4-001', categoryId: 'cat-rp4' }),
        asset({ id: 'p3', assetTag: 'RP4-003', categoryId: 'cat-rp4', status: 'asignado' }),
        asset({ id: 'p4', assetTag: 'RP4-004', categoryId: 'cat-rp4', deleted: true }),
        asset({ id: 'hh', assetTag: 'HH-001', categoryId: 'cat-hh' }),
      ],
      categories,
      'p2',
    )

    expect(list.map(item => item.id)).toEqual(['p1'])
  })

  it('etiqueta la impresora con etiqueta, nombre y serie', () => {
    expect(printerLabel(asset({ assetTag: 'RP4-001', name: 'RP4D', serialNumber: 'SN1' }))).toBe('RP4-001 · RP4D · S/N SN1')
    expect(printerLabel(asset({ assetTag: 'RP4-001', name: 'RP4D' }))).toBe('RP4-001 · RP4D')
  })
})
