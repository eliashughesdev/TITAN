import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  devicesApi,
} from '../../../api/devicesApi'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
} from '../../../types/device'

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

          setCommands(
            response.items,
          )
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
            current => [
              command,

              ...current.filter(
                item =>
                  item.id !==
                  command.id,
              ),
            ],
          )

          setMessage(
            `Comando ${commandType} enviado correctamente. Estado actual: ${command.status}.`,
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
      void loadDevice()
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

    loadDevice,
    refreshCommands,
    sendCommand,
  }
}