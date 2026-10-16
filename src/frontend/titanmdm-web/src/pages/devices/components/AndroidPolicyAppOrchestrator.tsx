
import {
  CheckCircle2,
  ClipboardCheck,
  CloudUpload,
  PackageCheck,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Smartphone,
  Trash2,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'
import {
  policiesApi,
  type AndroidPolicyAssignment,
  type AndroidPolicyPublication,
  type Policy,
  type PolicyDetails,
} from '../../../api/policiesApi'
import { androidEnterpriseApi } from '../../../api/androidEnterpriseApi'
import { useAuth } from '../../../auth/AuthContext'
import type { AndroidInventoryItem } from '../../../types/androidEnterprise'
import './AndroidPolicyAppOrchestrator.css'

type InstallType =
  | 'AVAILABLE'
  | 'FORCE_INSTALLED'
  | 'REQUIRED_FOR_SETUP'
  | 'BLOCKED'

interface CorporateApp {
  packageName: string
  label: string
  installType: InstallType
  visible: boolean
}

type JsonObject = Record<string, unknown>

const PACKAGE_PATTERN =
  /^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)+$/

function object(value: unknown): JsonObject {
  return value !== null &&
    typeof value === 'object' &&
    !Array.isArray(value)
    ? value as JsonObject
    : {}
}

function getError(error: unknown): string {
  if (error && typeof error === 'object' && 'response' in error) {
    const response = (
      error as {
        response?: { data?: { message?: string; code?: string } }
      }
    ).response
    if (response?.data?.message) return response.data.message
    if (response?.data?.code) return response.data.code
  }
  return error instanceof Error
    ? error.message
    : 'No se pudo completar la operación.'
}

function readApps(configurationJson: string): CorporateApp[] {
  const root = object(JSON.parse(configurationJson))
  const android = object(root.android)
  const applications = object(android.applications)
  const packages = applications.packages

  if (!Array.isArray(packages)) return []

  return packages.flatMap((entry: unknown) => {
    const app = object(entry)
    if (typeof app.packageName !== 'string') return []

    const packageName = app.packageName.trim()
    if (!PACKAGE_PATTERN.test(packageName)) return []

    const validTypes: InstallType[] = [
      'AVAILABLE',
      'FORCE_INSTALLED',
      'REQUIRED_FOR_SETUP',
      'BLOCKED',
    ]

    const rawType = app.installType
    const installType: InstallType =
      validTypes.includes(rawType as InstallType)
        ? rawType as InstallType
        : 'AVAILABLE'

    return [{
      packageName,
      label: typeof app.label === 'string'
        ? app.label
        : packageName,
      installType,
      visible: app.visible !== false && installType !== 'BLOCKED',
    }]
  })
}

function updateApps(
  configurationJson: string,
  apps: CorporateApp[],
): string {
  const root = object(JSON.parse(configurationJson))
  const android = object(root.android)
  const applications = object(android.applications)

  // Se conservan todas las demás secciones de política:
  // seguridad, kiosk, launcher, red, restricciones, etc.
  return JSON.stringify({
    ...root,
    android: {
      ...android,
      applications: {
        ...applications,
        packages: apps.map(app => ({
          packageName: app.packageName,
          label: app.label,
          installType: app.installType,
          visible: app.visible,
        })),
      },
    },
  }, null, 2)
}

function dateLabel(value?: string | null): string {
  if (!value) return 'Pendiente'
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString('es-DO')
}

