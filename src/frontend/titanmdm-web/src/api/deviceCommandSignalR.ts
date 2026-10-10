import * as signalR from '@microsoft/signalr'

import {
  tokenStorage,
} from '../auth/tokenStorage'

import type {
  DeviceCommand,
} from './deviceCommandsApi'

type CommandHandler = (
  command: DeviceCommand,
) => void

class DeviceCommandSignalRService {
  private connection:
    signalR.HubConnection | null = null

  private connecting:
    Promise<void> | null = null

  private reconnectTimer:
    number | null = null

  private readonly handlers =
    new Map<string, Set<CommandHandler>>()

  public subscribe(
    deviceId: string,
    handler: CommandHandler,
  ): () => void {
    const handlers =
      this.handlers.get(deviceId) ??
      new Set<CommandHandler>()

    handlers.add(handler)
    this.handlers.set(deviceId, handlers)

    void this.ensureConnected()
      .then(() => this.subscribeOnServer(deviceId))
      .catch(error => {
        console.warn(
          'No fue posible activar el seguimiento inmediato de comandos.',
          error,
        )
      })

    return () => {
      const current = this.handlers.get(deviceId)
      current?.delete(handler)

      if (current?.size === 0) {
        this.handlers.delete(deviceId)

        if (
          this.connection?.state ===
          signalR.HubConnectionState.Connected
        ) {
          void this.connection.invoke(
            'UnsubscribeDevice',
            deviceId,
          )
        }
      }

      if (this.handlers.size === 0) {
        void this.stop()
      }
    }
  }

  private async ensureConnected(): Promise<void> {
    if (
      this.connection?.state ===
      signalR.HubConnectionState.Connected
    ) {
      return
    }

    if (this.connecting) {
      return this.connecting
    }

    if (!this.connection) {
      const connection =
        new signalR.HubConnectionBuilder()
          .withUrl(
            '/hubs/device-commands',
            {
              accessTokenFactory: () =>
                tokenStorage.getAccessToken() ?? '',
            },
          )
          .withAutomaticReconnect(
            [0, 2000, 5000, 10000, 30000],
          )
          .configureLogging(
            signalR.LogLevel.Warning,
          )
          .build()

      connection.on(
        'DeviceCommandUpdated',
        (command: DeviceCommand) => {
          this.handlers
            .get(command.deviceId)
            ?.forEach(handler => handler(command))
        },
      )

      connection.onreconnected(async () => {
        await Promise.all(
          [...this.handlers.keys()].map(
            deviceId => this.subscribeOnServer(deviceId),
          ),
        )
      })

      connection.onclose(() => {
        this.scheduleReconnect()
      })

      this.connection = connection
    }

    this.connecting = this.connection
      .start()
      .catch(error => {
        this.scheduleReconnect()
        throw error
      })
      .finally(() => {
        this.connecting = null
      })

    return this.connecting
  }

  private async subscribeOnServer(
    deviceId: string,
  ): Promise<void> {
    if (
      !this.handlers.has(deviceId) ||
      this.connection?.state !==
        signalR.HubConnectionState.Connected
    ) {
      return
    }

    await this.connection.invoke(
      'SubscribeDevice',
      deviceId,
    )
  }

  private async stop(): Promise<void> {
    if (this.reconnectTimer !== null) {
      window.clearTimeout(this.reconnectTimer)
      this.reconnectTimer = null
    }

    const connection = this.connection
    this.connection = null

    if (connection) {
      await connection.stop()
    }
  }

  private scheduleReconnect(): void {
    if (
      this.handlers.size === 0 ||
      this.reconnectTimer !== null
    ) {
      return
    }

    const jitter = Math.floor(Math.random() * 1000)

    this.reconnectTimer = window.setTimeout(
      () => {
        this.reconnectTimer = null

        void this.ensureConnected()
          .then(() => Promise.all(
            [...this.handlers.keys()].map(
              deviceId => this.subscribeOnServer(deviceId),
            ),
          ))
          .catch(() => {
            this.scheduleReconnect()
          })
      },
      5000 + jitter,
    )
  }
}

export const deviceCommandSignalR =
  new DeviceCommandSignalRService()
