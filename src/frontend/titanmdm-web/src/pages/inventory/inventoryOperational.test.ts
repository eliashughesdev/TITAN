import { describe, expect, it } from 'vitest'

import type { Delivery, ReturnRecord } from './inventoryApi'
import {
  deliveryReport,
  filterDeliveries,
  filterReturns,
  isToday,
  localDateKey,
  monthKeyLabel,
  overdueDays,
  toLocalDateKey,
} from './inventoryOperational'

// 10:00 en Santo Domingo (UTC-4), independiente de la zona de la maquina de pruebas.
const NOW = new Date('2026-11-04T14:00:00Z')

const delivery = (overrides: Partial<Delivery>): Delivery => ({
  id: crypto.randomUUID(),
  assetTag: 'TI-1',
  assetName: 'Laptop',
  assignedToName: 'Ana',
  deliveryDate: '2026-11-01T15:00:00.000000+00:00',
  returned: false,
  deleted: false,
  createdAt: '2026-11-01',
  ...overrides,
})

describe('inventoryOperational', () => {
  it('usa la fecha del negocio (RD), no la del navegador', () => {
    // 01:30 UTC del 5 = 21:30 del 4 en RD.
    expect(localDateKey(new Date('2026-11-05T01:30:00Z'))).toBe('2026-11-04')
    expect(localDateKey(new Date('2026-11-05T04:00:00Z'))).toBe('2026-11-05')
  })

  it('formatea claves yyyy-MM sin meses fijos', () => {
    expect(monthKeyLabel('2026-11')).toBe('Nov 26')
    expect(monthKeyLabel('2027-01')).toBe('Ene 27')
    expect(monthKeyLabel('sin-formato')).toBe('sin-formato')
  })

  it('normaliza los formatos reales de SQL Server y del legado MongoDB', () => {
    expect(toLocalDateKey('2026-11-04')).toBe('2026-11-04')
    // Formato real de la tabla deliveries (+00:00 y 6 decimales).
    expect(toLocalDateKey('2026-11-03T18:40:06.028520+00:00')).toBe('2026-11-03')
    // Entrega a las 22:30 RD queda en el dia correcto.
    expect(toLocalDateKey('2026-11-05T02:30:00.123456+00:00')).toBe('2026-11-04')
    // ISO sin zona (Mongo) = UTC.
    expect(toLocalDateKey('2026-11-05T02:30:00.597000')).toBe('2026-11-04')
    expect(toLocalDateKey('2026-11-05 02:30:00')).toBe('2026-11-04')
    expect(toLocalDateKey('no-fecha')).toBeNull()
    expect(isToday('2026-11-04T23:59:00-04:00', NOW)).toBe(true)
  })

  it('filtra entregas de hoy, pendientes y vencidas', () => {
    const rows = [
      delivery({ deliveryDate: '2026-11-04T12:10:00.000000+00:00' }),
      delivery({ deliveryDate: '2026-11-05T02:00:00.000000+00:00' }), // 22:00 RD del 4
      delivery({ deliveryDate: '2026-10-01T15:00:00+00:00', expectedReturn: '2026-11-01' }),
      delivery({ deliveryDate: '2026-10-01T15:00:00+00:00', returned: true }),
    ]

    expect(filterDeliveries(rows, 'today', NOW)).toHaveLength(2)
    expect(filterDeliveries(rows, 'pending', NOW)).toHaveLength(3)
    expect(filterDeliveries(rows, 'overdue', NOW)).toHaveLength(1)
    expect(overdueDays(rows[2], NOW)).toBe(3)
    expect(overdueDays(rows[3], NOW)).toBe(0)
  })

  it('filtra devoluciones del dia', () => {
    const returns = [
      { id: '1', returnDate: '2026-11-04T13:00:00.000000+00:00', createdAt: '' },
      { id: '2', returnDate: '2026-11-02T13:00:00.000000+00:00', createdAt: '' },
    ] as ReturnRecord[]

    expect(filterReturns(returns, 'today', NOW)).toHaveLength(1)
    expect(filterReturns(returns, 'all', NOW)).toHaveLength(2)
  })

  it('arma el reporte de entregas del dia con alcance explicito', () => {
    const report = deliveryReport([delivery({ deliveryDate: '2026-11-04T13:00:00Z' })], 'today', NOW)

    expect(report.scope).toBe('Entregas del 2026-11-04')
    expect(report.fileBase).toBe('TitanMDM-Entregas-Hoy')
    expect(report.rows[0][3]).toBe('Ana')
    expect(report.rows[0][5]).toBe('2026-11-04')
  })
})
