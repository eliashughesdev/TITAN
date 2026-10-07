import {
  Boxes,
  Cpu,
  Laptop,
  Network,
  Package,
  RefreshCw,
  ServerCog,
  Shield,
  ShieldCheck,
  XCircle,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  useNavigate,
  useParams,
} from 'react-router-dom'

import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../api/deviceCommandsApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import {
  WindowsControlChrome,
} from '../../components/windows-control/WindowsControlChrome'

import {
  WindowsActionsSection,
  WindowsCommandHistorySection,
  WindowsInventorySection,
  WindowsOverviewSection,
  WindowsProcessesSection,
  WindowsSecuritySection,
  WindowsServicesSection,
  WindowsSoftwareSection,
  WindowsUpdateSection,
} from '../../components/windows-control/WindowsControlSections'

import type {
  CommandDefinition,
  UninstallTarget,
  WindowsControlTab,
} from '../../components/windows-control/windowsControl.types'

import {
  getErrorMessage,
  latestByType,
  normalizeApplications,
  normalizeProcesses,
  normalizeServices,
  parseSecurity,
  parseUpdate,
} from '../../components/windows-control/windowsControl.utils'

import type {
  DeviceDetails,
} from '../../types/device'

import './WindowsControlCenterPage.css'
import './WindowsTelemetryCards.css'

const commandDefinitions:
  CommandDefinition[] = [
    {
      type: 'DEVICE_INFO',
      label: 'Información del equipo',
      description:
        'Consulta información general del endpoint.',
      icon:
        <Laptop
          size={18}
        />,
    },
    {
      type: 'DEVICE_INVENTORY',
      label: 'Inventario completo',
      description:
        'Obtiene inventario integral del endpoint.',
      icon:
        <Boxes
          size={18}
        />,
    },
    {
      type: 'NETWORK_INFO',
      label: 'Red',
      description:
        'Obtiene adaptadores e información de red.',
      icon:
        <Network
          size={18}
        />,
    },
    {
      type: 'SECURITY_STATUS',
      label: 'Seguridad',
      description:
        'Defender, Firewall, BitLocker, TPM y Secure Boot.',
      icon:
        <ShieldCheck
          size={18}
        />,
    },
    {
      type: 'COMPLIANCE_CHECK',
      label: 'Compliance',
      description:
        'Ejecuta evaluación de cumplimiento.',
      icon:
        <Shield
          size={18}
        />,
    },
    {
      type: 'WINDOWS_UPDATE_STATUS',
      label: 'Windows Update',
      description:
        'Consulta servicio, historial y reboot pendiente.',
      icon:
        <RefreshCw
          size={18}
        />,
    },
    {
      type: 'PROCESS_INVENTORY',
      label: 'Procesos',
      description:
        'Obtiene procesos activos.',
      icon:
        <Cpu
          size={18}
        />,
    },
    {
      type: 'SERVICE_INVENTORY',
      label: 'Servicios',
      description:
        'Obtiene servicios Windows.',
      icon:
        <ServerCog
          size={18}
        />,
    },
    {
      type: 'APP_INVENTORY',
      label: 'Software',
      description:
        'Obtiene aplicaciones instaladas.',
      icon:
        <Package
          size={18}
        />,
    },
  ]

