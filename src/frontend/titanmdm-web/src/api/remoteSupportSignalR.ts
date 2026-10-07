import * as signalR
  from '@microsoft/signalr'

import {
  tokenStorage,
} from '../auth/tokenStorage'

/*
 * ============================================================
 * FRAME
 * ============================================================
 */

export interface RemoteFrame {
  sessionId: string

  sequence: number

  width: number

  height: number

  mimeType: string

  base64Data: string

  capturedAtUtc: string

  displayIndex: number

  displayCount: number

  displayLabel: string
}

/*
 * ============================================================
 * SESSION
 * ============================================================
 */

export interface RemoteSessionChanged {
  sessionId: string

  status: string

  connectedAtUtc?:
    string | null
}

/*
 * ============================================================
 * MONITORS
 * ============================================================
 */

export interface RemoteMonitorInfo {
  index: number

  deviceName: string

  width: number

  height: number

  isPrimary: boolean

  label: string
}

export interface RemoteMonitorState {
  sessionId: string

  selectedMonitorIndex: number

  monitors:
    RemoteMonitorInfo[]
}

/*
 * ============================================================
 * MULTI TECHNICIAN
 * ============================================================
 */

export interface RemoteParticipantState {
  sessionId: string

  userId: string

  displayName: string

  connected: boolean
}

export interface RemoteControlState {
  sessionId: string

  hasController: boolean

  userId?: string

  displayName?: string

  acquiredAtUtc?: string

  expiresAtUtc?: string
}

export interface RemoteHostState {
  sessionId: string

  connected: boolean
}

/*
 * ============================================================
 * HANDLERS
 * ============================================================
 */

export interface RemoteSupportSignalRHandlers {
  onFrame?: (
    frame:
      RemoteFrame,
  ) => void

  onSessionChanged?: (
    update:
      RemoteSessionChanged,
  ) => void

  onMonitorState?: (
    state:
      RemoteMonitorState,
  ) => void

  onParticipantStateChanged?: (
    state:
      RemoteParticipantState,
  ) => void

  onControlStateChanged?: (
    state:
      RemoteControlState,
  ) => void

  onRemoteHostStateChanged?: (
    state:
      RemoteHostState,
  ) => void

  onReconnecting?: (
    error?: Error,
  ) => void

  onReconnected?: (
    connectionId?: string,
  ) => void

  onClosed?: (
    error?: Error,
  ) => void
}

/*
 * ============================================================
 * ERROR
 * ============================================================
 */

function extractSignalRError(
  error: unknown,
): string {
  if (
    error instanceof Error
  ) {
    return error.message
  }

  if (
    typeof error ===
    'string'
  ) {
    return error
  }

  try {
    return JSON.stringify(
      error,
    )
  } catch {
    return (
      'Error SignalR desconocido.'
    )
  }
}

/*
 * ============================================================
 * CLIENT
 * ============================================================
 */

export class RemoteSupportSignalRClient {
  private connection:
    signalR.HubConnection | null =
      null

  /*
   * ==========================================================
   * STATE
   * ==========================================================
   */

  public get isConnected():
    boolean {
    return (
      this.connection?.state ===
      signalR
        .HubConnectionState
        .Connected
    )
  }

  public get state():
    signalR.HubConnectionState |
    null {
    return (
      this.connection?.state ??
      null
    )
  }

  /*
   * ==========================================================
   * CONNECT
   * ==========================================================
   */

  public async connect(
    handlers:
      RemoteSupportSignalRHandlers = {},
  ): Promise<void> {
    if (
      this.connection
    ) {
      if (
        this.connection.state ===
          signalR
            .HubConnectionState
            .Connected
        ||
        this.connection.state ===
          signalR
            .HubConnectionState
            .Connecting
        ||
        this.connection.state ===
          signalR
            .HubConnectionState
            .Reconnecting
      ) {
        return
      }
    }

    const connection =
      new signalR
        .HubConnectionBuilder()
        .withUrl(
          '/hubs/remote-support',
          {
            accessTokenFactory:
              () =>
                tokenStorage
                  .getAccessToken()
                ??
                '',
          },
        )
        .withAutomaticReconnect(
          [
            0,
            2000,
            5000,
            10000,
            15000,
          ],
        )
        .configureLogging(
          signalR
            .LogLevel
            .Warning,
        )
        .build()

    this.connection =
      connection

    /*
     * ========================================================
     * SERVER EVENTS
     * ========================================================
     */

    connection.on(
      'RemoteFrame',
      (
        frame:
          RemoteFrame,
      ) => {
        handlers
          .onFrame
          ?.(
            frame,
          )
      },
    )

    connection.on(
      'RemoteSessionUpdated',
      (
        update:
          RemoteSessionChanged,
      ) => {
        handlers
          .onSessionChanged
          ?.(
            update,
          )
      },
    )

    connection.on(
      'RemoteMonitorState',
      (
        state:
          RemoteMonitorState,
      ) => {
        handlers
          .onMonitorState
          ?.(
            state,
          )
      },
    )

    connection.on(
      'ParticipantStateChanged',
      (
        state:
          RemoteParticipantState,
      ) => {
        handlers
          .onParticipantStateChanged
          ?.(
            state,
          )
      },
    )

    connection.on(
      'RemoteControlState',
      (
        state:
          RemoteControlState,
      ) => {
        handlers
          .onControlStateChanged
          ?.(
            state,
          )
      },
    )

    connection.on(
      'RemoteHostStateChanged',
      (
        state:
          RemoteHostState,
      ) => {
        handlers
          .onRemoteHostStateChanged
          ?.(
            state,
          )
      },
    )

    /*
     * ========================================================
     * CONNECTION EVENTS
     * ========================================================
     */

    connection
      .onreconnecting(
        error => {
          handlers
            .onReconnecting
            ?.(
              error ??
              undefined,
            )
        },
      )

    connection
      .onreconnected(
        connectionId => {
          handlers
            .onReconnected
            ?.(
              connectionId ??
              undefined,
            )
        },
      )

    connection
      .onclose(
        error => {
          handlers
            .onClosed
            ?.(
              error ??
              undefined,
            )
        },
      )

    /*
     * ========================================================
     * START
     * ========================================================
     */

    try {
      await connection
        .start()
    } catch (
      error
    ) {
      const message =
        extractSignalRError(
          error,
        )

      this.connection =
        null

      throw new Error(
        `No fue posible conectar SignalR: ${message}`,
        {
          cause:
            error,
        },
      )
    }
  }