export function AndroidPolicyAppOrchestrator() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission('policies.manage')
  const canView = hasPermission('policies.view')

  const [policies, setPolicies] = useState<Policy[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [policy, setPolicy] = useState<PolicyDetails | null>(null)
  const [publication, setPublication] =
    useState<AndroidPolicyPublication | null>(null)
  const [apps, setApps] = useState<CorporateApp[]>([])
  const [originalApps, setOriginalApps] = useState<CorporateApp[]>([])

  const [devices, setDevices] = useState<AndroidInventoryItem[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [assignment, setAssignment] =
    useState<AndroidPolicyAssignment | null>(null)

  const [packageName, setPackageName] = useState('')
  const [appLabel, setAppLabel] = useState('')
  const [installType, setInstallType] =
    useState<InstallType>('AVAILABLE')
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const dirty = useMemo(
    () => JSON.stringify(apps) !== JSON.stringify(originalApps),
    [apps, originalApps],
  )

  const filteredApps = useMemo(() => {
    const term = search.toLowerCase().trim()
    return apps.filter(app =>
      !term ||
      app.packageName.toLowerCase().includes(term) ||
      app.label.toLowerCase().includes(term),
    )
  }, [apps, search])

  const refreshPolicies = useCallback(async () => {
    if (!canView) return
    const result = await policiesApi.getAll('Android')
    setPolicies(result)
    setSelectedId(current =>
      result.some(item => item.id === current)
        ? current
        : result[0]?.id ?? '',
    )
  }, [canView])

  const refreshDevices = useCallback(async () => {
    const page = await androidEnterpriseApi.getInventory({
      page: 1,
      pageSize: 100,
    })
    setDevices(
      page.items.filter(
        item => item.isManaged && !item.isDeletedInGoogle,
      ),
    )
  }, [])

  const loadPolicy = useCallback(async (id: string) => {
    if (!id) {
      setPolicy(null)
      setPublication(null)
      setApps([])
      setOriginalApps([])
      return
    }

    setBusy('loading')
    setError('')
    try {
      const [details, currentPublication] = await Promise.all([
        policiesApi.getById(id),
        policiesApi.getAndroidPublication(id),
      ])
      const currentApps = readApps(details.configurationJson)
      setPolicy(details)
      setPublication(currentPublication)
      setApps(currentApps)
      setOriginalApps(currentApps)
      setAssignment(null)
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }, [])

  useEffect(() => {
    void refreshPolicies().catch(error => setError(getError(error)))
    void refreshDevices().catch(error => setError(getError(error)))
  }, [refreshPolicies, refreshDevices])

  useEffect(() => {
    void loadPolicy(selectedId)
  }, [selectedId, loadPolicy])

  function addApp() {
    const nextPackage = packageName.trim()

    if (!PACKAGE_PATTERN.test(nextPackage)) {
      setError('Introduce un paquete Android válido, por ejemplo com.microsoft.teams.')
      return
    }

    if (apps.some(app => app.packageName.toLowerCase() ===
      nextPackage.toLowerCase())) {
      setError('La aplicación ya pertenece a esta política.')
      return
    }

    setApps(current => [...current, {
      packageName: nextPackage,
      label: appLabel.trim() || nextPackage,
      installType,
      visible: installType !== 'BLOCKED',
    }])

    setPackageName('')
    setAppLabel('')
    setInstallType('AVAILABLE')
    setError('')
    setSuccess('')
  }

  function changeApp(index: number, patch: Partial<CorporateApp>) {
    setApps(current => current.map((app, position) =>
      position === index ? { ...app, ...patch } : app,
    ))
  }

  async function savePolicy() {
    if (!policy || !canManage || busy) return

    setBusy('saving')
    setError('')
    setSuccess('')

    try {
      const updated = await policiesApi.update(policy.id, {
        name: policy.name,
        description: policy.description,
        configurationJson: updateApps(policy.configurationJson, apps),
      })

      const normalizedApps = readApps(updated.configurationJson)
      setPolicy(updated)
      setApps(normalizedApps)
      setOriginalApps(normalizedApps)

      // Una versión guardada aún NO está publicada en Google.
      setPublication(await policiesApi.getAndroidPublication(policy.id))
      setSuccess('Política guardada. Revisa y publica la versión actual.')
      await refreshPolicies()
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }

  async function publishPolicy() {
    if (!policy || !canManage || dirty || busy) return

    if (!window.confirm(
      '¿Publicar esta versión en Android Enterprise? ' +
      'Google podría aplicar cambios a dispositivos ya asignados.',
    )) return

    setBusy('publishing')
    setError('')
    setSuccess('')

    try {
      const result = await policiesApi.publishAndroid(policy.id)
      setPublication(await policiesApi.getAndroidPublication(policy.id))
      setSuccess(
        `Publicación enviada a Google: ${result.status}. ` +
        'Verifica la publicación antes de asignarla.',
      )
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }

  async function verifyPublication() {
    if (!policy || busy) return

    setBusy('verifying')
    setError('')
    setSuccess('')

    try {
      const verification =
        await policiesApi.verifyAndroidPublication(policy.id)
      setPublication(await policiesApi.getAndroidPublication(policy.id))
      setSuccess(
        verification.existsInGoogle
          ? 'La política existe en Google Android Management.'
          : 'Google todavía no confirma esta política.',
      )
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }

  async function assignPolicy() {
    if (
      !policy ||
      !canManage ||
      !selectedDeviceId ||
      dirty ||
      publication?.status !== 'Published' ||
      publication.policyVersion !== policy.currentVersion ||
      busy
    ) return

    const device = devices.find(item => item.deviceId === selectedDeviceId)
    if (!device) return

    if (!window.confirm(
      `¿Asignar "${policy.name}" al dispositivo "${device.deviceName}"?`,
    )) return

    setBusy('assigning')
    setError('')
    setSuccess('')

    try {
      const result =
        await policiesApi.assignAndroid(policy.id, selectedDeviceId)
      setAssignment(result)
      setSuccess(
        `Política asignada. Estado: ${result.assignmentStatus}. ` +
        'La aplicación final debe confirmarse mediante sincronización.',
      )
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }

  async function checkAssignment() {
    if (!policy || !selectedDeviceId || busy) return

    setBusy('checking')
    setError('')

    try {
      const result = await policiesApi.getAndroidAssignment(
        policy.id,
        selectedDeviceId,
      )
      setAssignment(result)
      setSuccess(
        result
          ? `Estado de asignación: ${result.assignmentStatus}`
          : 'Todavía no existe asignación para este dispositivo.',
      )
    } catch (failure) {
      setError(getError(failure))
    } finally {
      setBusy('')
    }
  }

  if (!canView) {
    return (
      <div className="apo-error">
        No tienes permiso para consultar políticas empresariales.
      </div>
    )
  }

  return (
    <section className="apo">
      <header className="apo-header">
        <div>
          <span className="apo-eyebrow">TITANMDM · ANDROID ENTERPRISE</span>
          <h2>Policy & App Orchestrator</h2>
          <p>
            Lista blanca, aplicaciones obligatorias, publicación y
            asignación real de políticas a la flota Android.
          </p>
        </div>

        <button type="button" disabled={!!busy}
          onClick={() => void Promise.all([
            refreshPolicies(),
            refreshDevices(),
          ]).catch(failure => setError(getError(failure)))}>
          <RefreshCw size={16} /> Actualizar catálogo
        </button>
      </header>

      {error && <div className="apo-error" role="alert">{error}</div>}
      {success && (
        <div className="apo-success" role="status">
          <CheckCircle2 size={17} /> {success}
        </div>
      )}

      <div className="apo-stats">
        <Stat label="Políticas Android" value={policies.length} icon={ShieldCheck} />
        <Stat label="Apps configuradas" value={apps.length} icon={PackageCheck} />
        <Stat label="Instalación obligatoria"
          value={apps.filter(app =>
            app.installType === 'FORCE_INSTALLED' ||
            app.installType === 'REQUIRED_FOR_SETUP').length}
          icon={CloudUpload} />
        <Stat label="Dispositivos consultados" value={devices.length}
          icon={Smartphone} />
      </div>

      <div className="apo-grid">
        <section className="apo-card">
          <div className="apo-title">
            <ShieldCheck size={18} />
            <div>
              <h3>Política corporativa</h3>
              <p>Selecciona la versión que deseas configurar.</p>
            </div>
          </div>

          <label className="apo-field">
            <span>Política Android</span>
            <select
              value={selectedId}
              disabled={!!busy}
              onChange={event => {
                if (dirty && !window.confirm(
                  'Hay cambios sin guardar. ¿Descartarlos?',
                )) return
                setSelectedId(event.target.value)
                setAssignment(null)
              }}
            >
              <option value="">Selecciona una política</option>
              {policies.map(item => (
                <option key={item.id} value={item.id}>
                  {item.name} · v{item.currentVersion} · {item.status}
                </option>
              ))}
            </select>
          </label>

          {policy && (
            <div className="apo-policy-meta">
              <div><span>Estado TitanMDM</span><strong>{policy.status}</strong></div>
              <div><span>Versión</span><strong>v{policy.currentVersion}</strong></div>
              <div><span>Google</span>
                <strong>{publication?.status ?? 'Sin publicar'}</strong>
              </div>
              <div><span>Versión publicada</span>
                <strong>{publication?.policyVersion ?? '—'}</strong>
              </div>
            </div>
          )}

          {dirty && (
            <p className="apo-warning">
              Existen modificaciones locales. Guarda antes de publicar.
            </p>
          )}

          <div className="apo-actions">
            <button type="button" disabled={!policy || !canManage || !!busy || !dirty}
              onClick={() => void savePolicy()}>
              <ClipboardCheck size={16} />
              {busy === 'saving' ? 'Guardando...' : 'Guardar configuración'}
            </button>

            <button type="button" disabled={!policy || !canManage || !!busy || dirty}
              onClick={() => void publishPolicy()}>
              <CloudUpload size={16} /> Publicar en Google
            </button>

            <button type="button" disabled={!policy || !!busy || dirty}
              onClick={() => void verifyPublication()}>
              <CheckCircle2 size={16} /> Verificar publicación
            </button>
          </div>
        </section>

        <section className="apo-card">
          <div className="apo-title">
            <Plus size={18} />
            <div>
              <h3>Agregar aplicación aprobada</h3>
              <p>
                Usa el identificador real de Google Play, no el nombre comercial.
              </p>
            </div>
          </div>

          <div className="apo-add-grid">
            <label className="apo-field">
              <span>Package name</span>
              <input value={packageName}
                onChange={event => setPackageName(event.target.value)}
                placeholder="com.microsoft.office.outlook" />
            </label>

            <label className="apo-field">
              <span>Nombre visible</span>
              <input value={appLabel}
                onChange={event => setAppLabel(event.target.value)}
                placeholder="Microsoft Outlook" />
            </label>

            <label className="apo-field">
              <span>Modo de instalación</span>
              <select value={installType}
                onChange={event => setInstallType(event.target.value as InstallType)}>
                <option value="AVAILABLE">Disponible</option>
                <option value="FORCE_INSTALLED">Instalación obligatoria</option>
                <option value="REQUIRED_FOR_SETUP">Requerida al configurar</option>
                <option value="BLOCKED">Bloqueada</option>
              </select>
            </label>
          </div>

          <button type="button" className="apo-primary"
            disabled={!policy || !canManage || !!busy}
            onClick={addApp}>
            <Plus size={16} /> Agregar a la política
          </button>

          <p className="apo-info">
            La validación del nombre de paquete comprueba su formato, no
            garantiza su existencia en Google Play. La publicación
            realizará la validación contra las APIs disponibles.
          </p>
        </section>
      </div>

      <section className="apo-card">
        <div className="apo-title apo-title-spread">
          <div>
            <h3>Lista blanca y control de aplicaciones</h3>
            <p>
              {apps.length} aplicaciones configuradas en la política seleccionada.
            </p>
          </div>
          <label className="apo-inline-search">
            <Search size={15} />
            <input value={search} onChange={event => setSearch(event.target.value)}
              placeholder="Buscar aplicación..." />
          </label>
        </div>

        <div className="apo-table-wrap">
          <table className="apo-table">
            <thead>
              <tr>
                <th>Paquete</th>
                <th>Etiqueta</th>
                <th>Instalación</th>
                <th>Visible en Launcher</th>
                <th>Acción</th>
              </tr>
            </thead>
            <tbody>
              {filteredApps.map(app => {
                const index = apps.indexOf(app)

                return (
                  <tr key={app.packageName}>
                    <td><strong>{app.packageName}</strong></td>
                    <td>
                      <input
                        aria-label={`Nombre de ${app.packageName}`}
                        value={app.label}
                        disabled={!canManage}
                        onChange={event =>
                          changeApp(index, { label: event.target.value })}
                      />
                    </td>
                    <td>
                      <select
                        aria-label={`Instalación de ${app.packageName}`}
                        value={app.installType}
                        disabled={!canManage}
                        onChange={event => {
                          const nextType = event.target.value as InstallType
                          changeApp(index, {
                            installType: nextType,
                            visible: nextType !== 'BLOCKED' && app.visible,
                          })
                        }}
                      >
                        <option value="AVAILABLE">Disponible</option>
                        <option value="FORCE_INSTALLED">Obligatoria</option>
                        <option value="REQUIRED_FOR_SETUP">Requerida</option>
                        <option value="BLOCKED">Bloqueada</option>
                      </select>
                    </td>
                    <td>
                      <input type="checkbox"
                        checked={app.visible}
                        disabled={!canManage || app.installType === 'BLOCKED'}
                        onChange={event =>
                          changeApp(index, { visible: event.target.checked })}
                        aria-label={`Visibilidad de ${app.packageName}`}
                      />
                    </td>
                    <td>
                      <button type="button" className="apo-remove"
                        aria-label={`Eliminar ${app.packageName}`}
                        disabled={!canManage}
                        onClick={() =>
                          setApps(current =>
                            current.filter(entry =>
                              entry.packageName !== app.packageName))}
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                )
              })}
              {filteredApps.length === 0 && (
                <tr>
                  <td colSpan={5} className="apo-empty">
                    No hay aplicaciones para mostrar.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="apo-card">
        <div className="apo-title">
          <Smartphone size={18} />
          <div>
            <h3>Despliegue y seguimiento</h3>
            <p>Asigna una política publicada y consulta su resultado real.</p>
          </div>
        </div>

        <div className="apo-deployment">
          <label className="apo-field">
            <span>Dispositivo Android</span>
            <select
              value={selectedDeviceId}
              onChange={event => {
                setSelectedDeviceId(event.target.value)
                setAssignment(null)
              }}
            >
              <option value="">Selecciona un dispositivo</option>
              {devices.map(device => (
                <option key={device.deviceId} value={device.deviceId}>
                  {device.deviceName} · {device.manufacturer ?? 'Android'}
                </option>
              ))}
            </select>
          </label>

          <div className="apo-actions">
            <button type="button" className="apo-primary"
              disabled={
                !canManage ||
                !policy ||
                !selectedDeviceId ||
                publication?.status !== 'Published' ||
                publication.policyVersion !== policy?.currentVersion ||
                dirty ||
                !!busy
              }
              onClick={() => void assignPolicy()}>
              <CloudUpload size={16} />
              {busy === 'assigning' ? 'Asignando...' : 'Asignar política'}
            </button>

            <button type="button" disabled={!policy || !selectedDeviceId || !!busy}
              onClick={() => void checkAssignment()}>
              <RefreshCw size={16} /> Consultar aplicación
            </button>
          </div>
        </div>

        {assignment && (
          <div className="apo-assignment">
            <div><span>Asignación</span>
              <strong>{assignment.assignmentStatus}</strong>
            </div>
            <div><span>Estado Google</span>
              <strong>{assignment.appliedPolicyState ?? 'Pendiente'}</strong>
            </div>
            <div><span>Versión</span>
              <strong>v{assignment.policyVersion}</strong>
            </div>
            <div><span>Última sincronización</span>
              <strong>{dateLabel(assignment.lastPolicySyncTimeUtc)}</strong>
            </div>
            {assignment.errorMessage && (
              <p className="apo-error">{assignment.errorMessage}</p>
            )}
          </div>
        )}
      </section>
    </section>
  )
}

type StatIcon = typeof ShieldCheck

function Stat({
  label,
  value,
  icon: Icon,
}: {
  label: string
  value: number
  icon: StatIcon
}) {
  return (
    <div className="apo-stat">
      <Icon size={20} />
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
    </div>
  )
}
