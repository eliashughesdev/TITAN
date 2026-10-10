import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react'

import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  deviceCommandSignalR,
} from '../../../api/deviceCommandSignalR'

import {
  devicesApi,
} from '../../../api/devicesApi'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
} from '../../../types/device'

import {
  commandProgressMessage,
  isTerminalCommand,
  mergeCommand,
} from '../../../utils/deviceCommandPresentation'

export function useDeviceDetail(
  deviceId:
    | string
    | undefined,
) {
  const [
    device,
    setDevice,
  ] =
    useState<DeviceDetails | null>(
      null,
    )

  const [
    androidDetails,
    setAndroidDetails,
  ] =
    useState<AndroidDeviceDetails | null>(
      null,
    )

  const [
    commands,
    setCommands,
  ] =
    useState<DeviceCommand[]>(
      [],
    )

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    sendingCommand,
    setSendingCommand,
  ] =
    useState<string | null>(
      null,
    )

  const [
    error,
    setError,
  ] =
    useState<string | null>(
      null,
    )

  const [
    message,
    setMessage,
  ] =
    useState<string | null>(
      null,
    )

  const [
    messageTone,
    setMessageTone,
  ] = useState<'info' | 'success'>(
    'info',
  )

  const trackedCommandId = useRef<string | null>(null)
  const activeDeviceId = useRef(deviceId)

  const handleCommandUpdate = useCallback(
    (command: DeviceCommand) => {
      setCommands(current =>
        mergeCommand(current, command),
      )

      if (trackedCommandId.current !== command.id) {
        return
      }

      if (
        command.status === 'Failed' ||
        command.status === 'Timeout' ||
        command.status === 'Cancelled'
      ) {
        setMessage(null)
        setError(commandProgressMessage(command))
        trackedCommandId.current = null
        return
      }

      setMessage(commandProgressMessage(command))
      setMessageTone(
        command.status === 'Success'
          ? 'success'
          : 'info',
      )

      if (isTerminalCommand(command.status)) {
        trackedCommandId.current = null
      }
    },
    [],
  )

  const refreshCommands =
    useCallback(
      async () => {
        if (!deviceId) {
          return
        }

        try {
          const response =
            await deviceCommandsApi
              .getForDevice(
                deviceId,
              )

          if (activeDeviceId.current !== deviceId) {
            return
          }

          setCommands(current =>
            response.items.reduce(
              (merged, command) =>
                mergeCommand(merged, command),
              current,
            ),
          )

          response.items.forEach(handleCommandUpdate)
        } catch (
          refreshError
        ) {
          console.error(
            'Error actualizando comandos:',
            refreshError,
          )
        }
      },
      [
        deviceId,
        handleCommandUpdate,
      ],
    )

  const loadDevice =
    useCallback(
      async () => {
        if (!deviceId) {
          setLoading(
            false,
          )

          setError(
            'No se recibió un identificador de dispositivo válido.',
          )

          return
        }

        try {
          setLoading(
            true,
          )

          setError(
            null,
          )

          const core =
            await devicesApi
              .getDeviceById(
                deviceId,
              )

          setDevice(
            core,
          )

          try {
            const commandData =
              await deviceCommandsApi
                .getForDevice(
                  deviceId,
                )

            setCommands(
              commandData.items,
            )
          } catch (
            commandError
          ) {
            console.error(
              'Error cargando historial de comandos:',
              commandError,
            )

            setCommands(
              [],
            )
          }

          if (
            core.platform ===
            'Android'
          ) {
            try {
              const android =
                await devicesApi
                  .getAndroidDeviceDetails(
                    deviceId,
                  )

              setAndroidDetails(
                android,
              )
            } catch (
              androidError
            ) {
              console.info(
                'El dispositivo Android no tiene información AMAPI asociada:',
                androidError,
              )

              setAndroidDetails(
                null,
              )
            }
          } else {
            setAndroidDetails(
              null,
            )
          }
        } catch (
          loadError
        ) {
          console.error(
            'Error cargando dispositivo:',
            loadError,
          )

          setDevice(
            null,
          )

          setError(
            'No fue posible obtener la información principal del dispositivo.',
          )
        } finally {
          setLoading(
            false,
          )
        }
      },
      [
        deviceId,
      ],
    )

  const sendCommand =
    useCallback(
      async (
        commandType: string,
        payloadJson =
          '{}',
      ) => {
        if (
          !deviceId
          ||
          sendingCommand !==
            null
        ) {
          return
        }

        try {
          setSendingCommand(
            commandType,
          )

          setError(
            null,
          )

          setMessage(
            null,
          )

          setMessageTone('info')

          const command =
            await deviceCommandsApi
              .create({
                deviceId,
                commandType,
                payloadJson,
                expirationMinutes:
                  30,
              })

          setCommands(
            current =>
              mergeCommand(current, command),
          )

          trackedCommandId.current = command.id

          setMessage(
            commandProgressMessage(command),
          )
        } catch (
          commandError
        ) {
          console.error(
            `Error enviando ${commandType}:`,
            commandError,
          )

          setError(
            `No fue posible enviar el comando ${commandType}.`,
          )
        } finally {
          setSendingCommand(
            null,
          )
        }
      },
      [
        deviceId,
        sendingCommand,
      ],
    )

  useEffect(
    () => {
      activeDeviceId.current = deviceId
    },
    [deviceId],
  )

  useEffect(
    () => {
      const timer = window.setTimeout(
        () => void loadDevice(),
        0,
      )

      return () => window.clearTimeout(timer)
    },
    [
      loadDevice,
    ],
  )

  useEffect(
    () => {
      if (!deviceId) {
        return
      }

      return deviceCommandSignalR.subscribe(
        deviceId,
        handleCommandUpdate,
      )
    },
    [deviceId, handleCommandUpdate],
  )

  useEffect(
    () => {
      if (!deviceId) {
        return
      }

      const timer =
        window.setInterval(
          () => {
            void refreshCommands()
          },
          5000,
        )

      return () =>
        window.clearInterval(
          timer,
        )
    },
    [
      deviceId,
      refreshCommands,
    ],
  )

  useEffect(
    () => {
      document.title =
        device
          ? `${device.deviceName} | TitanMDM`
          : 'Dispositivo | TitanMDM'
    },
    [
      device,
    ],
  )

  return {
    device,
    androidDetails,
    commands,

    loading,
    sendingCommand,

    error,
    message,
    messageTone,

    loadDevice,
    refreshCommands,
    sendCommand,
  }
}
