import {
  AlertTriangle,
  CheckCircle2,
  Command,
  ExternalLink,
  MonitorSmartphone,
  RefreshCw,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  Link,
} from 'react-router-dom'

import {
  androidEnterpriseApi,
} from '../../../../api/androidEnterpriseApi'

import {
  devicesApi,
} from '../../../../api/devicesApi'

import {
  useAuth,
} from '../../../../auth/AuthContext'

import {
  Permissions,
} from '../../../../auth/permissions'

import type {
  AndroidDeviceCapabilities,
  AndroidRemoteAction,
  AndroidRemoteActionRequest,
} from '../../../../types/androidEnterprise'

import type {
  AndroidDeviceDetails,
} from '../../../../types/device'

import {
  AndroidCommandPanel,
} from './AndroidCommandPanel'

import {
  AndroidDiagnosticsPanel,
} from './AndroidDiagnosticsPanel'

import {
  AndroidOperationsHistory,
} from './AndroidOperationsHistory'

import {
  AndroidRemoteSupportPanel,
} from './AndroidRemoteSupportPanel'

import type {
  AndroidCommandEntry,
} from './androidRemoteConsole.types'

import {
  actionLabel,
  androidActionCatalog,
  auditResultToCommandState,
  buildIdentity,
  consoleErrorMessage,
  findCapability,
  formatConsoleDate,
} from './androidRemoteConsole.utils'

import './androidRemoteConsole.css'

interface Props {
  deviceId: string
  deviceName?: string
}

const auditActionToCode: Record<string, string> = {
  Lock: 'lock',
  Reboot: 'reboot',
  ResetPassword: 'reset-password',
  StartLostMode: 'start-lost-mode',
  StopLostMode: 'stop-lost-mode',
  Wipe: 'wipe',
  Delete: 'delete',
}

function actionFromAuditName(action: string): string {
  const suffix = action.replace('Android.RemoteAction.', '')
  return auditActionToCode[suffix] ?? suffix
}

