import apiClient
  from './apiClient'

export interface RoutingHealth {
  openTickets: number
  unassignedTickets: number
  pendingUserTickets: number
  autoAssignedLast24Hours: number
  routingWaitingLast24Hours: number
  slaEscalatedLast24Hours: number
  averageAutoAssignMinutesLast24Hours: number
  activeTeams: number
  activeMembers: number
  membersOnDutyNow: number
}

export interface RoutingCandidate {
  teamId: string
  teamName: string

  userId: string
  technicianName: string
  email?: string | null

  isAvailable: boolean
  acceptsAutomaticAssignments: boolean

  capacity: number
  openTickets: number

  hasSchedule: boolean
  onDutyNow: boolean

  decision: string
  reason: string
}

export interface RoutingDiagnostic {
  ticketId: string
  ticketNumber: string
  status: string
  category: string

  subject?: string | null
  requesterEmail?: string | null

  requesterUserId?: string | null
  siteId?: string | null
  siteLocationId?: string | null
  requestedTeamId?: string | null
  assigneeUserId?: string | null

  alreadyAssigned: boolean
  pendingUser: boolean

  engineReason: string

  selectedCandidate?:
    RoutingCandidate | null

  candidates:
    RoutingCandidate[]
}

export interface RoutingQueueItem {
  ticketId: string
  ticketNumber: string
  category: string
  status: string
  outcome: string
  reason: string
}

export interface RoutingQueueResult {
  considered: number
  assigned: number
  skipped: number
  failed: number
  dryRun: boolean

  items:
    RoutingQueueItem[]
}

export interface RoutingRetryResult {
  assigned: boolean

  before:
    RoutingDiagnostic | null

  after:
    RoutingDiagnostic | null
}

export async function getRoutingHealth():
  Promise<RoutingHealth> {
  const {
    data,
  } =
    await apiClient
      .get<RoutingHealth>(
        '/helpdesk/routing/health',
      )

  return data
}

export async function getRoutingDiagnostic(
  ticketId: string,
):
  Promise<RoutingDiagnostic> {
  const {
    data,
  } =
    await apiClient
      .get<RoutingDiagnostic>(
        `/helpdesk/routing/tickets/${ticketId}/diagnostic`,
      )

  return data
}

export async function retryRoutingTicket(
  ticketId: string,
):
  Promise<RoutingRetryResult> {
  const {
    data,
  } =
    await apiClient
      .post<RoutingRetryResult>(
        `/helpdesk/routing/tickets/${ticketId}/retry`,
      )

  return data
}

export async function retryOpenRouting(
  maxTickets:
    number = 50,

  dryRun:
    boolean = true,
):
  Promise<RoutingQueueResult> {
  const {
    data,
  } =
    await apiClient
      .post<RoutingQueueResult>(
        '/helpdesk/routing/retry-open',
        {
          maxTickets,
          dryRun,
        },
      )

  return data
}