import apiClient from './apiClient'

export type RemoteSessionStatus =
  | 'Requested'
  | 'Connecting'
  | 'Connected'
  | 'Disconnecting'
  | 'Completed'
  | 'Failed'
  | 'Expired'
  | 'Cancelled'

export interface RemoteSessionEvent {
  id: string

  eventType: string

  description: string

  userId?:
    string | null

  metadataJson?:
    string | null

  occurredAtUtc: string
}

export interface RemoteSessionParticipant {
  id: string

  userId: string

  displayName: string

  canControl: boolean

  isConnected: boolean

  joinedAtUtc: string

  connectedAtUtc?:
    string | null

  disconnectedAtUtc?:
    string | null
}

export interface RemoteControlLease {
  id?: string

  userId: string

  displayName: string

  acquiredAtUtc: string

  expiresAtUtc: string
}

export interface RemoteSession {
  id: string

  organizationId?: string

  deviceId: string

  requestedByUserId?: string

  technicianName: string

  reason: string

  status:
    RemoteSessionStatus

  allowKeyboard:
    boolean

  allowMouse:
    boolean

  allowClipboard:
    boolean

  allowFileTransfer:
    boolean

  requestedAtUtc: string

  expiresAtUtc: string

  connectedAtUtc?:
    string | null

  disconnectedAtUtc?:
    string | null

  failureReason?:
    string | null

  terminationReason?:
    string | null

  terminatedBy?:
    string | null

  participantCount?:
    number

  connectedParticipants?:
    number

  participants?:
    RemoteSessionParticipant[]

  controlLease?:
    RemoteControlLease | null

  events?:
    RemoteSessionEvent[]

  joinedExisting?:
    boolean
}

export interface CreateRemoteSessionRequest {
  deviceId: string

  reason: string

  allowKeyboard: boolean

  allowMouse: boolean

  allowClipboard: boolean

  allowFileTransfer: boolean

  maximumDurationMinutes: number
}

export interface RemoteControlState {
  hasController: boolean

  ownedByCurrentUser?:
    boolean

  userId?:
    string

  displayName?:
    string

  acquiredAtUtc?:
    string

  expiresAtUtc?:
    string
}

export async function listRemoteSessions(
  take = 100,
): Promise<RemoteSession[]> {
  const response =
    await apiClient
      .get<RemoteSession[]>(
        '/remote-sessions',
        {
          params: {
            take,
          },
        },
      )

  return response.data
}

export async function getRemoteSession(
  sessionId: string,
): Promise<RemoteSession> {
  const response =
    await apiClient
      .get<RemoteSession>(
        `/remote-sessions/${sessionId}`,
      )

  return response.data
}

export async function createRemoteSession(
  request:
    CreateRemoteSessionRequest,
): Promise<RemoteSession> {
  const response =
    await apiClient
      .post<RemoteSession>(
        '/remote-sessions',
        request,
      )

  return response.data
}

export async function getRemoteParticipants(
  sessionId: string,
): Promise<
  RemoteSessionParticipant[]
> {
  const response =
    await apiClient
      .get<
        RemoteSessionParticipant[]
      >(
        `/remote-sessions/${sessionId}/participants`,
      )

  return response.data
}

export async function getRemoteControlState(
  sessionId: string,
): Promise<RemoteControlState> {
  const response =
    await apiClient
      .get<RemoteControlState>(
        `/remote-sessions/${sessionId}/control`,
      )

  return response.data
}

export async function acquireRemoteControl(
  sessionId: string,
): Promise<RemoteControlState> {
  const response =
    await apiClient
      .post<RemoteControlState>(
        `/remote-sessions/${sessionId}/control/acquire`,
        {},
      )

  return response.data
}

export async function renewRemoteControl(
  sessionId: string,
): Promise<RemoteControlState> {
  const response =
    await apiClient
      .post<RemoteControlState>(
        `/remote-sessions/${sessionId}/control/renew`,
        {},
      )

  return response.data
}

export async function releaseRemoteControl(
  sessionId: string,
): Promise<{
  released: boolean
}> {
  const response =
    await apiClient
      .post<{
        released: boolean
      }>(
        `/remote-sessions/${sessionId}/control/release`,
        {},
      )

  return response.data
}

export async function terminateRemoteSession(
  sessionId: string,
  reason =
    'Sesión finalizada por el técnico.',
): Promise<RemoteSession> {
  const response =
    await apiClient
      .post<RemoteSession>(
        `/remote-sessions/${sessionId}/terminate`,
        {
          reason,
        },
      )

  return response.data
}