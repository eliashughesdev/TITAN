import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { HelpdeskReportExportActions } from './HelpdeskReportExportActions'

// PNG 1x1 valido: ExcelJS y jsPDF necesitan una imagen real.
const PIXEL =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='

function fakeContext(): CanvasRenderingContext2D {
  return new Proxy(
    {},
    {
      get: (_target, prop) =>
        prop === 'measureText' ? () => ({ width: 10 }) : () => undefined,
      set: () => true,
    },
  ) as CanvasRenderingContext2D
}

const metric = { evaluated: 10, met: 8, breached: 2, compliancePercent: 80 }

const summary = {
  total: 10,
  resolved: 6,
  unresolved: 4,
  currentBacklog: 4,
  unassigned: 1,
  pendingUser: 1,
  overdueFirstResponse: 0,
  overdueResolution: 1,
  averageFirstResponseHours: 1.5,
  medianFirstResponseHours: 1,
  averageResolutionHours: 8,
  medianResolutionHours: 6,
  firstResponseSla: metric,
  resolutionSla: metric,
  byStatus: [{ label: 'open', count: 4 }, { label: 'resolved', count: 6 }],
  byPriority: [{ label: 'high', count: 3 }],
  byCategory: [{ label: 'hardware', count: 5 }],
  bySource: [{ label: 'portal', count: 10 }],
  bySite: [{ label: 'CEDI', count: 10, resolved: 6 }],
  byAgent: [
    { label: 'Tecnico', count: 10, resolved: 6, active: 4, resolutionRate: 60, slaBreached: 1 },
  ],
  backlogAging: [{ label: '0-2 dias', count: 4 }],
  daily: [{ date: '2026-11-01', created: 5, resolved: 3 }],
  automation: {
    autoAssigned: 1,
    autoHandovers: 0,
    classifications: 2,
    classificationReviews: 0,
    escalations: 0,
    reminders: 1,
    overdueAlerts: 0,
    reopened: 0,
  },
  generatedAtUtc: '2026-11-04T00:00:00Z',
}

describe('HelpdeskReportExportActions', () => {
  const createObjectURL = vi.fn(() => 'blob:titan')

  beforeEach(() => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(
      (() => fakeContext()) as unknown as HTMLCanvasElement['getContext'],
    )
    vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue(PIXEL)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    createObjectURL.mockClear()
  })

  it('genera el Excel real y lo descarga', async () => {
    render(<HelpdeskReportExportActions summary={summary} from="2026-11-01" to="2026-11-04" />)

    fireEvent.click(screen.getByRole('button', { name: /Excel/i }))

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1), { timeout: 45000 })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  }, 60000)

  it('genera el PDF real y lo descarga', async () => {
    render(<HelpdeskReportExportActions summary={summary} from="2026-11-01" to="2026-11-04" />)

    fireEvent.click(screen.getByRole('button', { name: /PDF/i }))

    await waitFor(
      () => expect(screen.getByRole('button', { name: /PDF/i })).not.toBeDisabled(),
      { timeout: 45000 },
    )
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  }, 60000)

  it('deshabilita la exportación con rango inválido', () => {
    render(<HelpdeskReportExportActions summary={summary} from="2026-11-05" to="2026-11-01" />)

    expect(screen.getByRole('button', { name: /Excel/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /PDF/i })).toBeDisabled()
  })
})
