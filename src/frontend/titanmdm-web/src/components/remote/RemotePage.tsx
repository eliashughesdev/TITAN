
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'

import {
  CircleStop,
  Clock3,
  Keyboard,
  Maximize2,
  Monitor,
  MousePointer2,
  Play,
  RadioTower,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react'

import { devicesApi } from '../../api/devicesApi'

import {
  createRemoteSession,
  getRemoteSession,
  listRemoteSessions,
  terminateRemoteSession,
} from '../../api/remoteSupportApi'

import type {
  RemoteSession,
} from '../../api/remoteSupportApi'

import {
  RemoteSupportSignalRClient,
} from '../../api/remoteSupportSignalR'

import type {
  RemoteControlState,
  RemoteFrame,
  RemoteSessionChanged,
  RemoteDesktopState,
} from '../../api/remoteSupportSignalR'

import RemoteDesktopViewer from './RemoteDesktopViewer'

import type {
  DeviceListItem,
} from '../../types/device'

import { useAuth } from '../../auth/AuthContext'

import './RemotePage.css'

// ============================================================
// TYPES AND HELPERS
// ============================================================

interface RemoteStreamMetrics {
  fps: number
  latencyMs: number
  kilobitsPerSecond: number
}

function formatDate(value?: string | null): string {
  if (!value) return '—'

  return new Date(value).toLocaleString()
}

function statusLabel(status?: string): string {
  const names: Record<string, string> = {
    Requested: 'Solicitada',
    Connecting: 'Conectando',
    Connected: 'Conectada',
    Disconnecting: 'Desconectando',
    Completed: 'Finalizada',
    Failed: 'Error',
    Expired: 'Expirada',
    Cancelled: 'Cancelada',
  }

  return status
    ? names[status] ?? status
    : 'Sin sesión'
}

function isTerminal(status: string): boolean {
  return [
    'Completed',
    'Failed',
    'Expired',
    'Cancelled',
  ].includes(status)
}

function desktopMessage(
  state: RemoteDesktopState,
): string {
  switch (state.kind) {
    case 'Winlogon':
      return (
        'Windows activó el escritorio protegido. ' +
        'La captura y el control están temporalmente restringidos.'
      )

    case 'AccessDenied':
      return (
        'Windows denegó el acceso al escritorio ' +
        'de entrada de esta sesión.'
      )

    case 'ScreenSaver':
      return (
        'Windows activó el protector de pantalla. ' +
        'Esperando el regreso al escritorio interactivo.'
      )

    case 'Unavailable':
      return (
        'El escritorio interactivo está temporalmente ' +
        'inaccesible.'
      )

    default:
      return (
        'Windows cambió a otro escritorio. ' +
        'La transmisión normal se recuperará ' +
        'cuando vuelva a estar disponible.'
      )
  }
}

function emptyMetricWindow() {
  return {
    startedAt: 0,
    frames: 0,
    bytes: 0,
  }
}

// ============================================================
// COMPONENT
// ============================================================

export function RemotePage() {
  const { user } = useAuth()

  const canManage =
    user?.permissions?.includes('remote.manage') ?? false

  // ==========================================================
  // REFS
  // ==========================================================

  const signalRRef =
    useRef<RemoteSupportSignalRClient | null>(null)

  const selectedSessionIdRef = useRef('')

  const viewerContainerRef =
    useRef<HTMLDivElement | null>(null)

  const loadGeneration = useRef(0)

  const metricWindowRef = useRef(emptyMetricWindow())

  const desktopProtectedRef = useRef(false)

  const desktopTransitionRef = useRef(-1)

  const desktopSessionRef = useRef('')

  const loadDataRef = useRef<() => Promise<void>>(
    async () => {},
  )

  // ==========================================================
  // DEVICE AND SESSION STATE
  // ==========================================================

  const [deviceSearch, setDeviceSearch] =
    useState('')

  const [devices, setDevices] =
    useState<DeviceListItem[]>([])

  const [sessions, setSessions] =
    useState<RemoteSession[]>([])

  const [activeSession, setActiveSession] =
    useState<RemoteSession | null>(null)

  const [selectedSessionId, setSelectedSessionId] =
    useState('')

  const [selectedDeviceId, setSelectedDeviceId] =
    useState('')

  // ==========================================================
  // SESSION OPTIONS
  // ==========================================================

  const [reason, setReason] =
    useState('Soporte técnico remoto')

  const [
    maximumDurationMinutes,
    setMaximumDurationMinutes,
  ] = useState(120)

  const [allowMouse, setAllowMouse] =
    useState(true)

  const [allowKeyboard, setAllowKeyboard] =
    useState(true)

  const [allowClipboard, setAllowClipboard] =
    useState(false)

  const [allowFileTransfer, setAllowFileTransfer] =
    useState(false)

  // ==========================================================
  // REMOTE VIEWER STATE
  // ==========================================================

  const [frame, setFrame] =
    useState<RemoteFrame | null>(null)

  const [streamMetrics, setStreamMetrics] =
    useState<RemoteStreamMetrics | null>(null)

  const [controlState, setControlState] =
    useState<RemoteControlState | null>(null)

  const [desktopState, setDesktopState] =
    useState<RemoteDesktopState | null>(null)

  // ==========================================================
  // UI STATE
  // ==========================================================

  const [changingControl, setChangingControl] =
    useState(false)

  const [loading, setLoading] =
    useState(true)

  const [creating, setCreating] =
    useState(false)

  const [terminating, setTerminating] =
    useState(false)

  const [channelConnected, setChannelConnected] =
    useState(false)

  const [error, setError] =
    useState<string | null>(null)

  // ==========================================================
  // DERIVED STATE
  // ==========================================================

  const windowsDevices = useMemo(
    () =>
      devices.filter(device =>
        device.platform === 'Windows' &&
        device.isManaged,
      ),
    [devices],
  )

  const activeSessions = useMemo(
    () =>
      sessions.filter(session =>
        !isTerminal(session.status),
      ),
    [sessions],
  )

  const selectedDevice = useMemo(
    () =>
      windowsDevices.find(device =>
        device.id === selectedDeviceId,
      ) ?? null,
    [windowsDevices, selectedDeviceId],
  )

  const ownsControl = Boolean(
    controlState?.hasController &&
    controlState.userId &&
    user?.id &&
    controlState.userId.toLowerCase() ===
      user.id.toLowerCase(),
  )

  const connected =
    activeSession?.status === 'Connected'

  const busySession =
    !!activeSession &&
    !isTerminal(activeSession.status)

  const desktopProtected =
    desktopState !== null &&
    !desktopState.canCapture

  const candidates = windowsDevices.filter(device =>
    !deviceSearch.trim() ||
    (
      `${device.deviceName} ` +
      `${device.assignedUser ?? ''} ` +
      `${device.ipAddress ?? ''}`
    ).toLowerCase().includes(
      deviceSearch.trim().toLowerCase(),
    ),
  )

  // ==========================================================
  // RESET HELPERS
  // ==========================================================

  const resetVideoState = useCallback(() => {
    setFrame(null)
    setStreamMetrics(null)

    metricWindowRef.current =
      emptyMetricWindow()
  }, [])

  const resetDesktopState = useCallback(() => {
    desktopProtectedRef.current = false
    desktopTransitionRef.current = -1
    desktopSessionRef.current = ''

    setDesktopState(null)
  }, [])

  const resetSessionView = useCallback(() => {
    selectedSessionIdRef.current = ''

    setSelectedSessionId('')
    setSelectedDeviceId('')
    setActiveSession(null)
    setControlState(null)

    resetVideoState()
    resetDesktopState()
  }, [resetVideoState, resetDesktopState])

  // ==========================================================
  // SESSION ID SYNCHRONIZATION
  // ==========================================================

  useEffect(() => {
    selectedSessionIdRef.current =
      selectedSessionId
  }, [selectedSessionId])

  // ==========================================================
  // LOAD DEVICES AND SESSIONS
  // ==========================================================

  const loadData = useCallback(async () => {
    const request =
      ++loadGeneration.current

    try {
      const [
        deviceResult,
        sessionResult,
      ] = await Promise.all([
        devicesApi.getDevices({
          platform: 'Windows',
          search: deviceSearch,
          page: 1,
          pageSize: 100,
        }),

        listRemoteSessions(200),
      ])

      if (request !== loadGeneration.current) {
        return
      }

      setDevices(deviceResult.items)
      setSessions(sessionResult)
    } catch (requestError) {
      if (request !== loadGeneration.current) {
        return
      }

      console.error(requestError)

      setError(
        'No fue posible cargar los dispositivos ' +
        'o las sesiones remotas.',
      )
    } finally {
      if (request === loadGeneration.current) {
        setLoading(false)
      }
    }
  }, [deviceSearch])

  useEffect(() => {
    loadDataRef.current = loadData
  }, [loadData])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData()
    }, 300)

    return () => {
      window.clearTimeout(timer)
    }
  }, [loadData])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void loadData()
    }, 15000)

    return () => {
      window.clearInterval(timer)
    }
  }, [loadData])

  // ==========================================================
  // REFRESH SESSION
  // ==========================================================

  const refreshActiveSession = useCallback(
    async (sessionId: string) => {
      try {
        const updated =
          await getRemoteSession(sessionId)

        if (
          selectedSessionIdRef.current ===
          updated.id
        ) {
          if (isTerminal(updated.status)) {
            resetSessionView()
          } else {
            setActiveSession(updated)
          }
        }

        setSessions(current => {
          if (!current.some(
            item => item.id === updated.id,
          )) {
            return [updated, ...current]
          }

          return current.map(item =>
            item.id === updated.id
              ? updated
              : item,
          )
        })
      } catch (requestError) {
        console.error(
          'No fue posible actualizar la sesión remota.',
          requestError,
        )
      }
    },
    [resetSessionView],
  )

  // ==========================================================
  // SIGNALR CONNECTION
  // ==========================================================

  useEffect(() => {
    let disposed = false

    const client =
      new RemoteSupportSignalRClient()

    signalRRef.current = client

    const joinCurrentSession = async () => {
      const sessionId =
        selectedSessionIdRef.current

      if (!sessionId || disposed) {
        return
      }

      try {
        await client.joinSession(sessionId)

        await client.requestControlState(
          sessionId,
        )
      } catch (joinError) {
        console.error(
          'JoinSession falló.',
          joinError,
        )

        if (!disposed) {
          setError(
            'El canal está conectado, pero no fue ' +
            'posible unirse a la sesión. ' +
            'Revisa permisos y estado.',
          )
        }
      }
    }

    // ========================================================
    // CONNECT AND EVENT HANDLERS
    // ========================================================

    void client.connect({
      // ------------------------------------------------------
      // REMOTE FRAME
      // ------------------------------------------------------

      onFrame: nextFrame => {
        if (
          disposed ||
          desktopProtectedRef.current ||
          nextFrame.sessionId !==
            selectedSessionIdRef.current
        ) {
          return
        }

        const metricWindow =
          metricWindowRef.current

        const now = performance.now()

        if (metricWindow.startedAt === 0) {
          metricWindow.startedAt = now
        }

        metricWindow.frames += 1

        metricWindow.bytes +=
          nextFrame.data.byteLength

        const elapsed =
          now - metricWindow.startedAt

        if (elapsed >= 1000) {
          setStreamMetrics({
            fps: Math.round(
              metricWindow.frames *
                1000 / elapsed,
            ),

            latencyMs: Math.max(
              0,
              Date.now() -
                Date.parse(
                  nextFrame.capturedAtUtc,
                ),
            ),

            kilobitsPerSecond: Math.round(
              metricWindow.bytes *
                8 / elapsed,
            ),
          })

          metricWindowRef.current =
            emptyMetricWindow()
        }

        setFrame(current => {
          if (
            desktopProtectedRef.current ||
            nextFrame.sessionId !==
              selectedSessionIdRef.current
          ) {
            return null
          }

          if (
            current &&
            current.sessionId ===
              nextFrame.sessionId &&
            nextFrame.sequence <=
              current.sequence
          ) {
            return current
          }

          return nextFrame
        })
      },

      // ------------------------------------------------------
      // WINDOWS DESKTOP STATE
      // ------------------------------------------------------

      onDesktopStateChanged: state => {
        if (
          disposed ||
          state.sessionId !==
            selectedSessionIdRef.current
        ) {
          return
        }

        if (
          desktopSessionRef.current !==
          state.sessionId
        ) {
          desktopSessionRef.current =
            state.sessionId

          desktopTransitionRef.current = -1
        }

        if (
          state.transitionSequence <
          desktopTransitionRef.current
        ) {
          return
        }

        desktopTransitionRef.current =
          state.transitionSequence

        desktopProtectedRef.current =
          !state.canCapture

        setDesktopState(state)

        if (!state.canCapture) {
          resetVideoState()
        }
      },

      // ------------------------------------------------------
      // SESSION STATUS
      // ------------------------------------------------------

      onSessionChanged: (
        update: RemoteSessionChanged,
      ) => {
        if (disposed) {
          return
        }

        if (isTerminal(update.status)) {
          if (
            update.sessionId ===
            selectedSessionIdRef.current
          ) {
            resetSessionView()
          }

          setSessions(current =>
            current.map(session =>
              session.id === update.sessionId
                ? {
                    ...session,
                    status:
                      update.status as
                        RemoteSession['status'],
                  }
                : session,
            ),
          )

          return
        }

        void refreshActiveSession(
          update.sessionId,
        )
      },

      // ------------------------------------------------------
      // CONTROL STATE
      // ------------------------------------------------------

      onControlStateChanged:
        nextControlState => {
          if (
            disposed ||
            nextControlState.sessionId !==
              selectedSessionIdRef.current
          ) {
            return
          }

          setControlState(
            nextControlState,
          )
        },

      // ------------------------------------------------------
      // REMOTE HOST STATE
      // ------------------------------------------------------

      onRemoteHostStateChanged:
        hostState => {
          if (
            disposed ||
            hostState.sessionId !==
              selectedSessionIdRef.current
          ) {
            return
          }

          if (!hostState.connected) {
            resetVideoState()
            resetDesktopState()
          }
        },

      // ------------------------------------------------------
      // RECONNECTING
      // ------------------------------------------------------

      onReconnecting: () => {
        if (disposed) {
          return
        }

        setChannelConnected(false)

        resetVideoState()
      },

      // ------------------------------------------------------
      // RECONNECTED
      // ------------------------------------------------------

      onReconnected: () => {
        if (disposed) {
          return
        }

        setChannelConnected(true)

        resetVideoState()
        resetDesktopState()

        void joinCurrentSession()
      },

      // ------------------------------------------------------
      // CHANNEL CLOSED
      // ------------------------------------------------------

      onClosed: closeError => {
        if (disposed) {
          return
        }

        setChannelConnected(false)
        setControlState(null)

        resetVideoState()
        resetDesktopState()

        if (closeError) {
          console.error(
            'Canal remoto cerrado.',
            closeError,
          )
        }
      },
    })
      .then(async () => {
        if (disposed) {
          return
        }

        setChannelConnected(true)

        await joinCurrentSession()
      })
      .catch(connectionError => {
        if (disposed) {
          return
        }

        console.error(
          'No fue posible conectar SignalR.',
          connectionError,
        )

        setChannelConnected(false)

        resetVideoState()
        resetDesktopState()

        setError(
          'No fue posible establecer el canal ' +
          'de soporte remoto.',
        )
      })

    // ========================================================
    // CLEANUP
    // ========================================================

    return () => {
      disposed = true

      if (signalRRef.current === client) {
        signalRRef.current = null
      }

      void client.disconnect()
    }
  }, [
    refreshActiveSession,
    resetSessionView,
    resetVideoState,
    resetDesktopState,
  ])

  // ==========================================================
  // SELECT SESSION
  // ==========================================================

  const selectSession = async (
    sessionId: string,
  ) => {
    try {
      setError(null)

      resetVideoState()
      resetDesktopState()

      setControlState(null)
      setActiveSession(null)

      const previousSessionId =
        selectedSessionIdRef.current

      if (
        previousSessionId &&
        previousSessionId !== sessionId &&
        signalRRef.current?.isConnected
      ) {
        try {
          await signalRRef.current
            .leaveSession(previousSessionId)
        } catch (leaveError) {
          console.warn(
            'No fue posible abandonar ' +
            'la sesión anterior.',
            leaveError,
          )
        }
      }

      selectedSessionIdRef.current =
        sessionId

      setSelectedSessionId(sessionId)

      if (!sessionId) {
        return
      }

      const session =
        await getRemoteSession(sessionId)

      if (
        selectedSessionIdRef.current !==
        sessionId
      ) {
        return
      }

      if (isTerminal(session.status)) {
        resetSessionView()
        return
      }

      setActiveSession(session)

      setSelectedDeviceId(
        session.deviceId,
      )

      if (
        signalRRef.current &&
        channelConnected
      ) {
        try {
          await signalRRef.current
            .joinSession(sessionId)

          await signalRRef.current
            .requestControlState(sessionId)
        } catch (joinError) {
          console.error(joinError)

          setError(
            'El canal está disponible, ' +
            'pero no fue posible unirse ' +
            'a esta sesión remota.',
          )
        }
      }
    } catch (requestError) {
      console.error(requestError)

      if (
        selectedSessionIdRef.current ===
        sessionId
      ) {
        setError(
          'No fue posible abrir la ' +
          'sesión seleccionada.',
        )
      }
    }
  }

  // ==========================================================
  // START SESSION
  // ==========================================================

  const startSession = async () => {
    if (
      !canManage ||
      creating ||
      busySession
    ) {
      return
    }

    if (!selectedDeviceId) {
      setError(
        'Selecciona un equipo Windows.',
      )
      return
    }

    if (reason.trim().length < 3) {
      setError(
        'Indica el motivo de la conexión remota.',
      )
      return
    }

    try {
      setCreating(true)
      setError(null)

      resetVideoState()
      resetDesktopState()

      setControlState(null)

      const session =
        await createRemoteSession({
          deviceId: selectedDeviceId,
          reason: reason.trim(),
          allowKeyboard,
          allowMouse,
          allowClipboard,
          allowFileTransfer,
          maximumDurationMinutes,
        })

      setSessions(current => [
        session,
        ...current.filter(item =>
          item.id !== session.id,
        ),
      ])

      selectedSessionIdRef.current =
        session.id

      setSelectedSessionId(session.id)
      setActiveSession(session)

      if (
        signalRRef.current &&
        channelConnected
      ) {
        try {
          await signalRRef.current
            .joinSession(session.id)

          await signalRRef.current
            .requestControlState(session.id)
        } catch (joinError) {
          console.error(joinError)

          setError(
            'La sesión fue creada, ' +
            'pero no fue posible unirse ' +
            'al canal SignalR.',
          )
        }
      }
    } catch (requestError) {
      const message =
        requestError instanceof Error
          ? requestError.message
          : String(requestError)

      setError(
        `No fue posible crear la sesión remota. ${message}`,
      )
    } finally {
      setCreating(false)
    }
  }

  // ==========================================================
  // TERMINATE SESSION
  // ==========================================================

  const endSession = async () => {
    if (
      !canManage ||
      !activeSession ||
      terminating
    ) {
      return
    }

    const sessionId = activeSession.id

    try {
      setTerminating(true)
      setError(null)

      await terminateRemoteSession(
        sessionId,
      )

      if (
        selectedSessionIdRef.current ===
        sessionId
      ) {
        resetSessionView()
      }

      if (signalRRef.current?.isConnected) {
        try {
          await signalRRef.current
            .leaveSession(sessionId)
        } catch (leaveError) {
          console.warn(
            'La sesión terminó, ' +
            'pero LeaveSession falló.',
            leaveError,
          )
        }
      }

      setSessions(current =>
        current.map(session =>
          session.id === sessionId
            ? {
                ...session,
                status: 'Completed',
              }
            : session,
        ),
      )

      await loadDataRef.current()
    } catch (requestError) {
      console.error(
        'Finalización fallida.',
        requestError,
      )

      setError(
        'No fue posible confirmar ' +
        'la finalización de la sesión.',
      )
    } finally {
      setTerminating(false)
    }
  }

  // ==========================================================
  // CONTROL LEASE RENEWAL
  // ==========================================================

  useEffect(() => {
    if (
      !ownsControl ||
      !selectedSessionId ||
      !channelConnected
    ) {
      return
    }

    const renew = async () => {
      const client = signalRRef.current

      if (
        !client ||
        selectedSessionIdRef.current !==
          selectedSessionId
      ) {
        return
      }

      try {
        await client.renewControl(
          selectedSessionId,
        )
      } catch (renewError) {
        console.error(
          'RenewControl falló.',
          renewError,
        )

        setControlState(null)

        setError(
          'Se perdió el control remoto. ' +
          'Solicítalo nuevamente.',
        )
      }
    }

    const timer = window.setInterval(() => {
      void renew()
    }, 20000)

    return () => {
      window.clearInterval(timer)
    }
  }, [
    channelConnected,
    ownsControl,
    selectedSessionId,
  ])

  // ==========================================================
  // ACQUIRE / RELEASE CONTROL
  // ==========================================================

  const toggleControl = async () => {
    const client = signalRRef.current

    if (
      !canManage ||
      !activeSession ||
      !client ||
      !channelConnected ||
      changingControl ||
      isTerminal(activeSession.status)
    ) {
      return
    }

    try {
      setChangingControl(true)
      setError(null)

      if (ownsControl) {
        await client.releaseControl(
          activeSession.id,
        )

        return
      }

      const nextState =
        await client.acquireControl(
          activeSession.id,
        )

      setControlState(nextState)
    } catch (controlError) {
      const message =
        controlError instanceof Error
          ? controlError.message
          : String(controlError)

      setError(
        `No fue posible cambiar el control remoto. ${message}`,
      )
    } finally {
      setChangingControl(false)
    }
  }

  // ==========================================================
  // POINTER MOVE
  // ==========================================================

  const pointerMove = useCallback(
    (x: number, y: number) => {
      if (
        !activeSession ||
        !signalRRef.current ||
        !channelConnected ||
        !ownsControl ||
        desktopProtectedRef.current ||
        activeSession.status !== 'Connected'
      ) {
        return
      }

      void signalRRef.current
        .pointerMove(
          activeSession.id,
          x,
          y,
        )
        .catch(console.error)
    },
    [
      activeSession,
      channelConnected,
      ownsControl,
    ],
  )

  // ==========================================================
  // POINTER BUTTON
  // ==========================================================

  const pointerButton = useCallback(
    (
      action:
        | 'left-down'
        | 'left-up'
        | 'right-down'
        | 'right-up',
    ) => {
      if (
        !activeSession ||
        !signalRRef.current ||
        !channelConnected ||
        !ownsControl ||
        desktopProtectedRef.current ||
        activeSession.status !== 'Connected'
      ) {
        return
      }

      void signalRRef.current
        .pointerButton(
          activeSession.id,
          action,
        )
        .catch(console.error)
    },
    [
      activeSession,
      channelConnected,
      ownsControl,
    ],
  )

  // ==========================================================
  // MOUSE WHEEL
  // ==========================================================

  const wheel = useCallback(
    (delta: number) => {
      if (
        !activeSession ||
        !signalRRef.current ||
        !channelConnected ||
        !ownsControl ||
        desktopProtectedRef.current ||
        activeSession.status !== 'Connected'
      ) {
        return
      }

      void signalRRef.current
        .pointerWheel(
          activeSession.id,
          delta,
        )
        .catch(console.error)
    },
    [
      activeSession,
      channelConnected,
      ownsControl,
    ],
  )

  // ==========================================================
  // KEYBOARD
  // ==========================================================

  const keyboard = useCallback(
    (
      virtualKey: number,
      keyDown: boolean,
    ) => {
      if (
        !activeSession ||
        !signalRRef.current ||
        !channelConnected ||
        !ownsControl ||
        desktopProtectedRef.current ||
        activeSession.status !== 'Connected'
      ) {
        return
      }

      void signalRRef.current
        .keyboard(
          activeSession.id,
          virtualKey,
          keyDown,
        )
        .catch(console.error)
    },
    [
      activeSession,
      channelConnected,
      ownsControl,
    ],
  )

  // ==========================================================
  // FULLSCREEN
  // ==========================================================

  const toggleFullscreen = async () => {
    const element =
      viewerContainerRef.current

    if (!element) {
      return
    }

    if (document.fullscreenElement) {
      await document.exitFullscreen()
      return
    }

    try {
      await element.requestFullscreen()
    } catch {
      setError(
        'No fue posible activar pantalla completa.',
      )
    }
  }

  // ==========================================================
  // RENDER
  // ==========================================================

  return (
    <main className="remote-page wr">
      {/* ====================================================
          HEADER
          ==================================================== */}

      <header className="wr-header">
        <div>
          <span className="wr-eyebrow">
            WINDOWS · CONSOLA DE SOPORTE
          </span>

          <h1>
            <RadioTower size={28} />
            Soporte remoto
          </h1>

          <p>
            Selecciona un equipo, inicia la sesión
            y trabaja desde una consola central.
          </p>
        </div>

        <div className="wr-header-actions">
          <span
            className={
              `wr-chip ${
                channelConnected
                  ? 'wr-online'
                  : 'wr-offline'
              }`
            }
          >
            <RadioTower size={15} />

            {channelConnected
              ? 'Canal conectado'
              : 'Canal desconectado'}
          </span>

          <button
            onClick={() => void loadData()}
            disabled={creating}
          >
            <RefreshCw size={16} />
            Actualizar
          </button>
        </div>
      </header>

      {/* ====================================================
          ERRORS
          ==================================================== */}

      {error && (
        <div className="wr-error" role="alert">
          {error}
        </div>
      )}

      <div className="wr-layout">
        {/* ==================================================
            DEVICE SIDEBAR
            ================================================== */}

        <aside className="wr-sidebar">
          <section className="wr-card">
            <div className="wr-card-title">
              <h2>Nueva conexión</h2>
              <Monitor size={19} />
            </div>

            {/* DEVICE SEARCH */}

            <label>
              Buscar equipo

              <input
                value={deviceSearch}
                onChange={event =>
                  setDeviceSearch(event.target.value)
                }
                placeholder="Nombre del equipo"
                disabled={
                  creating ||
                  busySession
                }
              />
            </label>

            {/* DEVICE SELECTOR */}

            <label>
              Equipo Windows

              <select
                value={selectedDeviceId}
                disabled={
                  creating ||
                  busySession
                }
                onChange={event =>
                  setSelectedDeviceId(
                    event.target.value,
                  )
                }
              >
                <option value="">
                  Selecciona un equipo
                </option>

                {selectedDevice &&
                  !candidates.some(device =>
                    device.id === selectedDevice.id,
                  ) && (
                    <option
                      value={selectedDevice.id}
                    >
                      {selectedDevice.deviceName}
                    </option>
                  )}

                {candidates.map(device => (
                  <option
                    key={device.id}
                    value={device.id}
                  >
                    {device.deviceName}
                    {' · '}
                    {device.status}
                  </option>
                ))}
              </select>
            </label>

            <small>
              Hasta 100 resultados por búsqueda.
              Refina el nombre para localizar
              otros equipos.
            </small>

            {/* DEVICE DETAILS */}

            {selectedDevice && (
              <div className="wr-device-info">
                <strong>
                  {selectedDevice.deviceName}
                </strong>

                <span>
                  {selectedDevice.assignedUser ??
                    'Usuario sin asignar'}
                </span>

                <span>
                  {selectedDevice.ipAddress ??
                    'IP sin reportar'}
                </span>

                <span>
                  Último contacto:{' '}
                  {formatDate(
                    selectedDevice.lastSeenAtUtc,
                  )}
                </span>
              </div>
            )}

            {/* SESSION REASON */}

            <label>
              Motivo

              <textarea
                rows={3}
                maxLength={500}
                value={reason}
                disabled={
                  creating ||
                  busySession
                }
                onChange={event =>
                  setReason(
                    event.target.value,
                  )
                }
              />
            </label>

            {/* MAXIMUM DURATION */}

            <label>
              Duración máxima

              <select
                value={maximumDurationMinutes}
                disabled={
                  creating ||
                  busySession
                }
                onChange={event =>
                  setMaximumDurationMinutes(
                    Number(event.target.value),
                  )
                }
              >
                {[
                  30,
                  60,
                  120,
                  240,
                  480,
                ].map(minutes => (
                  <option
                    key={minutes}
                    value={minutes}
                  >
                    {minutes < 60
                      ? `${minutes} minutos`
                      : `${minutes / 60} horas`}
                  </option>
                ))}
              </select>
            </label>

            {/* PERMISSIONS */}

            <div className="wr-permissions">
              <label>
                <input
                  type="checkbox"
                  checked={allowMouse}
                  disabled={
                    creating ||
                    busySession
                  }
                  onChange={event =>
                    setAllowMouse(
                      event.target.checked,
                    )
                  }
                />
                Mouse
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowKeyboard}
                  disabled={
                    creating ||
                    busySession
                  }
                  onChange={event =>
                    setAllowKeyboard(
                      event.target.checked,
                    )
                  }
                />
                Teclado
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowClipboard}
                  disabled={
                    creating ||
                    busySession
                  }
                  onChange={event =>
                    setAllowClipboard(
                      event.target.checked,
                    )
                  }
                />
                Portapapeles
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowFileTransfer}
                  disabled={
                    creating ||
                    busySession
                  }
                  onChange={event =>
                    setAllowFileTransfer(
                      event.target.checked,
                    )
                  }
                />
                Archivos
              </label>
            </div>

            <small>
              Portapapeles y archivos requieren
              soporte funcional del agente;
              seleccionarlos no confirma disponibilidad.
            </small>

            {/* START SESSION */}

            <button
              className="wr-primary"
              disabled={
                !canManage ||
                creating ||
                !selectedDeviceId ||
                busySession ||
                !channelConnected ||
                reason.trim().length < 3
              }
              onClick={() =>
                void startSession()
              }
            >
              <Play size={16} />

              {creating
                ? 'Creando sesión…'
                : 'Conectar al equipo'}
            </button>

            {!canManage && (
              <small>
                Tu rol permite consultar;
                iniciar sesiones requiere remote.manage.
              </small>
            )}
          </section>

          {/* ================================================
              ACTIVE SESSIONS
              ================================================ */}

          <section className="wr-card">
            <div className="wr-card-title">
              <h2>Sesiones activas</h2>

              <span className="wr-chip">
                {activeSessions.length}
              </span>
            </div>

            <label>
              Sesión

              <select
                value={selectedSessionId}
                disabled={
                  loading ||
                  creating
                }
                onChange={event =>
                  void selectSession(
                    event.target.value,
                  )
                }
              >
                <option value="">
                  Selecciona una sesión
                </option>

                {activeSessions.map(session => (
                  <option
                    key={session.id}
                    value={session.id}
                  >
                    {windowsDevices.find(
                      device =>
                        device.id === session.deviceId,
                    )?.deviceName ??
                      session.deviceId.slice(0, 8)}
                    {' · '}
                    {statusLabel(session.status)}
                    {' · '}
                    {session.technicianName}
                  </option>
                ))}
              </select>
            </label>

            {!activeSessions.length && (
              <p className="wr-muted">
                No hay sesiones activas.
              </p>
            )}
          </section>
        </aside>

        {/* ==================================================
            REMOTE CONSOLE
            ================================================== */}

        <section className="wr-console">
          {/* ================================================
              TOOLBAR
              ================================================ */}

          <div className="wr-toolbar">
            <div>
              <strong>
                <Monitor size={17} />
                {selectedDevice?.deviceName ??
                  'Área de trabajo'}
              </strong>

              <span className="wr-chip">
                <ShieldCheck size={15} />

                {desktopProtected
                  ? 'Escritorio protegido'
                  : statusLabel(
                      activeSession?.status,
                    )}
              </span>
            </div>

            <div>
              {/* MOUSE STATUS */}

              <span
                className={
                  `wr-chip ${
                    connected &&
                    !desktopProtected &&
                    ownsControl &&
                    activeSession?.allowMouse
                      ? 'wr-online'
                      : ''
                  }`
                }
              >
                <MousePointer2 size={15} />
                Mouse
              </span>

              {/* KEYBOARD STATUS */}

              <span
                className={
                  `wr-chip ${
                    connected &&
                    !desktopProtected &&
                    ownsControl &&
                    activeSession?.allowKeyboard
                      ? 'wr-online'
                      : ''
                  }`
                }
              >
                <Keyboard size={15} />
                Teclado
              </span>

              {/* CONTROL OWNERSHIP */}

              {canManage && activeSession && (
                <button
                  className={
                    ownsControl
                      ? ''
                      : 'wr-primary'
                  }
                  disabled={
                    !channelConnected ||
                    changingControl ||
                    isTerminal(
                      activeSession.status,
                    )
                  }
                  onClick={() =>
                    void toggleControl()
                  }
                >
                  <MousePointer2 size={16} />

                  {changingControl
                    ? 'Actualizando…'
                    : ownsControl
                      ? 'Liberar control'
                      : controlState?.hasController
                        ? `Control: ${
                            controlState.displayName ??
                            'otro técnico'
                          }`
                        : 'Tomar control'}
                </button>
              )}

              {/* FULLSCREEN */}

              <button
                disabled={!activeSession}
                onClick={() =>
                  void toggleFullscreen()
                }
              >
                <Maximize2 size={16} />
                Pantalla completa
              </button>

              {/* TERMINATE */}

              <button
                className="wr-danger"
                disabled={
                  !canManage ||
                  !busySession ||
                  terminating
                }
                onClick={() =>
                  void endSession()
                }
              >
                <CircleStop size={16} />

                {terminating
                  ? 'Finalizando…'
                  : 'Finalizar'}
              </button>
            </div>
          </div>

          {/* ================================================
              REMOTE DESKTOP VIEWER
              ================================================ */}

          <div
            ref={viewerContainerRef}
            className={
              `wr-viewer ${
                frame && !desktopProtected
                  ? 'wr-viewer-live'
                  : ''
              }`
            }
          >
            {/* PROTECTED DESKTOP */}

            {desktopProtected && desktopState ? (
              <div
                className="wr-placeholder"
                role="status"
              >
                <div className="wr-placeholder-icon">
                  <ShieldCheck size={54} />
                </div>

                <h2>
                  Escritorio protegido de Windows
                </h2>

                <p>
                  {desktopMessage(desktopState)}
                </p>

                <div className="wr-guide">
                  <span>
                    Sesión conservada
                  </span>

                  <span>
                    Captura restringida
                  </span>

                  <span>
                    Recuperación automática
                  </span>
                </div>
              </div>
            ) : frame ? (
              /* NORMAL REMOTE VIEWER */

              <RemoteDesktopViewer
                frame={frame}
                width={frame.width}
                height={frame.height}
                connected={
                  connected &&
                  channelConnected
                }
                allowMouse={
                  ownsControl &&
                  !desktopProtected &&
                  (
                    activeSession?.allowMouse ??
                    false
                  )
                }
                allowKeyboard={
                  ownsControl &&
                  !desktopProtected &&
                  (
                    activeSession?.allowKeyboard ??
                    false
                  )
                }
                onPointerMove={pointerMove}
                onPointerButton={pointerButton}
                onWheel={wheel}
                onKeyboard={keyboard}
              />
            ) : (
              /* EMPTY VIEWER */

              <div className="wr-placeholder">
                <div className="wr-placeholder-icon">
                  <Monitor size={54} />
                </div>

                <h2>
                  {busySession
                    ? 'Esperando transmisión del equipo'
                    : 'Tu espacio de soporte remoto'}
                </h2>

                <p>
                  {busySession
                    ? (
                        'La sesión está registrada. ' +
                        'La imagen aparecerá cuando ' +
                        'el agente publique la transmisión.'
                      )
                    : (
                        'Busca un equipo Windows o ' +
                        'selecciona una sesión existente ' +
                        'para comenzar.'
                      )}
                </p>

                <div className="wr-guide">
                  <span>
                    1 · Seleccionar equipo
                  </span>

                  <span>
                    2 · Conectar
                  </span>

                  <span>
                    3 · Brindar asistencia
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* ================================================
              SESSION METRICS AND DETAILS
              ================================================ */}

          <footer className="wr-session-info">
            <span>
              <Clock3 size={15} />

              {activeSession
                ? formatDate(
                    activeSession.connectedAtUtc ??
                    activeSession.requestedAtUtc,
                  )
                : 'Sin sesión seleccionada'}
            </span>

            <span>
              Técnico:{' '}
              {activeSession?.technicianName ??
                '—'}
            </span>

            <span>
              {desktopProtected
                ? 'Escritorio protegido · Transmisión en pausa'
                : streamMetrics
                  ? (
                      `${streamMetrics.fps} FPS · ` +
                      `${streamMetrics.latencyMs} ms · ` +
                      `${streamMetrics.kilobitsPerSecond} kbps`
                    )
                  : 'Sin métricas de video'}
            </span>

            <span>
              {activeSession?.reason ??
                'Las acciones de sesión se registran en el servidor.'}
            </span>
          </footer>
        </section>
      </div>
    </main>
  )
}
