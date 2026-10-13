import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  getRoutingHealth,
  getRoutingWorker,
  retryOpenRouting,
} from '../../api/helpdeskRoutingApi'

import {
  HelpdeskRoutingOperationsPanel,
} from './HelpdeskRoutingOperationsPanel'

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: () => true,
  }),
}))

vi.mock('../../api/helpdeskRoutingApi', () => ({
  getRoutingDiagnostic: vi.fn(),
  getRoutingHealth: vi.fn(),
  getRoutingWorker: vi.fn(),
  retryOpenRouting: vi.fn(),
  retryRoutingTicket: vi.fn(),
}))

describe('HelpdeskRoutingOperationsPanel', () => {
  beforeEach(() => {
    vi.mocked(getRoutingHealth).mockResolvedValue({
      openTickets: 4,
      unassignedTickets: 2,
      pendingUserTickets: 1,
      autoAssignedLast24Hours: 3,
      routingWaitingLast24Hours: 1,
      slaEscalatedLast24Hours: 0,
      averageAutoAssignMinutesLast24Hours: 1.5,
      activeTeams: 2,
      activeMembers: 5,
      membersOnDutyNow: 4,
      automaticRetriesPending: 2,
      firstResponseSlaOverdue: 1,
      resolutionSlaOverdue: 0,
    })
    vi.mocked(getRoutingWorker).mockResolvedValue({
      running: true,
      startedAtUtc: '2026-10-13T10:00:00Z',
      lastCycleStartedAtUtc: '2026-10-13T10:01:00Z',
      lastCycleCompletedAtUtc: '2026-10-13T10:01:01Z',
      completedCycles: 4,
      errors: 0,
      lastSelectedTickets: 2,
      lastError: null,
      aiAvailable: false,
    })
    vi.mocked(retryOpenRouting).mockResolvedValue({
      considered: 2,
      assigned: 0,
      skipped: 2,
      failed: 0,
      dryRun: true,
      items: [],
    })
  })

  it('shows worker health and requires simulation before queue processing', async () => {
    render(<HelpdeskRoutingOperationsPanel />)

    expect(
      await screen.findByText('Worker routing'),
    ).toBeInTheDocument()
    expect(screen.getByText('Activo')).toBeInTheDocument()
    expect(screen.getByText('Deshabilitada')).toBeInTheDocument()

    const process = screen.getByRole('button', {
      name: 'Procesar cola',
    })
    expect(process).toBeDisabled()

    fireEvent.click(screen.getByRole('button', {
      name: 'Simular cola',
    }))

    await waitFor(() =>
      expect(retryOpenRouting).toHaveBeenCalledWith(50, true),
    )
    await waitFor(() =>
      expect(process).not.toBeDisabled(),
    )
  })
})
