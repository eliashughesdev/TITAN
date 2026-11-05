import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { Asset, Dashboard } from './inventoryApi'
import {
  buildInventoryTables,
  exportInventoryExcel,
  exportInventoryPdf,
  type InventoryReportData,
} from './inventoryReportExport'

const dashboard: Dashboard = {
  totalAssets: 2,
  assignedAssets: 1,
  availableAssets: 1,
  handHeldAssets: 0,
  damagedAssets: 0,
  retiredAssets: 0,
  totalEmployees: 1,
  activeDeliveries: 1,
  returnedDeliveries: 0,
  pendingReceptions: 0,
  warrantiesExpiringSoon: 0,
  totalPurchaseCost: 100000,
  accumulatedDepreciation: 25000,
  bookValue: 75000,
  atRiskAssets: 0,
  totalBranches: 1,
  rotationRate: 50,
  byStatus: [{ name: 'asignado', count: 1 }],
  byCondition: [{ name: 'bueno', count: 2 }],
  byCategory: [{ name: 'Laptop', count: 2 }],
  byBranch: [{ name: 'CEDI', count: 2 }],
  byManufacturer: [],
  byDepartment: [],
  monthlyAssignments: [],
}

const asset: Asset = {
  id: 'a1',
  assetTag: 'TI-0001',
  name: 'Latitude 5440',
  status: 'asignado',
  condition: 'bueno',
  purchaseCost: 50000,
  deleted: false,
  createdAt: '2026-01-01',
}

const data: InventoryReportData = {
  dashboard,
  assets: [asset],
  depreciation: [
    {
      assetId: 'a1',
      assetTag: 'TI-0001',
      name: 'Latitude 5440',
      purchaseCost: 50000,
      depreciationYears: 4,
      yearsElapsed: 1,
      annualDepreciation: 12500,
      accumulatedDepreciation: 12500,
      bookValue: 37500,
    },
  ],
}

describe('inventoryReportExport', () => {
  const createObjectURL = vi.fn(() => 'blob:itam')

  beforeEach(() => {
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    createObjectURL.mockClear()
  })

  it('arma las tablas desde los datos reales del API', () => {
    const tables = buildInventoryTables(data)

    expect(tables.map(t => t.name)).toContain('Depreciación')
    expect(tables.find(t => t.name === 'Depreciación')?.rows[0]).toEqual([
      'TI-0001', 'Latitude 5440', 50000, 4, 1, 12500, 12500, 37500,
    ])
    expect(tables.find(t => t.name === 'Activos')?.rows).toHaveLength(1)
  })

  it('genera y descarga el Excel', async () => {
    await exportInventoryExcel(data)
    expect(createObjectURL).toHaveBeenCalledTimes(1)
  }, 60000)

  it('genera y descarga el PDF', async () => {
    await exportInventoryPdf(data)
    expect(createObjectURL).toHaveBeenCalledTimes(1)
  }, 60000)
})