  /*
   * ==========================================================
   * SESSION
   * ==========================================================
   */

  public async joinSession(
    sessionId: string,
  ): Promise<void> {
    if (!sessionId) {
      throw new Error(
        'JoinSession requiere SessionId.',
      )
    }

    await this
      .invoke(
        'JoinSession',
        sessionId,
      )
  }

  public async leaveSession(
    sessionId: string,
  ): Promise<void> {
    if (!sessionId) {
      return
    }

    await this
      .invoke(
        'LeaveSession',
        sessionId,
      )
  }

  /*
   * ==========================================================
   * CONTROL
   * ==========================================================
   */

  public async acquireControl(
    sessionId: string,
  ): Promise<RemoteControlState> {
    return this
      .invoke<RemoteControlState>(
        'AcquireControl',
        sessionId,
      )
  }

  public async renewControl(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'RenewControl',
        sessionId,
      )
  }

  public async releaseControl(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'ReleaseControl',
        sessionId,
      )
  }

  public async requestControlState(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'RequestControlState',
        sessionId,
      )
  }

  /*
   * ==========================================================
   * MONITORS
   * ==========================================================
   */

  public async selectMonitor(
    sessionId: string,
    monitorIndex: number,
  ): Promise<void> {
    await this
      .invoke(
        'SelectMonitor',
        sessionId,
        monitorIndex,
      )
  }

  public async nextMonitor(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'NextMonitor',
        sessionId,
      )
  }

  public async previousMonitor(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'PreviousMonitor',
        sessionId,
      )
  }

  public async requestMonitorState(
    sessionId: string,
  ): Promise<void> {
    await this
      .invoke(
        'RequestMonitorState',
        sessionId,
      )
  }

  /*
   * ==========================================================
   * POINTER
   * ==========================================================
   */

  public async pointerMove(
    sessionId: string,
    x: number,
    y: number,
  ): Promise<void> {
    await this
      .invoke(
        'PointerMove',
        sessionId,
        x,
        y,
      )
  }

  public async pointerButton(
    sessionId: string,
    action:
      | 'left-down'
      | 'left-up'
      | 'right-down'
      | 'right-up',
  ): Promise<void> {
    await this
      .invoke(
        'PointerButton',
        sessionId,
        action,
      )
  }

  public async pointerWheel(
    sessionId: string,
    delta: number,
  ): Promise<void> {
    await this
      .invoke(
        'PointerWheel',
        sessionId,
        delta,
      )
  }

  /*
   * ==========================================================
   * KEYBOARD
   * ==========================================================
   */

  public async keyboard(
    sessionId: string,
    virtualKey: number,
    keyDown: boolean,
  ): Promise<void> {
    await this
      .invoke(
        'Keyboard',
        sessionId,
        virtualKey,
        keyDown,
      )
  }

  /*
   * ==========================================================
   * DISCONNECT
   * ==========================================================
   */

  public async disconnect():
    Promise<void> {
    const connection =
      this.connection

    this.connection =
      null

    if (!connection) {
      return
    }

    try {
      if (
        connection.state !==
        signalR
          .HubConnectionState
          .Disconnected
      ) {
        await connection
          .stop()
      }
    } catch (
      error
    ) {
      console.warn(
        '[TitanMDM Remote] Error cerrando SignalR.',
        error,
      )
    }
  }

  /*
   * ==========================================================
   * INTERNAL INVOKE
   * ==========================================================
   */

  private async invoke<T = void>(
    methodName: string,
    ...args: unknown[]
  ): Promise<T> {
    const connection =
      this.requireConnection()

    try {
      return await connection
        .invoke<T>(
          methodName,
          ...args,
        )
    } catch (
      error
    ) {
      const message =
        extractSignalRError(
          error,
        )

      throw new Error(
        `${methodName} falló: ${message}`,
        {
          cause:
            error,
        },
      )
    }
  }

  private requireConnection():
    signalR.HubConnection {
    if (!this.connection) {
      throw new Error(
        'El cliente SignalR no está inicializado.',
      )
    }

    if (
      this.connection.state !==
      signalR
        .HubConnectionState
        .Connected
    ) {
      throw new Error(
        `SignalR no está conectado. Estado: ${this.connection.state}.`,
      )
    }

    return this.connection
  }
}