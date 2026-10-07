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
import type { RemoteSession } from '../../api/remoteSupportApi'
import {
  RemoteSupportSignalRClient,
} from '../../api/remoteSupportSignalR'
import type {
  RemoteFrame,
  RemoteSessionChanged,
} from '../../api/remoteSupportSignalR'
import RemoteDesktopViewer from './RemoteDesktopViewer'
import type { DeviceListItem } from '../../types/device'
import { useAuth } from '../../auth/AuthContext'
import './RemotePage.css'

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
  return status ? names[status] ?? status : 'Sin sesión'
}

function isTerminal(status: string): boolean {
  return ['Completed', 'Failed', 'Expired', 'Cancelled']
    .includes(status)
}

export function RemotePage() {
  const { user } = useAuth()
  const canManage =
    user?.permissions?.includes('remote.manage') ?? false

  const [deviceSearch, setDeviceSearch] = useState('')
  const loadGeneration = useRef(0)
  const signalRRef =
    useRef<RemoteSupportSignalRClient | null>(null)
  const selectedSessionIdRef = useRef('')
  const viewerContainerRef = useRef<HTMLDivElement | null>(null)

  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [sessions, setSessions] = useState<RemoteSession[]>([])
  const [activeSession, setActiveSession] =
    useState<RemoteSession | null>(null)
  const [selectedSessionId, setSelectedSessionId] = useState('')
  const [selectedDeviceId, setSelectedDeviceId] = useState('')

  const [reason, setReason] = useState('Soporte técnico remoto')
  const [maximumDurationMinutes, setMaximumDurationMinutes] =
    useState(120)
  const [allowMouse, setAllowMouse] = useState(true)
  const [allowKeyboard, setAllowKeyboard] = useState(true)
  const [allowClipboard, setAllowClipboard] = useState(false)
  const [allowFileTransfer, setAllowFileTransfer] = useState(false)

  const [frame, setFrame] = useState<RemoteFrame | null>(null)
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const [terminating, setTerminating] = useState(false)
  const [channelConnected, setChannelConnected] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    selectedSessionIdRef.current = selectedSessionId
  }, [selectedSessionId])

  const frameUrl = useMemo(
    () => frame
      ? `data:${frame.mimeType};base64,${frame.base64Data}`
      : null,
    [frame],
  )

  const windowsDevices = useMemo(
    () => devices.filter(x =>
      x.platform === 'Windows' && x.isManaged,
    ),
    [devices],
  )

  const activeSessions = useMemo(
    () => sessions.filter(x => !isTerminal(x.status)),
    [sessions],
  )

  const selectedDevice = useMemo(
    () => windowsDevices.find(x =>
      x.id === selectedDeviceId,
    ) ?? null,
    [windowsDevices, selectedDeviceId],
  )

  const loadData = useCallback(async () => {
    const request = ++loadGeneration.current

    try {
      const [deviceResult, sessionResult] = await Promise.all([
        devicesApi.getDevices({
          platform: 'Windows',
          search: deviceSearch,
          page: 1,
          pageSize: 100,
        }),
        listRemoteSessions(200),
      ])

      if (request !== loadGeneration.current) return

      setDevices(deviceResult.items)
      setSessions(sessionResult)
    } catch (requestError) {
      if (request !== loadGeneration.current) return
      console.error(requestError)
      setError(
        'No fue posible cargar los dispositivos o las sesiones remotas.',
      )
    } finally {
      if (request === loadGeneration.current) {
        setLoading(false)
      }
    }
  }, [deviceSearch])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData()
    }, 300)
    return () => window.clearTimeout(timer)
  }, [loadData])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void loadData()
    }, 15000)
    return () => window.clearInterval(timer)
  }, [loadData])

  const refreshActiveSession = useCallback(
    async (sessionId: string) => {
      try {
        const updated = await getRemoteSession(sessionId)

        if (selectedSessionIdRef.current === updated.id) {
          setActiveSession(updated)
        }

        setSessions(current => {
          if (!current.some(x => x.id === updated.id)) {
            return [updated, ...current]
          }
          return current.map(x =>
            x.id === updated.id ? updated : x,
          )
        })
      } catch (requestError) {
        console.error(
          'No fue posible actualizar la sesión remota.',
          requestError,
        )
      }
    },
    [],
  )

  useEffect(() => {
    let disposed = false
    const client = new RemoteSupportSignalRClient()
    signalRRef.current = client

    const joinCurrentSession = async () => {
      const sessionId = selectedSessionIdRef.current
      if (!sessionId) return

      try {
        await client.joinSession(sessionId)
      } catch (joinError) {
        console.error('JoinSession falló.', joinError)
        if (!disposed) {
          setError(
            'El canal está conectado, pero no fue posible ' +
            'unirse a la sesión. Revisa permisos y estado.',
          )
        }
      }
    }

    void client.connect({
      onFrame: nextFrame => {
        if (
          nextFrame.sessionId !== selectedSessionIdRef.current
        ) return

        setFrame(current => {
          if (
            current &&
            current.sessionId === nextFrame.sessionId &&
            nextFrame.sequence <= current.sequence
          ) return current

          return nextFrame
        })
      },
      onSessionChanged: (update: RemoteSessionChanged) => {
        void refreshActiveSession(update.sessionId)
      },
      onReconnecting: () => {
        if (!disposed) setChannelConnected(false)
      },
      onReconnected: () => {
        if (disposed) return
        setChannelConnected(true)
        void joinCurrentSession()
      },
      onClosed: closeError => {
        if (disposed) return
        setChannelConnected(false)
        if (closeError) {
          console.error('Canal remoto cerrado.', closeError)
        }
      },
    }).then(async () => {
      if (disposed) return
      setChannelConnected(true)
      await joinCurrentSession()
    }).catch(connectionError => {
      if (disposed) return
      console.error(connectionError)
      setChannelConnected(false)
      setError(
        'No fue posible establecer el canal de soporte remoto.',
      )
    })

    return () => {
      disposed = true
      if (signalRRef.current === client) {
        signalRRef.current = null
      }
      void client.disconnect()
    }
  }, [refreshActiveSession])

  const selectSession = async (sessionId: string) => {
    try {
      setError(null)
      setFrame(null)
      setActiveSession(null)

      const previousSessionId = selectedSessionIdRef.current

      if (
        previousSessionId &&
        previousSessionId !== sessionId &&
        signalRRef.current
      ) {
        try {
          await signalRRef.current.leaveSession(previousSessionId)
        } catch (leaveError) {
          console.warn(
            'No fue posible abandonar la sesión anterior.',
            leaveError,
          )
        }
      }

      selectedSessionIdRef.current = sessionId
      setSelectedSessionId(sessionId)

      if (!sessionId) return

      const session = await getRemoteSession(sessionId)

      if (selectedSessionIdRef.current !== sessionId) return

      setActiveSession(session)
      setSelectedDeviceId(session.deviceId)

      if (signalRRef.current && channelConnected) {
        try {
          await signalRRef.current.joinSession(sessionId)
        } catch (joinError) {
          console.error(joinError)
          setError(
            'El canal está disponible, pero no fue posible ' +
            'unirse a esta sesión remota.',
          )
        }
      }
    } catch {
      if (selectedSessionIdRef.current === sessionId) {
        setError('No fue posible abrir la sesión seleccionada.')
      }
    }
  }

  const startSession = async () => {
    if (
      !canManage ||
      creating ||
      (activeSession && !isTerminal(activeSession.status))
    ) return

    if (!selectedDeviceId) {
      setError('Selecciona un equipo Windows.')
      return
    }

    if (reason.trim().length < 3) {
      setError('Indica el motivo de la conexión remota.')
      return
    }

    try {
      setCreating(true)
      setError(null)
      setFrame(null)

      const session = await createRemoteSession({
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
        ...current.filter(x => x.id !== session.id),
      ])

      selectedSessionIdRef.current = session.id
      setSelectedSessionId(session.id)
      setActiveSession(session)

      if (signalRRef.current && channelConnected) {
        try {
          await signalRRef.current.joinSession(session.id)
        } catch (joinError) {
          const message = joinError instanceof Error
            ? joinError.message
            : String(joinError)

          setError(
            `La sesión fue creada, pero no se pudo unir el canal. ${message}`,
          )
        }
      }
    } catch (requestError) {
      const message = requestError instanceof Error
        ? requestError.message
        : String(requestError)
      setError(`No fue posible crear la sesión remota. ${message}`)
    } finally {
      setCreating(false)
    }
  }

  const endSession = async () => {
    if (!canManage || !activeSession || terminating) return

    const sessionId = activeSession.id

    try {
      setTerminating(true)
      setError(null)
      await terminateRemoteSession(sessionId)
      setFrame(null)
      await refreshActiveSession(sessionId)
      await loadData()
    } catch {
      setError('No fue posible finalizar la sesión remota.')
    } finally {
      setTerminating(false)
    }
  }

  const pointerMove = useCallback((x: number, y: number) => {
    if (
      !activeSession ||
      !signalRRef.current ||
      !channelConnected ||
      activeSession.status !== 'Connected'
    ) return

    void signalRRef.current
      .pointerMove(activeSession.id, x, y)
      .catch(console.error)
  }, [activeSession, channelConnected])

  const pointerButton = useCallback((
    action: 'left-down' | 'left-up' | 'right-down' | 'right-up',
  ) => {
    if (
      !activeSession ||
      !signalRRef.current ||
      !channelConnected ||
      activeSession.status !== 'Connected'
    ) return

    void signalRRef.current
      .pointerButton(activeSession.id, action)
      .catch(console.error)
  }, [activeSession, channelConnected])

  const wheel = useCallback((delta: number) => {
    if (
      !activeSession ||
      !signalRRef.current ||
      !channelConnected ||
      activeSession.status !== 'Connected'
    ) return

    void signalRRef.current
      .pointerWheel(activeSession.id, delta)
      .catch(console.error)
  }, [activeSession, channelConnected])

  const keyboard = useCallback((
    virtualKey: number,
    keyDown: boolean,
  ) => {
    if (
      !activeSession ||
      !signalRRef.current ||
      !channelConnected ||
      activeSession.status !== 'Connected'
    ) return

    void signalRRef.current
      .keyboard(activeSession.id, virtualKey, keyDown)
      .catch(console.error)
  }, [activeSession, channelConnected])

  const toggleFullscreen = async () => {
    const element = viewerContainerRef.current
    if (!element) return

    if (document.fullscreenElement) {
      await document.exitFullscreen()
      return
    }

    try {
      await element.requestFullscreen()
    } catch {
      setError('No fue posible activar pantalla completa.')
    }
  }

  const connected = activeSession?.status === 'Connected'
  const busySession =
    !!activeSession && !isTerminal(activeSession.status)

  const candidates = windowsDevices.filter(x =>
    !deviceSearch.trim() ||
    `${x.deviceName} ${x.assignedUser ?? ''} ${x.ipAddress ?? ''}`
      .toLowerCase()
      .includes(deviceSearch.trim().toLowerCase()),
  )

  return (
    <main className="remote-page wr">
      <header className="wr-header">
        <div>
          <span className="wr-eyebrow">
            WINDOWS · CONSOLA DE SOPORTE
          </span>
          <h1><RadioTower size={28} /> Soporte remoto</h1>
          <p>
            Selecciona un equipo, inicia la sesión
            y trabaja desde una consola central.
          </p>
        </div>

        <div className="wr-header-actions">
          <span className={
            `wr-chip ${channelConnected ? 'wr-online' : 'wr-offline'}`
          }>
            <RadioTower size={15} />
            {channelConnected
              ? 'Canal conectado'
              : 'Canal desconectado'}
          </span>

          <button
            onClick={() => void loadData()}
            disabled={creating}
          >
            <RefreshCw size={16} /> Actualizar
          </button>
        </div>
      </header>

      {error && (
        <div className="wr-error" role="alert">{error}</div>
      )}

      <div className="wr-layout">
        <aside className="wr-sidebar">
          <section className="wr-card">
            <div className="wr-card-title">
              <h2>Nueva conexión</h2>
              <Monitor size={19} />
            </div>

            <label>
              Buscar equipo
              <input
                value={deviceSearch}
                onChange={e => setDeviceSearch(e.target.value)}
                placeholder="Nombre del equipo"
                disabled={creating || busySession}
              />
            </label>

            <label>
              Equipo Windows
              <select
                value={selectedDeviceId}
                disabled={creating || busySession}
                onChange={e => setSelectedDeviceId(e.target.value)}
              >
                <option value="">Selecciona un equipo</option>

                {selectedDevice &&
                  !candidates.some(x =>
                    x.id === selectedDevice.id,
                  ) && (
                    <option value={selectedDevice.id}>
                      {selectedDevice.deviceName}
                    </option>
                  )}

                {candidates.map(x => (
                  <option key={x.id} value={x.id}>
                    {x.deviceName} · {x.status}
                  </option>
                ))}
              </select>
            </label>

            <small>
              Hasta 100 resultados por búsqueda. Refina el nombre
              para localizar otros equipos.
            </small>

            {selectedDevice && (
              <div className="wr-device-info">
                <strong>{selectedDevice.deviceName}</strong>
                <span>
                  {selectedDevice.assignedUser ??
                    'Usuario sin asignar'}
                </span>
                <span>
                  {selectedDevice.ipAddress ?? 'IP sin reportar'}
                </span>
                <span>
                  Último contacto:{' '}
                  {formatDate(selectedDevice.lastSeenAtUtc)}
                </span>
              </div>
            )}

            <label>
              Motivo
              <textarea
                rows={3}
                maxLength={500}
                value={reason}
                disabled={creating || busySession}
                onChange={e => setReason(e.target.value)}
              />
            </label>

            <label>
              Duración máxima
              <select
                value={maximumDurationMinutes}
                disabled={creating || busySession}
                onChange={e =>
                  setMaximumDurationMinutes(Number(e.target.value))
                }
              >
                {[30, 60, 120, 240, 480].map(n => (
                  <option key={n} value={n}>
                    {n < 60 ? `${n} minutos` : `${n / 60} horas`}
                  </option>
                ))}
              </select>
            </label>

            <div className="wr-permissions">
              <label>
                <input
                  type="checkbox"
                  checked={allowMouse}
                  disabled={creating || busySession}
                  onChange={e => setAllowMouse(e.target.checked)}
                />
                Mouse
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowKeyboard}
                  disabled={creating || busySession}
                  onChange={e =>
                    setAllowKeyboard(e.target.checked)
                  }
                />
                Teclado
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowClipboard}
                  disabled={creating || busySession}
                  onChange={e =>
                    setAllowClipboard(e.target.checked)
                  }
                />
                Portapapeles
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowFileTransfer}
                  disabled={creating || busySession}
                  onChange={e =>
                    setAllowFileTransfer(e.target.checked)
                  }
                />
                Archivos
              </label>
            </div>

            <small>
              Portapapeles y archivos requieren soporte funcional
              del agente; seleccionarlos no confirma disponibilidad.
            </small>

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
              onClick={() => void startSession()}
            >
              <Play size={16} />
              {creating
                ? 'Creando sesión…'
                : 'Conectar al equipo'}
            </button>

            {!canManage && (
              <small>
                Tu rol permite consultar; iniciar sesiones
                requiere remote.manage.
              </small>
            )}
          </section>

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
                disabled={loading || creating}
                onChange={e =>
                  void selectSession(e.target.value)
                }
              >
                <option value="">Selecciona una sesión</option>

                {activeSessions.map(x => (
                  <option key={x.id} value={x.id}>
                    {windowsDevices.find(d =>
                      d.id === x.deviceId,
                    )?.deviceName ?? x.deviceId.slice(0, 8)}
                    {' · '}{statusLabel(x.status)}
                    {' · '}{x.technicianName}
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

        <section className="wr-console">
          <div className="wr-toolbar">
            <div>
              <strong>
                <Monitor size={17} />
                {selectedDevice?.deviceName ?? 'Área de trabajo'}
              </strong>
              <span className="wr-chip">
                <ShieldCheck size={15} />
                {statusLabel(activeSession?.status)}
              </span>
            </div>

            <div>
              <span className={
                `wr-chip ${
                  connected && activeSession?.allowMouse
                    ? 'wr-online'
                    : ''
                }`
              }>
                <MousePointer2 size={15} /> Mouse
              </span>

              <span className={
                `wr-chip ${
                  connected && activeSession?.allowKeyboard
                    ? 'wr-online'
                    : ''
                }`
              }>
                <Keyboard size={15} /> Teclado
              </span>

              <button
                disabled={!activeSession}
                onClick={() => void toggleFullscreen()}
              >
                <Maximize2 size={16} /> Pantalla completa
              </button>

              <button
                className="wr-danger"
                disabled={
                  !canManage || !busySession || terminating
                }
                onClick={() => void endSession()}
              >
                <CircleStop size={16} />
                {terminating ? 'Finalizando…' : 'Finalizar'}
              </button>
            </div>
          </div>

          <div
            ref={viewerContainerRef}
            className={
              `wr-viewer ${frameUrl ? 'wr-viewer-live' : ''}`
            }
          >
            {frameUrl ? (
              <RemoteDesktopViewer
                frameUrl={frameUrl}
                width={frame?.width}
                height={frame?.height}
                connected={connected}
                allowMouse={activeSession?.allowMouse ?? false}
                allowKeyboard={
                  activeSession?.allowKeyboard ?? false
                }
                onPointerMove={pointerMove}
                onPointerButton={pointerButton}
                onWheel={wheel}
                onKeyboard={keyboard}
              />
            ) : (
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
                    ? 'La sesión está registrada. La imagen aparecerá cuando el agente publique la transmisión.'
                    : 'Busca un equipo Windows o selecciona una sesión existente para comenzar.'}
                </p>

                <div className="wr-guide">
                  <span>1 · Seleccionar equipo</span>
                  <span>2 · Conectar</span>
                  <span>3 · Brindar asistencia</span>
                </div>
              </div>
            )}
          </div>

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
              Técnico: {activeSession?.technicianName ?? '—'}
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