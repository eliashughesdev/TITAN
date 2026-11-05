import * as signalR
  from '@microsoft/signalr'
import {
  MessagePackHubProtocol,
} from '@microsoft/signalr-protocol-msgpack'

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

  data: Uint8Array

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


export type RemoteDesktopKind =
  | 'Default'
  | 'Winlogon'
  | 'ScreenSaver'
  | 'Other'
  | 'AccessDenied'
  | 'Unavailable'
  | 'Unknown'

export interface RemoteDesktopState {
  sessionId: string
  kind: RemoteDesktopKind
  canCapture: boolean
  transitionSequence: number
  observedAtUtc: string
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

  
  onDesktopStateChanged?: (
    state: RemoteDesktopState,
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

  onStreamProfileChanged?: (
    profile: RemoteStreamProfile,
  ) => void
}

/*
 * ============================================================
 * PERFIL DE STREAMING / TRANSFERENCIA DE ARCHIVOS
 * ============================================================
 */

export type RemoteStreamPreset =
  | 'auto'
  | 'speed'
  | 'balanced'
  | 'quality'

export interface RemoteStreamProfile {
  sessionId?: string
  preset: RemoteStreamPreset
  /** 0 = automatico */
  maxWidth: number
  /** 0 = automatico */
  maxFps: number
}

export interface RemoteFileTransferProgress {
  fileName: string
  sentBytes: number
  totalBytes: number
}

export interface RemoteFileTransferResult {
  fileName: string
  savedAs: string
  bytes: number
}

/* Por encima de este tamano se omite el SHA-256 del navegador (memoria). */
const MAX_HASHED_FILE_BYTES =
  64 * 1024 * 1024

async function sha256Hex(
  file: Blob,
): Promise<string> {
  if (
    file.size > MAX_HASHED_FILE_BYTES ||
    !globalThis.crypto?.subtle
  ) {
    return ''
  }

  const digest =
    await crypto.subtle.digest(
      'SHA-256',
      await file.arrayBuffer(),
    )

  return Array.from(
    new Uint8Array(digest),
    byte => byte.toString(16).padStart(2, '0'),
  ).join('')
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
        .withHubProtocol(
          new MessagePackHubProtocol(),
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
      'RemoteStreamProfile',
      (
        profile:
          RemoteStreamProfile,
      ) => {
        handlers
          .onStreamProfileChanged?.(
            profile,
          )
      },
    )

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
    
    connection.on(
      'RemoteDesktopStateChanged',
      (state: RemoteDesktopState) => {
        handlers.onDesktopStateChanged?.(state)
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
   * LATENCIA: RTT medido con el reloj del navegador
   * ==========================================================
   */

  public async ping(): Promise<number> {
    const started =
      performance.now()

    await this
      .invoke<number>(
        'Ping',
        Date.now(),
      )

    return Math.round(
      performance.now() - started,
    )
  }

  /*
   * ==========================================================
   * PERFIL DE STREAMING
   * ==========================================================
   */

  public async setStreamProfile(
    sessionId: string,
    profile: RemoteStreamProfile,
  ): Promise<void> {
    await this
      .invoke(
        'SetStreamProfile',
        sessionId,
        profile.preset,
        profile.maxWidth,
        profile.maxFps,
      )
  }

  /*
   * ==========================================================
   * CTRL + ALT + SUPR (lo ejecuta el servicio SYSTEM del agente)
   * ==========================================================
   */

  public async sendSecureAttention(
    sessionId: string,
  ): Promise<{ commandId: string; status: string }> {
    return await this
      .invoke<{ commandId: string; status: string }>(
        'SendSecureAttention',
        sessionId,
      )
  }

  /*
   * ==========================================================
   * TRANSFERENCIA DE ARCHIVOS (tecnico -> equipo)
   * ==========================================================
   */

  public async sendFile(
    sessionId: string,
    file: File,
    onProgress?: (progress: RemoteFileTransferProgress) => void,
    signal?: AbortSignal,
  ): Promise<RemoteFileTransferResult> {
    const ticket =
      await this
        .invoke<{ transferId: string; chunkSize: number; fileName: string }>(
          'BeginFileTransfer',
          sessionId,
          file.name,
          file.size,
        )

    const hashPromise =
      sha256Hex(file)

    let offset = 0

    try {
      while (offset < file.size) {
        if (signal?.aborted) {
          throw new DOMException('Transferencia cancelada.', 'AbortError')
        }

        const chunk =
          new Uint8Array(
            await file
              .slice(offset, offset + ticket.chunkSize)
              .arrayBuffer(),
          )

        // Cada bloque espera el acuse del equipo (contrapresion).
        offset =
          await this
            .invoke<number>(
              'SendFileChunk',
              sessionId,
              ticket.transferId,
              offset,
              chunk,
            )

        onProgress?.({
          fileName: ticket.fileName,
          sentBytes: offset,
          totalBytes: file.size,
        })
      }

      return await this
        .invoke<RemoteFileTransferResult>(
          'CompleteFileTransfer',
          sessionId,
          ticket.transferId,
          await hashPromise,
        )
    } catch (error) {
      await this
        .invoke(
          'CancelFileTransfer',
          sessionId,
          ticket.transferId,
        )
        .catch(() => undefined)

      throw error
    }
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