export function WindowsControlCenterPage() {
  const {
    deviceId,
  } =
    useParams<{
      deviceId: string
    }>()

  const navigate =
    useNavigate()

  const [
    device,
    setDevice,
  ] =
    useState<
      DeviceDetails | null
    >(null)

  const [
    commands,
    setCommands,
  ] =
    useState<
      DeviceCommand[]
    >([])

  const [
    activeTab,
    setActiveTab,
  ] =
    useState<WindowsControlTab>(
      'overview',
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
    useState<
      string | null
    >(null)

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(null)

  const [
    message,
    setMessage,
  ] =
    useState<
      string | null
    >(null)

  const [
    processFilter,
    setProcessFilter,
  ] =
    useState('')

  const [
    serviceFilter,
    setServiceFilter,
  ] =
    useState('')

  const [
    softwareFilter,
    setSoftwareFilter,
  ] =
    useState('')

  const [
    scriptPath,
    setScriptPath,
  ] =
    useState('')

  const [
    scriptSha256,
    setScriptSha256,
  ] =
    useState('')

  const [
    scriptTimeout,
    setScriptTimeout,
  ] =
    useState('300')

  const [
    packagePath,
    setPackagePath,
  ] =
    useState('')

  const [
    packageSha256,
    setPackageSha256,
  ] =
    useState('')

  const [
    packageArguments,
    setPackageArguments,
  ] =
    useState('')

  const [
    uninstallTarget,
    setUninstallTarget,
  ] =
    useState<UninstallTarget>({
      name: '',
      productCode: '',
      executable: '',
      arguments: '',
    })

  /*
   * ==============================================================
   * DATA
   * ==============================================================
   */

  const loadData =
    useCallback(
      async () => {
        if (!deviceId) {
          setError(
            'DeviceId inválido.',
          )

          setLoading(
            false,
          )

          return
        }

        try {
          setError(
            null,
          )

          const [
            deviceResponse,
            commandResponse,
          ] =
            await Promise.all([
              devicesApi
                .getDeviceById(
                  deviceId,
                ),

              deviceCommandsApi
                .getForDevice(
                  deviceId,
                ),
            ])

          if (
            deviceResponse.platform !==
            'Windows'
          ) {
            setDevice(
              null,
            )

            setError(
              'El Control Center solo admite endpoints Windows.',
            )

            return
          }

          setDevice(
            deviceResponse,
          )

          setCommands(
            commandResponse.items,
          )
        } catch (
          loadError
        ) {
          setError(
            getErrorMessage(
              loadError,
              'No fue posible cargar el endpoint.',
            ),
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
        } catch {
          // Polling silencioso.
        }
      },
      [
        deviceId,
      ],
    )

  useEffect(
    () => {
      void loadData()
    },
    [
      loadData,
    ],
  )

  useEffect(
    () => {
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
      refreshCommands,
    ],
  )

  useEffect(
    () => {
      document.title =
        device
          ? `${device.deviceName} | TitanMDM`
          : 'Windows Control Center | TitanMDM'
    },
    [
      device,
    ],
  )

  /*
   * ==============================================================
   * COMMAND
   * ==============================================================
   */

  const sendCommand =
    useCallback(
      async (
        commandType:
          string,

        payload:
          Record<
            string,
            unknown
          > = {},
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

                payloadJson:
                  JSON.stringify(
                    payload,
                  ),

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
            `${commandType} enviado al endpoint.`,
          )
        } catch (
          commandError
        ) {
          setError(
            getErrorMessage(
              commandError,
              `No fue posible ejecutar ${commandType}.`,
            ),
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

  /*
   * ==============================================================
   * SNAPSHOTS
   * ==============================================================
   */

  const processCommand =
    useMemo(
      () =>
        latestByType(
          commands,
          'PROCESS_INVENTORY',
        ),
      [
        commands,
      ],
    )

  const serviceCommand =
    useMemo(
      () =>
        latestByType(
          commands,
          'SERVICE_INVENTORY',
        ),
      [
        commands,
      ],
    )

  const softwareCommand =
    useMemo(
      () =>
        latestByType(
          commands,
          'APP_INVENTORY',
        ),
      [
        commands,
      ],
    )

  const securityCommand =
    useMemo(
      () =>
        latestByType(
          commands,
          'SECURITY_STATUS',
        ),
      [
        commands,
      ],
    )

  const updateCommand =
    useMemo(
      () =>
        latestByType(
          commands,
          'WINDOWS_UPDATE_STATUS',
        ),
      [
        commands,
      ],
    )

  const processes =
    useMemo(
      () =>
        normalizeProcesses(
          processCommand,
        ),
      [
        processCommand,
      ],
    )

  const services =
    useMemo(
      () =>
        normalizeServices(
          serviceCommand,
        ),
      [
        serviceCommand,
      ],
    )

  const applications =
    useMemo(
      () =>
        normalizeApplications(
          softwareCommand,
        ),
      [
        softwareCommand,
      ],
    )

  const security =
    useMemo(
      () =>
        parseSecurity(
          securityCommand,
        ),
      [
        securityCommand,
      ],
    )

  const update =
    useMemo(
      () =>
        parseUpdate(
          updateCommand,
        ),
      [
        updateCommand,
      ],
    )

  /*
   * ==============================================================
   * FILTERED DATA
   * ==============================================================
   */

  const filteredProcesses =
    useMemo(
      () => {
        const filter =
          processFilter
            .trim()
            .toLowerCase()

        if (!filter) {
          return processes
        }

        return processes.filter(
          process =>
            process.name
              .toLowerCase()
              .includes(
                filter,
              )
            ||
            String(
              process.processId,
            ).includes(
              filter,
            ),
        )
      },
      [
        processes,
        processFilter,
      ],
    )

  const filteredServices =
    useMemo(
      () => {
        const filter =
          serviceFilter
            .trim()
            .toLowerCase()

        if (!filter) {
          return services
        }

        return services.filter(
          service =>
            service.name
              .toLowerCase()
              .includes(
                filter,
              )
            ||
            (
              service.displayName
              ??
              ''
            )
              .toLowerCase()
              .includes(
                filter,
              ),
        )
      },
      [
        services,
        serviceFilter,
      ],
    )

  const filteredApplications =
    useMemo(
      () => {
        const filter =
          softwareFilter
            .trim()
            .toLowerCase()

        if (!filter) {
          return applications
        }

        return applications.filter(
          application =>
            application.name
              .toLowerCase()
              .includes(
                filter,
              )
            ||
            (
              application.publisher
              ??
              ''
            )
              .toLowerCase()
              .includes(
                filter,
              ),
        )
      },
      [
        applications,
        softwareFilter,
      ],
    )

  /*
   * ==============================================================
   * KPIS
   * ==============================================================
   */

  const activeCommands =
    commands.filter(
      command =>
        [
          'Pending',
          'Queued',
          'Dispatching',
          'Sent',
          'Delivered',
          'Executing',
        ].includes(
          command.status,
        ),
    ).length

  const successfulCommands =
    commands.filter(
      command =>
        command.status ===
        'Success',
    ).length

  const failedCommands =
    commands.filter(
      command =>
        command.status ===
          'Failed'
        ||
        command.status ===
          'Timeout',
    ).length

  /*
   * ==============================================================
   * PAGE STATES
   * ==============================================================
   */

  if (
    loading
    &&
    device === null
  ) {
    return (
      <div className="windows-control-loading">
        <RefreshCw
          size={26}
          className="windows-control-spin"
        />

        Cargando Windows Control Center...
      </div>
    )
  }

  if (
    device === null
  ) {
    return (
      <div className="windows-control-error-page">
        <XCircle
          size={34}
        />

        <h2>
          Control Center no disponible
        </h2>

        <p>
          {error ??
            'No fue posible cargar el endpoint.'}
        </p>
      </div>
    )
  }

  return (
    <div className="windows-control-page">
      <WindowsControlChrome
        device={
          device
        }
        activeTab={
          activeTab
        }
        activeCommands={
          activeCommands
        }
        successfulCommands={
          successfulCommands
        }
        failedCommands={
          failedCommands
        }
        sendingCommand={
          sendingCommand
        }
        onBack={() =>
          navigate(
            '/devices?platform=Windows&workspace=windows',
          )
        }
        onRefresh={() =>
          void loadData()
        }
        onRemote={() =>
          navigate(
            '/remote?workspace=windows',
          )
        }
        onPing={() =>
          void sendCommand(
            'PING',
          )
        }
        onTabChange={
          setActiveTab
        }
      />

      {error && (
        <div className="windows-control-notice windows-control-notice--error">
          <XCircle
            size={17}
          />

          {error}
        </div>
      )}

      {message && (
        <div className="windows-control-notice windows-control-notice--success">
          {message}
        </div>
      )}

      {activeTab ===
        'overview' && (
        <WindowsOverviewSection
          device={
            device
          }
          definitions={
            commandDefinitions
          }
          sendingCommand={
            sendingCommand
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'inventory' && (
        <WindowsInventorySection
          commands={
            commands
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'security' && (
        <WindowsSecuritySection
          security={
            security
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'updates' && (
        <WindowsUpdateSection
          update={
            update
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'processes' && (
        <WindowsProcessesSection
          processes={
            filteredProcesses
          }
          filter={
            processFilter
          }
          setFilter={
            setProcessFilter
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'services' && (
        <WindowsServicesSection
          services={
            filteredServices
          }
          filter={
            serviceFilter
          }
          setFilter={
            setServiceFilter
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'software' && (
        <WindowsSoftwareSection
          applications={
            filteredApplications
          }
          filter={
            softwareFilter
          }
          setFilter={
            setSoftwareFilter
          }
          packagePath={
            packagePath
          }
          setPackagePath={
            setPackagePath
          }
          packageSha256={
            packageSha256
          }
          setPackageSha256={
            setPackageSha256
          }
          packageArguments={
            packageArguments
          }
          setPackageArguments={
            setPackageArguments
          }
          uninstallTarget={
            uninstallTarget
          }
          setUninstallTarget={
            setUninstallTarget
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'actions' && (
        <WindowsActionsSection
          scriptPath={
            scriptPath
          }
          setScriptPath={
            setScriptPath
          }
          scriptSha256={
            scriptSha256
          }
          setScriptSha256={
            setScriptSha256
          }
          scriptTimeout={
            scriptTimeout
          }
          setScriptTimeout={
            setScriptTimeout
          }
          sendCommand={
            sendCommand
          }
        />
      )}

      {activeTab ===
        'commands' && (
        <WindowsCommandHistorySection
          commands={
            commands
          }
        />
      )}
    </div>
  )
}