export function AndroidRemoteConsole({
  deviceId,
  deviceName,
}: Props) {
  const { hasPermission } = useAuth()

  const canExecute = hasPermission(
    Permissions.androidEnterprise.actions,
  )

  const canManage = hasPermission(
    Permissions.androidEnterprise.manage,
  )

  const [capabilities, setCapabilities] =
    useState<AndroidDeviceCapabilities | null>(null)

  const [details, setDetails] =
    useState<AndroidDeviceDetails | null>(null)

  const [auditEntries, setAuditEntries] =
    useState<AndroidCommandEntry[]>([])

  const [localEntries, setLocalEntries] =
    useState<AndroidCommandEntry[]>([])

  const [loading, setLoading] = useState(true)
  const [syncing, setSyncing] = useState(false)
  const [busyAction, setBusyAction] =
    useState<AndroidRemoteAction | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const load = useCallback(async () => {
    const [capabilitiesResult, detailsResult, auditResult] =
      await Promise.allSettled([
        androidEnterpriseApi.getDeviceCapabilities(deviceId),
        devicesApi.getAndroidDeviceDetails(deviceId),
        androidEnterpriseApi.getAudit({ page: 1, pageSize: 50 }),
      ])

    if (capabilitiesResult.status === 'fulfilled') {
      setCapabilities(capabilitiesResult.value)
    } else {
      setCapabilities(null)
      setError(consoleErrorMessage(capabilitiesResult.reason))
    }

    if (detailsResult.status === 'fulfilled') {
      setDetails(detailsResult.value)
    } else {
      setDetails(null)
    }

    if (auditResult.status === 'fulfilled') {
      const history = auditResult.value.items
        .filter(
          item =>
            item.action.startsWith('Android.RemoteAction.') &&
            item.targetId === deviceId,
        )
        .map<AndroidCommandEntry>(item => ({
          id: item.id,
          action: actionFromAuditName(item.action),
          label: actionLabel(actionFromAuditName(item.action)),
          state: auditResultToCommandState(item.result),
          requestedAtUtc: item.createdAtUtc,
          correlationId: item.correlationId,
          googleCommandName: null,
          detail: item.result,
          actor: item.actorName,
        }))

      setAuditEntries(history)
    }

    setLoading(false)
  }, [deviceId])

  useEffect(() => {
    void load()
  }, [load])

  const refresh = useCallback(() => {
    setLoading(true)
    setError(null)
    void load()
  }, [load])

  const identity = useMemo(
    () => buildIdentity(capabilities),
    [capabilities],
  )

  const entries = useMemo(
    () =>
      [...localEntries, ...auditEntries].sort((left, right) =>
        (right.requestedAtUtc ?? '').localeCompare(
          left.requestedAtUtc ?? '',
        ),
      ),
    [localEntries, auditEntries],
  )

  async function synchronize() {
    if (!canManage || syncing) return

    setSyncing(true)
    setError(null)
    setNotice(null)

    try {
      const result = await androidEnterpriseApi.synchronizeDevices()
      setNotice(
        `Inventario sincronizado: ${result.created} nuevos, ` +
          `${result.updated} actualizados, ${result.failed} errores.`,
      )
      await load()
    } catch (failure) {
      setError(consoleErrorMessage(failure))
    } finally {
      setSyncing(false)
    }
  }

  async function execute(action: AndroidRemoteAction) {
    if (!canExecute || busyAction) return

    const view = findCapability(capabilities, action)

    if (view.status !== 'Available') {
      setError(view.reason ?? 'La operación no está disponible.')
      return
    }

    const definition = androidActionCatalog.find(
      item => item.action === action,
    )

    const request: AndroidRemoteActionRequest = { action }

    if (action === 'reset-password') {
      const password = window.prompt(
        'Nueva contraseña para el dispositivo:',
      )

      if (!password || !password.trim()) {
        setError('Indica la nueva contraseña antes de emitir la solicitud.')
        return
      }

      request.newPassword = password
    }

    if (action === 'start-lost-mode') {
      const message = window.prompt(
        'Mensaje de modo perdido (opcional):',
      )

      if (message && message.trim()) {
        request.lostModeMessage = message.trim()
      }

      const phone = window.prompt(
        'Teléfono de recuperación (opcional):',
      )

      if (phone && phone.trim()) {
        request.lostModePhoneNumber = phone.trim()
      }
    }

    const target = deviceName?.trim() || 'el dispositivo'

    if (definition?.destructive) {
      const typed = window.prompt(
        `Acción irreversible. Escribe "${target}" para confirmar:`,
      )

      if ((typed ?? '').trim() !== target) {
        setError('Confirmación reforzada cancelada o no coincidente.')
        return
      }
    } else if (
      !window.confirm(
        `¿Confirmas "${definition?.label ?? action}" sobre ${target}?`,
      )
    ) {
      return
    }

    const pendingId = `local-${action}-${Date.now()}`

    setError(null)
    setNotice(null)
    setBusyAction(action)
    setLocalEntries(current => [
      {
        id: pendingId,
        action,
        label: actionLabel(action),
        state: 'executing',
        requestedAtUtc: new Date().toISOString(),
        correlationId: null,
        googleCommandName: null,
        detail: null,
        actor: 'Tú',
      },
      ...current,
    ])

    try {
      const result = await androidEnterpriseApi.executeDeviceAction(
        deviceId,
        request,
      )

      setLocalEntries(current =>
        current.map(entry =>
          entry.id === pendingId
            ? {
                ...entry,
                state: 'accepted',
                correlationId: result.correlationId,
                googleCommandName: result.googleCommandName,
                detail: 'Aceptado por Google. Verificación en dispositivo pendiente.',
              }
            : entry,
        ),
      )

      setNotice(
        'Solicitud emitida. La ejecución real se confirma con la próxima ' +
          'sincronización de estado del dispositivo.',
      )

      await load()
    } catch (failure) {
      const message = consoleErrorMessage(failure)

      setLocalEntries(current =>
        current.map(entry =>
          entry.id === pendingId
            ? { ...entry, state: 'failed', detail: message }
            : entry,
        ),
      )

      setError(message)
    } finally {
      setBusyAction(null)
    }
  }

  return (
    <section className="arc" aria-label="Centro de control remoto Android">
      <header className="arc-hero">
        <div className="arc-hero__identity">
          <Smartphone size={26} />
          <div>
            <div className="arc-eyebrow">
              <Command size={13} />
              ANDROID ENTERPRISE · CENTRO DE CONTROL REMOTO
            </div>
            <h2>{deviceName ?? 'Dispositivo Android'}</h2>
            <p>
              {identity.manufacturer} · {identity.model} · Android{' '}
              {identity.androidVersion} ({identity.apiLevel})
            </p>
          </div>
        </div>

        <div className="arc-hero__status">
          <span className="arc-chip">{identity.managementMode}</span>
          <span className="arc-chip">{identity.ownership}</span>
          <button
            type="button"
            className="arc-ghost-button"
            onClick={refresh}
            disabled={loading}
          >
            <RefreshCw size={14} />
            {loading ? 'Cargando...' : 'Actualizar'}
          </button>
        </div>
      </header>

      {error && (
        <div className="arc-inline-note arc-inline-note--error" role="alert">
          <AlertTriangle size={16} />
          {error}
        </div>
      )}

      {notice && (
        <div
          className="arc-inline-note arc-inline-note--success"
          role="status"
        >
          <CheckCircle2 size={16} />
          {notice}
        </div>
      )}

      <div className="arc-layout">
        <div className="arc-main">
          <AndroidCommandPanel
            capabilities={capabilities}
            canExecute={canExecute}
            busyAction={busyAction}
            onExecute={action => void execute(action)}
          />

          <AndroidDiagnosticsPanel
            capabilities={capabilities}
            details={details}
            loading={loading}
            error={null}
            onRefresh={refresh}
          />

          <AndroidOperationsHistory
            entries={entries}
            loading={loading}
            onRefresh={refresh}
          />
        </div>

        <aside className="arc-side">
          <AndroidRemoteSupportPanel capabilities={capabilities} />

          <section className="arc-card" aria-label="Módulos de gestión">
            <header className="arc-card__head">
              <div>
                <h3>Módulos de gestión</h3>
                <p>Aplicaciones, políticas, kiosk y launcher.</p>
              </div>
            </header>

            <nav className="arc-module-links">
              <Link to="/apps?workspace=android">
                <MonitorSmartphone size={16} />
                Aplicaciones
                <ExternalLink size={13} />
              </Link>
              <Link to="/policies?workspace=android">
                <ShieldCheck size={16} />
                Políticas y restricciones
                <ExternalLink size={13} />
              </Link>
              <Link to="/kiosk?workspace=android">
                <Command size={16} />
                Kiosk / Launcher
                <ExternalLink size={13} />
              </Link>
              <Link to="/android/titan-launcher?workspace=android">
                <Smartphone size={16} />
                Titan Launcher
                <ExternalLink size={13} />
              </Link>
            </nav>

            <button
              type="button"
              className="arc-primary-button"
              disabled={!canManage || syncing}
              onClick={() => void synchronize()}
            >
              <RefreshCw size={14} />
              {syncing ? 'Sincronizando...' : 'Sincronizar inventario'}
            </button>
          </section>

          <section className="arc-card">
            <header className="arc-card__head">
              <div>
                <h3>Resumen de compatibilidad</h3>
                <p>Última lectura: {formatConsoleDate(new Date().toISOString())}</p>
              </div>
            </header>

            <dl className="arc-summary-list">
              <div>
                <dt>Fabricante</dt>
                <dd>{identity.manufacturer}</dd>
              </div>
              <div>
                <dt>Modelo</dt>
                <dd>{identity.model}</dd>
              </div>
              <div>
                <dt>Parche de seguridad</dt>
                <dd>{identity.securityPatch}</dd>
              </div>
              <div>
                <dt>Google Mobile Services</dt>
                <dd>
                  {capabilities?.hasGoogleMobileServices
                    ? 'Disponible'
                    : 'Pendiente de validación'}
                </dd>
              </div>
            </dl>
          </section>
        </aside>
      </div>
    </section>
  )
}
