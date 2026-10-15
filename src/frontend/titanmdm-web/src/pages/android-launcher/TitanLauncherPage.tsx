import {
  ArrowDown,
  ArrowUp,
  Boxes,
  CheckCircle2,
  ClipboardList,
  CloudUpload,
  Eye,
  EyeOff,
  FileClock,
  LayoutDashboard,
  LayoutGrid,
  Loader2,
  PackagePlus,
  Plus,
  RefreshCw,
  Rocket,
  Save,
  ShieldCheck,
  Smartphone,
  Trash2,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from 'react'

import { androidEnterpriseApi } from '../../api/androidEnterpriseApi'
import {
  policiesApi,
  type AndroidPolicyAssignmentDevice,
  type AndroidPolicyAssignmentPage,
  type AndroidPolicyPublication,
  type AndroidPolicyRemoteVerificationResult,
  type PolicyDetails,
} from '../../api/policiesApi'
import { useAuth } from '../../auth/AuthContext'
import {
  TitanEmptyState,
  TitanPageHeader,
  TitanSectionCard,
  TitanStatCard,
  TitanStatusBadge,
} from '../../components/ui'
import type {
  AndroidAuditEvent,
  AndroidAuditPage,
  AndroidDeviceInventorySummary,
  AndroidEnterpriseStatus,
  AndroidInventoryItem,
  AndroidInventoryPage,
} from '../../types/androidEnterprise'
import {
  buildLauncherConfiguration,
  createLauncherDraft,
  DEFAULT_BACKGROUND_COLOR,
  DEFAULT_PRIMARY_COLOR,
  isValidBrandingColor,
  moveLauncherApplication,
  normalizeBrandingColor,
  parseLauncherConfiguration,
  type LauncherApplication,
  type LauncherDraft,
  type LauncherInstallType,
} from './launcherModel'
import './TitanLauncherPage.css'

type LauncherTab =
  | 'dashboard'
  | 'designer'
  | 'apps'
  | 'profiles'
  | 'deployments'
  | 'inventory'
  | 'audit'

interface LauncherProfile {
  policy: PolicyDetails
  draft: LauncherDraft
  publication: AndroidPolicyPublication | null
}

const tabs: Array<{ id: LauncherTab; label: string; icon: ReactNode }> = [
  { id: 'dashboard', label: 'Dashboard', icon: <LayoutDashboard size={16} /> },
  { id: 'designer', label: 'Diseñador', icon: <LayoutGrid size={16} /> },
  { id: 'apps', label: 'Apps', icon: <Boxes size={16} /> },
  { id: 'profiles', label: 'Perfiles', icon: <ClipboardList size={16} /> },
  { id: 'deployments', label: 'Despliegues', icon: <Rocket size={16} /> },
  { id: 'inventory', label: 'Inventario', icon: <Smartphone size={16} /> },
  { id: 'audit', label: 'Auditoría', icon: <FileClock size={16} /> },
]

function getErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as {
      response?: { data?: { message?: string } }
    }).response
    if (response?.data?.message) return response.data.message
  }
  return error instanceof Error ? error.message : fallback
}

function formatDate(value: string | null | undefined): string {
  if (!value) return 'No disponible'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function badgeTone(status: string | null | undefined) {
  const normalized = status?.toLowerCase() ?? ''
  if (normalized.includes('noncompliant') || normalized.includes('fail') || normalized.includes('error')) return 'danger' as const
  if (normalized.includes('publish') || normalized.includes('active') || normalized.includes('applied') || normalized.includes('compliant')) return 'success' as const
  if (normalized.includes('pending') || normalized.includes('applying') || normalized.includes('draft')) return 'warning' as const
  return 'neutral' as const
}

export function TitanLauncherPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission('android.enterprise.manage') && hasPermission('policies.manage')
  const [activeTab, setActiveTab] = useState<LauncherTab>('dashboard')
  const [profiles, setProfiles] = useState<LauncherProfile[]>([])
  const [enterprise, setEnterprise] = useState<AndroidEnterpriseStatus | null>(null)
  const [enterpriseUnavailable, setEnterpriseUnavailable] = useState(false)
  const [summary, setSummary] = useState<AndroidDeviceInventorySummary | null>(null)
  const [selectedPolicyId, setSelectedPolicyId] = useState('')
  const selectedPolicyIdRef = useRef('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [draft, setDraft] = useState<LauncherDraft>(createLauncherDraft)
  const [dirty, setDirty] = useState(false)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [verification, setVerification] = useState<AndroidPolicyRemoteVerificationResult | null>(null)
  const [deploymentPage, setDeploymentPage] = useState<AndroidPolicyAssignmentPage | null>(null)
  const [deploymentQuery, setDeploymentQuery] = useState('')
  const [deploymentSearch, setDeploymentSearch] = useState('')
  const [deploymentPageIndex, setDeploymentPageIndex] = useState(1)
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<string[]>([])
  const [loadingDeployments, setLoadingDeployments] = useState(false)
  const loadRequest = useRef(0)
  const deploymentRequest = useRef(0)

  useEffect(() => {
    selectedPolicyIdRef.current = selectedPolicyId
  }, [selectedPolicyId])

  const applyProfile = useCallback((profile: LauncherProfile | null) => {
    setSelectedPolicyId(profile?.policy.id ?? '')
    setName(profile?.policy.name ?? '')
    setDescription(profile?.policy.description ?? '')
    setDraft(profile?.draft ?? createLauncherDraft())
    setDirty(false)
    setVerification(null)
  }, [])

  const load = useCallback(async (preferredPolicyId?: string) => {
    const request = ++loadRequest.current
    setLoading(true)
    setError(null)

    try {
      const [policyResult, enterpriseResult, summaryResult] = await Promise.allSettled([
        policiesApi.getAll('Android'),
        androidEnterpriseApi.getStatus(),
        androidEnterpriseApi.getDeviceSummary(),
      ])

      if (request !== loadRequest.current) return
      if (policyResult.status === 'rejected') throw policyResult.reason

      const details = await Promise.all(
        policyResult.value.map((policy) => policiesApi.getById(policy.id)),
      )
      if (request !== loadRequest.current) return

      const launcherDetails = details.flatMap((policy) => {
        const parsed = parseLauncherConfiguration(policy.configurationJson)
        return parsed ? [{ policy, draft: parsed }] : []
      })
      const publications = await Promise.allSettled(
        launcherDetails.map(({ policy }) => policiesApi.getAndroidPublication(policy.id)),
      )
      if (request !== loadRequest.current) return

      const nextProfiles = launcherDetails.map((profile, index) => ({
        ...profile,
        publication: publications[index].status === 'fulfilled'
          ? publications[index].value
          : null,
      }))

      setProfiles(nextProfiles)
      setEnterprise(enterpriseResult.status === 'fulfilled' ? enterpriseResult.value : null)
      setEnterpriseUnavailable(enterpriseResult.status === 'rejected')
      setSummary(summaryResult.status === 'fulfilled' ? summaryResult.value : null)

      const targetId = preferredPolicyId || selectedPolicyIdRef.current
      const target = nextProfiles.find((profile) => profile.policy.id === targetId)
        ?? nextProfiles[0]
        ?? null
      applyProfile(target)
    } catch (loadError) {
      if (request === loadRequest.current) {
        setError(getErrorMessage(loadError, 'No fue posible cargar Titan Launcher.'))
      }
    } finally {
      if (request === loadRequest.current) setLoading(false)
    }
  }, [applyProfile])

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeout)
  }, [load])

  const selectedProfile = useMemo(
    () => profiles.find((profile) => profile.policy.id === selectedPolicyId) ?? null,
    [profiles, selectedPolicyId],
  )

  const loadDeployments = useCallback(async () => {
    const request = ++deploymentRequest.current
    if (!selectedPolicyId) {
      setDeploymentPage(null)
      setSelectedDeviceIds([])
      return
    }

    setLoadingDeployments(true)
    try {
      const result = await policiesApi.getAndroidAssignmentsPage(
        selectedPolicyId,
        {
          search: deploymentSearch,
          page: deploymentPageIndex,
          pageSize: 25,
        },
      )
      if (request !== deploymentRequest.current) return
      setDeploymentPage(result)
      setSelectedDeviceIds([])
    } catch (deployError) {
      if (request === deploymentRequest.current) {
        setDeploymentPage(null)
        setError(getErrorMessage(deployError, 'No fue posible cargar los despliegues.'))
      }
    } finally {
      if (request === deploymentRequest.current) {
        setLoadingDeployments(false)
      }
    }
  }, [selectedPolicyId, deploymentSearch, deploymentPageIndex])

  useEffect(() => {
    if (activeTab === 'deployments' || activeTab === 'dashboard' || activeTab === 'audit') {
      const timeout = window.setTimeout(() => void loadDeployments(), 0)
      return () => window.clearTimeout(timeout)
    }
  }, [activeTab, loadDeployments])

  function updateDraft(patch: Partial<LauncherDraft>) {
    setDraft((current) => ({ ...current, ...patch }))
    setDirty(true)
  }

  function selectProfile(profile: LauncherProfile) {
    if (dirty && !window.confirm('Hay cambios sin guardar. ¿Cambiar de perfil y descartarlos?')) return
    applyProfile(profile)
  }

  function newProfile() {
    if (dirty && !window.confirm('Hay cambios sin guardar. ¿Crear un perfil nuevo?')) return
    applyProfile(null)
    setActiveTab('designer')
  }

  async function saveProfile() {
    if (!canManage) return
    if (!name.trim() || !draft.companyName.trim() || !draft.packageName.trim() || !draft.activityName.trim()) {
      setError('Completa el nombre, la compañía, el paquete y la actividad HOME.')
      return
    }
    if (draft.applications.some((app) => !app.packageName.trim())) {
      setError('Todas las aplicaciones necesitan un package name.')
      return
    }
    if (!isValidBrandingColor(draft.primaryColor) || !isValidBrandingColor(draft.backgroundColor)) {
      setError('Los colores del launcher deben utilizar el formato #RRGGBB.')
      return
    }

    setWorking(true)
    setError(null)
    setMessage(null)
    try {
      const configurationJson = JSON.stringify(buildLauncherConfiguration(draft))
      const saved = selectedPolicyId
        ? await policiesApi.update(selectedPolicyId, {
            name: name.trim(),
            description: description.trim() || null,
            configurationJson,
          })
        : await policiesApi.create({
            name: name.trim(),
            description: description.trim() || null,
            platform: 'Android',
            configurationJson,
          })

      if (saved.status !== 'Active') await policiesApi.activate(saved.id)
      setDirty(false)
      setMessage('Perfil guardado y activado. El siguiente paso es publicarlo.')
      await load(saved.id)
    } catch (saveError) {
      setError(getErrorMessage(saveError, 'No fue posible guardar el perfil.'))
    } finally {
      setWorking(false)
    }
  }

  async function publishProfile() {
    if (!canManage || !selectedPolicyId || dirty) return
    setWorking(true)
    setError(null)
    setMessage(null)
    try {
      const result = await policiesApi.publishAndroid(selectedPolicyId)
      setMessage(`Versión ${result.policyVersion} publicada en Android Management API.`)
      await load(selectedPolicyId)
    } catch (publishError) {
      setError(getErrorMessage(publishError, 'No fue posible publicar el perfil.'))
    } finally {
      setWorking(false)
    }
  }

  async function verifyProfile() {
    if (!selectedPolicyId) return
    setWorking(true)
    setError(null)
    try {
      const result = await policiesApi.verifyAndroidPublication(selectedPolicyId)
      setVerification(result)
      setMessage(result.existsInGoogle
        ? 'La publicación existe en Google Android Management.'
        : 'Google no reportó esta publicación.')
    } catch (verifyError) {
      setError(getErrorMessage(verifyError, 'No fue posible verificar la publicación.'))
    } finally {
      setWorking(false)
    }
  }

  async function assignDevice(deviceId: string) {
    if (!canManage || !selectedProfile || selectedProfile.policy.status !== 'Active' || selectedProfile.publication?.status !== 'Published') return
    setWorking(true)
    setError(null)
    setMessage(null)
    try {
      await policiesApi.assignAndroid(selectedProfile.policy.id, deviceId)
      setMessage('Asignación aceptada. El estado permanecerá pendiente hasta la sincronización del dispositivo.')
      await loadDeployments()
    } catch (assignError) {
      setError(getErrorMessage(assignError, 'No fue posible asignar el launcher.'))
    } finally {
      setWorking(false)
    }
  }

  async function assignSelectedDevices() {
    if (!canManage || !selectedProfile || selectedProfile.policy.status !== 'Active' || selectedProfile.publication?.status !== 'Published') return
    if (selectedDeviceIds.length === 0) return
    setWorking(true)
    setError(null)
    setMessage(null)
    try {
      await Promise.all(
        selectedDeviceIds.map((deviceId) => policiesApi.assignAndroid(selectedProfile.policy.id, deviceId)),
      )
      setMessage(`Asignación enviada a ${selectedDeviceIds.length} dispositivo(s).`)
      setSelectedDeviceIds([])
      await loadDeployments()
    } catch (assignError) {
      setError(getErrorMessage(assignError, 'No fue posible asignar el launcher.'))
    } finally {
      setWorking(false)
    }
  }

  const deployable = selectedProfile?.policy.status === 'Active' && selectedProfile.publication?.status === 'Published'
  const publishedProfiles = profiles.filter((profile) => profile.publication?.status === 'Published').length
  const managedDevicesCount = summary?.managed ?? deploymentPage?.items.filter((item) => item.isManaged).length ?? 0
  const appliedDeployments = deploymentPage?.items.filter((item) =>
    item.assignmentStatus.toLowerCase().includes('applied') ||
    (item.appliedPolicyState ?? '').toLowerCase().includes('applied'),
  ).length ?? 0

  return (
    <main className="titan-page launcher-page">
      <TitanPageHeader
        eyebrow="ANDROID ENTERPRISE"
        title="Titan Launcher"
        description="Diseña, publica y despliega una pantalla de inicio corporativa multiaplicación con datos reales de Android Management API."
        icon={<LayoutGrid size={17} />}
        actions={(
          <>
            {canManage && (
              <button type="button" className="titan-btn titan-btn--primary" onClick={newProfile}>
                <Plus size={16} /> Nuevo perfil
              </button>
            )}
            <button type="button" className="titan-btn titan-btn--secondary" disabled={loading} onClick={() => void load()}>
              <RefreshCw size={16} className={loading ? 'launcher-spin' : ''} /> Actualizar
            </button>
          </>
        )}
      />

      <section className="launcher-enterprise-strip">
        <div>
          <span>Android Enterprise</span>
          <TitanStatusBadge
            label={enterpriseUnavailable ? 'No disponible' : enterprise?.status ?? 'Pendiente'}
            tone={enterprise?.canAuthenticate ? 'success' : enterpriseUnavailable ? 'danger' : 'warning'}
          />
        </div>
        <div>
          <span>Enterprise</span>
          <strong>{enterprise?.enterpriseDisplayName || enterprise?.enterpriseName || 'Sin vincular'}</strong>
        </div>
        <div>
          <span>Permisos</span>
          <strong>{canManage ? 'Administración' : 'Solo lectura'}</strong>
        </div>
        {enterprise?.lastError && <p>{enterprise.lastError}</p>}
      </section>

      {message && <div className="launcher-notice success" role="status"><CheckCircle2 size={17} />{message}</div>}
      {error && <div className="launcher-notice error" role="alert">{error}</div>}

      <nav className="launcher-tabs" aria-label="Secciones de Titan Launcher">
        {tabs.map((tab) => (
          <button
            type="button"
            key={tab.id}
            className={activeTab === tab.id ? 'active' : ''}
            aria-current={activeTab === tab.id ? 'page' : undefined}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.icon}{tab.label}
          </button>
        ))}
      </nav>

      {loading ? (
        <TitanSectionCard title="Cargando Titan Launcher" icon={<Loader2 className="launcher-spin" size={20} />} loading>
          <span />
        </TitanSectionCard>
      ) : (
        <>
          {activeTab === 'dashboard' && (
            <LauncherDashboard
              profiles={profiles.length}
              publishedProfiles={publishedProfiles}
              devices={managedDevicesCount}
              applied={appliedDeployments}
              summary={summary}
              selected={selectedProfile}
              dirty={dirty}
              verification={verification}
              canManage={canManage}
              working={working}
              onSave={saveProfile}
              onPublish={publishProfile}
              onVerify={verifyProfile}
              onOpenDesigner={() => setActiveTab('designer')}
            />
          )}

          {activeTab === 'designer' && (
            <DesignerTab
              name={name}
              description={description}
              draft={draft}
              dirty={dirty}
              canManage={canManage}
              working={working}
              selected={selectedProfile}
              onName={(value) => { setName(value); setDirty(true) }}
              onDescription={(value) => { setDescription(value); setDirty(true) }}
              onDraft={updateDraft}
              onSave={saveProfile}
            />
          )}

          {activeTab === 'apps' && (
            <AppsTab
              applications={draft.applications}
              canManage={canManage}
              onChange={(applications) => updateDraft({ applications })}
            />
          )}

          {activeTab === 'profiles' && (
            <ProfilesTab
              profiles={profiles}
              selectedPolicyId={selectedPolicyId}
              canManage={canManage}
              onSelect={selectProfile}
              onCreate={newProfile}
            />
          )}

          {activeTab === 'deployments' && (
            <DeploymentsTab
              selected={selectedProfile}
              page={deploymentPage}
              loading={loadingDeployments}
              canManage={canManage}
              working={working}
              deployable={deployable}
              searchQuery={deploymentQuery}
              selectedIds={selectedDeviceIds}
              onSearchChange={setDeploymentQuery}
              onSearchSubmit={() => {
                setDeploymentPageIndex(1)
                setDeploymentSearch(deploymentQuery.trim())
              }}
              onClearSearch={() => {
                setDeploymentQuery('')
                setDeploymentSearch('')
                setDeploymentPageIndex(1)
              }}
              onPageChange={setDeploymentPageIndex}
              onToggleSelect={(deviceId, checked) =>
                setSelectedDeviceIds((current) =>
                  checked ? [...new Set([...current, deviceId])] : current.filter((id) => id !== deviceId),
                )}
              onToggleAll={(deviceIds, checked) =>
                setSelectedDeviceIds(checked ? [...new Set([...deviceIds])] : [])}
              onAssign={assignDevice}
              onAssignSelected={assignSelectedDevices}
            />
          )}

          {activeTab === 'inventory' && <InventoryTab />}

          {activeTab === 'audit' && (
            <AuditTab
              selected={selectedProfile}
              assignments={deploymentPage?.items ?? []}
              verification={verification}
            />
          )}
        </>
      )}
    </main>
  )
}

function LauncherDashboard({
  profiles,
  publishedProfiles,
  devices,
  applied,
  summary,
  selected,
  dirty,
  verification,
  canManage,
  working,
  onSave,
  onPublish,
  onVerify,
  onOpenDesigner,
}: {
  profiles: number
  publishedProfiles: number
  devices: number
  applied: number
  summary: AndroidDeviceInventorySummary | null
  selected: LauncherProfile | null
  dirty: boolean
  verification: AndroidPolicyRemoteVerificationResult | null
  canManage: boolean
  working: boolean
  onSave: () => Promise<void>
  onPublish: () => Promise<void>
  onVerify: () => Promise<void>
  onOpenDesigner: () => void
}) {
  return (
    <div className="launcher-dashboard">
      <section className="titan-kpi-grid">
        <TitanStatCard icon={<ClipboardList size={22} />} label="Perfiles" value={profiles} />
        <TitanStatCard icon={<CloudUpload size={22} />} label="Publicados" value={publishedProfiles} />
        <TitanStatCard icon={<Smartphone size={22} />} label="Android administrados" value={devices} />
        <TitanStatCard icon={<ShieldCheck size={22} />} label="Aplicados (página)" value={applied} hint="Dispositivos de la página actual" />
        <TitanStatCard
          icon={<ShieldCheck size={22} />}
          label="Cumplimiento"
          value={summary ? `${summary.compliant}/${summary.total}` : 'N/D'}
          hint={summary ? `${summary.nonCompliant} no conformes` : 'Inventario no sincronizado'}
        />
        <TitanStatCard
          icon={<RefreshCw size={22} />}
          label="Última sincronización"
          value={summary?.lastSynchronizationUtc ? formatDate(summary.lastSynchronizationUtc) : 'Nunca'}
          hint={summary ? `${summary.missingInGoogle} ausentes en Google` : 'Sin datos'}
        />
      </section>

      <div className="launcher-dashboard-grid">
        <TitanSectionCard title="Flujo de despliegue" description="Estado real del perfil seleccionado." icon={<Rocket size={20} />}>
          {!selected ? (
            <TitanEmptyState
              icon={<LayoutGrid size={28} />}
              title="Crea o selecciona un perfil"
              description="El flujo comenzará con un borrador schema v1."
              action={<button type="button" className="titan-btn titan-btn--secondary" onClick={onOpenDesigner}>Abrir diseñador</button>}
            />
          ) : (
            <ol className="launcher-workflow">
              <WorkflowStep label="Guardado" value={dirty ? 'Cambios pendientes' : `Versión ${selected.policy.currentVersion}`} ready={!dirty} />
              <WorkflowStep label="Activado" value={selected.policy.status} ready={selected.policy.status === 'Active'} />
              <WorkflowStep label="Publicado" value={selected.publication?.status ?? 'Pendiente'} ready={selected.publication?.status === 'Published'} />
              <WorkflowStep label="Verificado" value={verification ? (verification.existsInGoogle ? 'Existe en Google' : 'No encontrado') : 'Sin verificar'} ready={verification?.existsInGoogle === true} />
            </ol>
          )}
        </TitanSectionCard>

        <TitanSectionCard title="Acciones" description="Guardar, publicar y verificar son pasos explícitos." icon={<ShieldCheck size={20} />}>
          <div className="launcher-action-stack">
            <button type="button" className="titan-btn titan-btn--primary" disabled={!canManage || working || !dirty} onClick={() => void onSave()}><Save size={16} /> Guardar y activar</button>
            <button type="button" className="titan-btn titan-btn--secondary" disabled={!canManage || working || !selected || dirty || selected.policy.status !== 'Active'} onClick={() => void onPublish()}><CloudUpload size={16} /> Publicar</button>
            <button type="button" className="titan-btn titan-btn--secondary" disabled={working || !selected?.publication} onClick={() => void onVerify()}><ShieldCheck size={16} /> Verificar en Google</button>
            {!canManage && <p className="launcher-muted">Modo lectura: las mutaciones requieren permisos de Android Enterprise y políticas.</p>}
          </div>
        </TitanSectionCard>
      </div>
    </div>
  )
}

function WorkflowStep({ label, value, ready }: { label: string; value: string; ready: boolean }) {
  return (
    <li>
      <span className={ready ? 'ready' : ''}>{ready ? <CheckCircle2 size={16} /> : <FileClock size={16} />}</span>
      <div><strong>{label}</strong><small>{value}</small></div>
    </li>
  )
}

function DesignerTab({
  name,
  description,
  draft,
  dirty,
  canManage,
  working,
  selected,
  onName,
  onDescription,
  onDraft,
  onSave,
}: {
  name: string
  description: string
  draft: LauncherDraft
  dirty: boolean
  canManage: boolean
  working: boolean
  selected: LauncherProfile | null
  onName: (value: string) => void
  onDescription: (value: string) => void
  onDraft: (patch: Partial<LauncherDraft>) => void
  onSave: () => Promise<void>
}) {
  const visibleApps = draft.applications.filter((app) => app.visible)
  return (
    <div className="launcher-designer-grid">
      <TitanSectionCard
        title={selected ? 'Editar perfil' : 'Nuevo perfil'}
        description="Modelo de configuración Titan Launcher schema v1."
        icon={<LayoutGrid size={20} />}
        action={<TitanStatusBadge label={dirty ? 'Cambios pendientes' : 'Sin cambios'} tone={dirty ? 'warning' : 'success'} />}
      >
        <div className="launcher-form-grid">
          <label><span>Nombre del perfil</span><input disabled={!canManage} value={name} onChange={(event) => onName(event.target.value)} placeholder="Launcher tiendas" /></label>
          <label><span>Schema</span><input readOnly value="Titan Launcher v1" /></label>
          <label className="launcher-wide"><span>Descripción</span><textarea disabled={!canManage} value={description} onChange={(event) => onDescription(event.target.value)} /></label>
          <label><span>Compañía</span><input disabled={!canManage} value={draft.companyName} onChange={(event) => onDraft({ companyName: event.target.value })} /></label>
          <label><span>Mensaje de bienvenida</span><input disabled={!canManage} value={draft.welcomeMessage} onChange={(event) => onDraft({ welcomeMessage: event.target.value })} /></label>
          <label className="launcher-color">
            <span>Color primario</span>
            <span className="launcher-color-control">
              <input
                type="color"
                disabled={!canManage}
                value={normalizeBrandingColor(draft.primaryColor, DEFAULT_PRIMARY_COLOR)}
                onChange={(event) => onDraft({ primaryColor: event.target.value })}
                aria-label="Selector de color primario"
              />
              <input
                disabled={!canManage}
                value={draft.primaryColor}
                onChange={(event) => onDraft({ primaryColor: event.target.value })}
                aria-label="Color primario en hexadecimal"
              />
            </span>
          </label>
          <label className="launcher-color">
            <span>Color de fondo</span>
            <span className="launcher-color-control">
              <input
                type="color"
                disabled={!canManage}
                value={normalizeBrandingColor(draft.backgroundColor, DEFAULT_BACKGROUND_COLOR)}
                onChange={(event) => onDraft({ backgroundColor: event.target.value })}
                aria-label="Selector de color de fondo"
              />
              <input
                disabled={!canManage}
                value={draft.backgroundColor}
                onChange={(event) => onDraft({ backgroundColor: event.target.value })}
                aria-label="Color de fondo en hexadecimal"
              />
            </span>
          </label>
          <label><span>Paquete del agente</span><input disabled={!canManage} value={draft.packageName} onChange={(event) => onDraft({ packageName: event.target.value })} /></label>
          <label><span>Actividad HOME</span><input disabled={!canManage} value={draft.activityName} onChange={(event) => onDraft({ activityName: event.target.value })} /></label>
          <label className="launcher-check launcher-wide"><input type="checkbox" disabled={!canManage} checked={draft.requiredForSetup} onChange={(event) => onDraft({ requiredForSetup: event.target.checked })} /><span>Requerida durante el aprovisionamiento</span></label>
        </div>
        <button type="button" className="titan-btn titan-btn--primary launcher-save" disabled={!canManage || working || !dirty} onClick={() => void onSave()}><Save size={16} /> Guardar y activar</button>
      </TitanSectionCard>

      <TitanSectionCard title="Vista previa" description="Representación responsive de las apps visibles." icon={<Smartphone size={20} />} className="launcher-preview-card">
        <div className="launcher-phone">
          <div className="launcher-phone-speaker" />
          <div
            className="launcher-phone-screen"
            style={{
              '--launcher-primary': normalizeBrandingColor(draft.primaryColor, DEFAULT_PRIMARY_COLOR),
              '--launcher-background': normalizeBrandingColor(draft.backgroundColor, DEFAULT_BACKGROUND_COLOR),
            } as CSSProperties}
          >
            <span className="launcher-preview-company">{draft.companyName || 'Compañía'}</span>
            <h3>{draft.welcomeMessage || 'Bienvenido'}</h3>
            <div className="launcher-preview-apps">
              {visibleApps.length ? visibleApps.map((app) => (
                <div key={app.packageName}>
                  <span>{(app.label || app.packageName).slice(0, 1).toUpperCase()}</span>
                  <small>{app.label || app.packageName}</small>
                </div>
              )) : <p>Agrega aplicaciones visibles para completar la pantalla.</p>}
            </div>
            <footer>Administrado por TitanMDM</footer>
          </div>
        </div>
      </TitanSectionCard>
    </div>
  )
}

function AppsTab({
  applications,
  canManage,
  onChange,
}: {
  applications: LauncherApplication[]
  canManage: boolean
  onChange: (applications: LauncherApplication[]) => void
}) {
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [overIndex, setOverIndex] = useState<number | null>(null)

  function update(index: number, patch: Partial<LauncherApplication>) {
    onChange(applications.map((app, current) => current === index ? { ...app, ...patch } : app))
  }

  function drop(target: number) {
    if (dragIndex === null || dragIndex === target) {
      setDragIndex(null)
      setOverIndex(null)
      return
    }

    const next = [...applications]
    const [moved] = next.splice(dragIndex, 1)
    next.splice(target, 0, moved)
    onChange(next)
    setDragIndex(null)
    setOverIndex(null)
  }

  return (
    <TitanSectionCard
      title="Aplicaciones del launcher"
      description="Arrastra las filas o usa las flechas para reordenar; el orden se conserva en la configuración. Las apps ocultas se publican como BLOCKED."
      icon={<Boxes size={20} />}
      action={canManage ? (
        <button type="button" className="titan-btn titan-btn--primary" onClick={() => onChange([...applications, { packageName: '', label: '', installType: 'AVAILABLE', visible: true }])}><PackagePlus size={16} /> Agregar app</button>
      ) : undefined}
    >
      {!applications.length ? (
        <TitanEmptyState icon={<Boxes size={28} />} title="Sin aplicaciones" description="Agrega las apps autorizadas para este launcher." />
      ) : (
        <div className="launcher-app-list">
          {applications.map((app, index) => (
            <article
              className={overIndex === index && dragIndex !== null && dragIndex !== index ? 'launcher-app-row drag-over' : 'launcher-app-row'}
              key={`${index}-${app.packageName}`}
              draggable={canManage}
              onDragStart={() => setDragIndex(index)}
              onDragOver={(event) => {
                if (canManage && dragIndex !== null) {
                  event.preventDefault()
                  setOverIndex(index)
                }
              }}
              onDrop={(event) => {
                event.preventDefault()
                drop(index)
              }}
              onDragEnd={() => {
                setDragIndex(null)
                setOverIndex(null)
              }}
            >
              <span className="launcher-app-order" title={canManage ? 'Arrastra para reordenar' : undefined}>{index + 1}</span>
              <label><span>Package name</span><input disabled={!canManage} value={app.packageName} onChange={(event) => update(index, { packageName: event.target.value })} placeholder="com.empresa.app" /></label>
              <label><span>Etiqueta</span><input disabled={!canManage} value={app.label} onChange={(event) => update(index, { label: event.target.value })} placeholder="Nombre visible" /></label>
              <label><span>Instalación</span><select disabled={!canManage} value={app.installType} onChange={(event) => update(index, { installType: event.target.value as LauncherInstallType })}><option value="AVAILABLE">Disponible</option><option value="FORCE_INSTALLED">Forzada</option><option value="REQUIRED_FOR_SETUP">Requerida en setup</option></select></label>
              <button type="button" className={app.visible ? 'launcher-visibility visible' : 'launcher-visibility'} disabled={!canManage} onClick={() => update(index, { visible: !app.visible })}>{app.visible ? <Eye size={16} /> : <EyeOff size={16} />}{app.visible ? 'Visible' : 'Oculta'}</button>
              <div className="launcher-row-actions">
                <button type="button" disabled={!canManage || index === 0} aria-label="Mover arriba" onClick={() => onChange(moveLauncherApplication(applications, index, -1))}><ArrowUp size={15} /></button>
                <button type="button" disabled={!canManage || index === applications.length - 1} aria-label="Mover abajo" onClick={() => onChange(moveLauncherApplication(applications, index, 1))}><ArrowDown size={15} /></button>
                <button type="button" disabled={!canManage} aria-label="Eliminar aplicación" onClick={() => onChange(applications.filter((_, current) => current !== index))}><Trash2 size={15} /></button>
              </div>
            </article>
          ))}
        </div>
      )}
    </TitanSectionCard>
  )
}

function ProfilesTab({
  profiles,
  selectedPolicyId,
  canManage,
  onSelect,
  onCreate,
}: {
  profiles: LauncherProfile[]
  selectedPolicyId: string
  canManage: boolean
  onSelect: (profile: LauncherProfile) => void
  onCreate: () => void
}) {
  return (
    <TitanSectionCard title="Perfiles Titan Launcher" description="Políticas Android cuyo launcher está habilitado." icon={<ClipboardList size={20} />}>
      {!profiles.length ? (
        <TitanEmptyState
          icon={<LayoutGrid size={28} />}
          title="No hay perfiles launcher"
          description="Crea el primer perfil usando el contrato schema v1."
          action={canManage ? <button type="button" className="titan-btn titan-btn--primary" onClick={onCreate}><Plus size={16} /> Crear perfil</button> : undefined}
        />
      ) : (
        <div className="launcher-profile-grid">
          {profiles.map((profile) => (
            <button type="button" key={profile.policy.id} className={profile.policy.id === selectedPolicyId ? 'selected' : ''} onClick={() => onSelect(profile)}>
              <div><strong>{profile.policy.name}</strong><span>{profile.policy.description || 'Sin descripción'}</span></div>
              <div className="launcher-profile-badges"><TitanStatusBadge label={profile.policy.status} tone={badgeTone(profile.policy.status)} /><TitanStatusBadge label={profile.publication?.status ?? 'Sin publicar'} tone={badgeTone(profile.publication?.status)} /></div>
              <small>v{profile.policy.currentVersion} · {profile.draft.applications.length} apps · {profile.policy.assignedDevices} asignados</small>
            </button>
          ))}
        </div>
      )}
    </TitanSectionCard>
  )
}

function DeploymentsTab({
  selected,
  page,
  loading,
  canManage,
  working,
  deployable,
  searchQuery,
  selectedIds,
  onSearchChange,
  onSearchSubmit,
  onClearSearch,
  onPageChange,
  onToggleSelect,
  onToggleAll,
  onAssign,
  onAssignSelected,
}: {
  selected: LauncherProfile | null
  page: AndroidPolicyAssignmentPage | null
  loading: boolean
  canManage: boolean
  working: boolean
  deployable: boolean
  searchQuery: string
  selectedIds: string[]
  onSearchChange: (value: string) => void
  onSearchSubmit: () => void
  onClearSearch: () => void
  onPageChange: (page: number) => void
  onToggleSelect: (deviceId: string, checked: boolean) => void
  onToggleAll: (deviceIds: string[], checked: boolean) => void
  onAssign: (deviceId: string) => Promise<void>
  onAssignSelected: () => Promise<void>
}) {
  const rows = page?.items ?? []
  const totalPages = Math.max(page?.totalPages ?? 1, 1)
  const selectableIds = rows.filter((row) => row.isManaged).map((row) => row.deviceId)
  const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selectedIds.includes(id))

  return (
    <TitanSectionCard
      title="Despliegues"
      description="Estado de asignación por dispositivo Android administrado, resuelto por el servidor con paginación y búsqueda."
      icon={<Rocket size={20} />}
      loading={loading && !page}
    >
      {!selected ? (
        <TitanEmptyState icon={<ClipboardList size={28} />} title="Selecciona un perfil" description="Las asignaciones dependen del perfil activo." />
      ) : (
        <>
          <form
            className="launcher-tab-toolbar"
            onSubmit={(event) => {
              event.preventDefault()
              onSearchSubmit()
            }}
          >
            <input
              value={searchQuery}
              onChange={(event) => onSearchChange(event.target.value)}
              placeholder="Buscar dispositivo por nombre, serie o modelo"
              aria-label="Buscar dispositivos desplegables"
            />
            <button type="submit" className="titan-btn">Buscar</button>
            {searchQuery && (
              <button
                type="button"
                className="titan-btn titan-btn--secondary"
                onClick={onClearSearch}
              >
                Limpiar
              </button>
            )}
            {page && (
              <button
                type="button"
                className="titan-btn titan-btn--primary launcher-table-action"
                disabled={!canManage || working || selectedIds.length === 0 || !deployable}
                onClick={() => void onAssignSelected()}
              >
                Asignar seleccionados ({selectedIds.length})
              </button>
            )}
          </form>

          {!rows.length ? (
            <TitanEmptyState
              icon={<Smartphone size={28} />}
              title={searchQuery ? 'Sin resultados' : 'Sin dispositivos administrados'}
              description={searchQuery ? `No hay dispositivos que coincidan con «${searchQuery}».` : 'Sincroniza el inventario de Android Enterprise.'}
            />
          ) : (
            <>
              <div className="launcher-table-wrap">
                <table className="titan-table launcher-table">
                  <thead>
                    <tr>
                      <th>
                        <input
                          type="checkbox"
                          aria-label="Seleccionar página"
                          checked={allSelected}
                          disabled={!canManage || !deployable}
                          onChange={(event) => onToggleAll(selectableIds, event.target.checked)}
                        />
                      </th>
                      <th>Dispositivo</th>
                      <th>Asignación</th>
                      <th>Google</th>
                      <th>Sincronización</th>
                      <th>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => {
                      const assigned = row.assignmentStatus !== 'NotAssigned'
                      return (
                        <tr key={row.deviceId}>
                          <td>
                            {row.isManaged && (
                              <input
                                type="checkbox"
                                aria-label={`Seleccionar ${row.deviceName}`}
                                checked={selectedIds.includes(row.deviceId)}
                                disabled={!canManage || !deployable}
                                onChange={(event) => onToggleSelect(row.deviceId, event.target.checked)}
                              />
                            )}
                          </td>
                          <td><strong>{row.deviceName}</strong><small>{row.model || row.serialNumber}</small></td>
                          <td><TitanStatusBadge label={row.assignmentStatus} tone={badgeTone(row.assignmentStatus)} /></td>
                          <td>{row.appliedPolicyState ?? (assigned ? 'Pendiente' : 'N/D')}</td>
                          <td>{formatDate(row.lastPolicySyncTimeUtc)}</td>
                          <td>
                            <button
                              type="button"
                              className="titan-btn titan-btn--secondary launcher-table-action"
                              disabled={!canManage || working || !deployable}
                              onClick={() => void onAssign(row.deviceId)}
                            >
                              {assigned ? 'Reasignar' : 'Asignar'}
                            </button>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>

              <div className="launcher-tab-toolbar launcher-pagination">
                <span>
                  Página {page?.page ?? 1} de {totalPages} · {page?.totalCount ?? 0} dispositivos
                </span>
                <button
                  type="button"
                  className="titan-btn titan-btn--secondary"
                  disabled={loading || (page?.page ?? 1) <= 1}
                  onClick={() => onPageChange((page?.page ?? 1) - 1)}
                >
                  Anterior
                </button>
                <button
                  type="button"
                  className="titan-btn titan-btn--secondary"
                  disabled={loading || (page?.page ?? 1) >= totalPages}
                  onClick={() => onPageChange((page?.page ?? 1) + 1)}
                >
                  Siguiente
                </button>
              </div>
            </>
          )}

          {!deployable && <p className="launcher-muted">El perfil debe estar activo y publicado antes de asignarlo.</p>}
        </>
      )}
    </TitanSectionCard>
  )
}

function InventoryTab() {
  const pageSize = 25
  const [page, setPage] = useState(1)
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [data, setData] = useState<AndroidInventoryPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)

  const load = useCallback(async () => {
    const id = ++requestId.current
    setLoading(true)
    setError(null)

    try {
      const result = await androidEnterpriseApi.getInventory({ page, pageSize, search: term })
      if (id !== requestId.current) return
      setData(result)
    } catch (cause) {
      if (id === requestId.current) {
        setError(getErrorMessage(cause, 'No fue posible cargar el inventario Android.'))
      }
    } finally {
      if (id === requestId.current) setLoading(false)
    }
  }, [page, term, pageSize])

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeout)
  }, [load])

  const totalPages = Math.max(data?.totalPages ?? 1, 1)

  return (
    <TitanSectionCard
      title="Inventario Android"
      description="Dispositivos Android con paginación y búsqueda resueltas por el servidor."
      icon={<Smartphone size={20} />}
    >
      <form
        className="launcher-tab-toolbar"
        onSubmit={(event) => {
          event.preventDefault()
          setPage(1)
          setTerm(query.trim())
        }}
      >
        <input
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Buscar por nombre, serie, modelo o política"
          aria-label="Buscar en el inventario Android"
        />
        <button type="submit" className="titan-btn">Buscar</button>
        {term && (
          <button
            type="button"
            className="titan-btn titan-btn--secondary"
            onClick={() => {
              setQuery('')
              setTerm('')
              setPage(1)
            }}
          >
            Limpiar
          </button>
        )}
      </form>

      {loading && !data ? (
        <div className="launcher-muted"><Loader2 className="launcher-spin" size={16} /> Cargando inventario…</div>
      ) : error ? (
        <TitanEmptyState icon={<Smartphone size={28} />} title="Inventario no disponible" description={error} />
      ) : !data?.items.length ? (
        <TitanEmptyState
          icon={<Smartphone size={28} />}
          title="Inventario vacío"
          description={term ? `No hay dispositivos que coincidan con «${term}».` : 'No hay dispositivos Android disponibles para mostrar.'}
        />
      ) : (
        <>
          <div className="launcher-table-wrap">
            <table className="titan-table launcher-table">
              <thead>
                <tr>
                  <th>Dispositivo</th>
                  <th>Estado</th>
                  <th>Cumplimiento</th>
                  <th>Política aplicada</th>
                  <th>Google</th>
                  <th>Última sincronización</th>
                  <th>Último contacto</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((device: AndroidInventoryItem) => (
                  <tr key={device.deviceId}>
                    <td><strong>{device.deviceName}</strong><small>{device.serialNumber}{device.manufacturer || device.model ? ` · ${device.manufacturer ?? ''} ${device.model ?? ''}` : ''}</small></td>
                    <td><TitanStatusBadge label={device.status} tone={badgeTone(device.status)} /></td>
                    <td><TitanStatusBadge label={device.complianceStatus} tone={badgeTone(device.complianceStatus)} /></td>
                    <td>
                      {device.appliedPolicyName ? (
                        <span>
                          {device.appliedPolicyName} · v{device.appliedPolicyVersion}
                          <small>{device.appliedPolicyState}</small>
                        </span>
                      ) : (
                        <span>Sin política<small>{device.managementMode ?? '…"'}</small></span>
                      )}
                    </td>
                    <td>{device.isDeletedInGoogle ? 'Ausente' : 'Presente'}</td>
                    <td>{formatDate(device.lastPolicySyncTimeUtc)}</td>
                    <td>{formatDate(device.lastSeenAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="launcher-tab-toolbar launcher-pagination">
            <span>
              Página {data.page} de {totalPages} · {data.totalCount} dispositivos
            </span>
            <button
              type="button"
              className="titan-btn titan-btn--secondary"
              disabled={loading || data.page <= 1}
              onClick={() => setPage(data.page - 1)}
            >
              Anterior
            </button>
            <button
              type="button"
              className="titan-btn titan-btn--secondary"
              disabled={loading || data.page >= totalPages}
              onClick={() => setPage(data.page + 1)}
            >
              Siguiente
            </button>
          </div>
        </>
      )}
    </TitanSectionCard>
  )
}

function AuditTab({
  selected,
  assignments,
  verification,
}: {
  selected: LauncherProfile | null
  assignments: AndroidPolicyAssignmentDevice[]
  verification: AndroidPolicyRemoteVerificationResult | null
}) {
  const pageSize = 50
  const [audit, setAudit] = useState<AndroidAuditPage | null>(null)
  const [auditLoading, setAuditLoading] = useState(true)
  const [auditError, setAuditError] = useState<string | null>(null)
  const [auditQuery, setAuditQuery] = useState('')
  const [auditTerm, setAuditTerm] = useState('')
  const [auditPage, setAuditPage] = useState(1)
  const requestId = useRef(0)

  const load = useCallback(async () => {
    const id = ++requestId.current
    setAuditLoading(true)
    setAuditError(null)

    try {
      const result = await androidEnterpriseApi.getAudit({ page: auditPage, pageSize, search: auditTerm })
      if (id !== requestId.current) return
      setAudit(result)
    } catch (cause) {
      if (id === requestId.current) {
        setAuditError(getErrorMessage(cause, 'No fue posible cargar la auditoría Android.'))
      }
    } finally {
      if (id === requestId.current) setAuditLoading(false)
    }
  }, [auditPage, auditTerm, pageSize])

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeout)
  }, [load])

  const auditTotalPages = Math.max(audit?.totalPages ?? 1, 1)

  const timeline = selected
    ? [
        { label: 'Perfil creado', state: selected.policy.status, date: selected.policy.createdAtUtc },
        { label: 'Última actualización', state: `Versión ${selected.policy.currentVersion}`, date: selected.policy.updatedAtUtc },
        { label: 'Activación', state: selected.policy.activatedAtUtc ? 'Activo' : 'Pendiente', date: selected.policy.activatedAtUtc },
        { label: 'Intento de publicación', state: selected.publication?.status ?? 'Sin publicación', date: selected.publication?.lastAttemptAtUtc },
        { label: 'Publicación Google', state: selected.publication?.googlePolicyName ?? 'Pendiente', date: selected.publication?.publishedAtUtc },
        { label: 'Verificación remota', state: verification ? (verification.existsInGoogle ? 'Verificada' : 'No encontrada') : 'No ejecutada', date: null },
        ...assignments
          .filter((assignment) => assignment.assignmentId)
          .map((assignment) => ({
            label: `Asignación: ${assignment.deviceName}`,
            state: assignment.assignmentStatus,
            date: assignment.updatedAtUtc,
          })),
      ]
    : []

  return (
    <>
      <TitanSectionCard
        title="Eventos de auditoría Android"
        description="Eventos administrativos y de acceso Android persistidos por el servidor (Android.* y RBAC.Android*), con paginación del lado del servidor."
        icon={<FileClock size={20} />}
      >
        <form
          className="launcher-tab-toolbar"
          onSubmit={(event) => {
            event.preventDefault()
            setAuditPage(1)
            setAuditTerm(auditQuery.trim())
          }}
        >
          <input
            value={auditQuery}
            onChange={(event) => setAuditQuery(event.target.value)}
            placeholder="Buscar por acción, dispositivo, operador o correlación"
            aria-label="Buscar en la auditoría Android"
          />
          <button type="submit" className="titan-btn">Buscar</button>
          {auditTerm && (
            <button
              type="button"
              className="titan-btn titan-btn--secondary"
              onClick={() => {
                setAuditQuery('')
                setAuditTerm('')
                setAuditPage(1)
              }}
            >
              Limpiar
            </button>
          )}
        </form>

        {auditLoading && !audit ? (
          <div className="launcher-muted"><Loader2 className="launcher-spin" size={16} /> Cargando eventos…</div>
        ) : auditError ? (
          <TitanEmptyState icon={<FileClock size={28} />} title="Auditoría no disponible" description={auditError} />
        ) : !audit?.items.length ? (
          <TitanEmptyState
            icon={<FileClock size={28} />}
            title="Sin eventos Android"
            description={auditTerm ? `No hay eventos que coincidan con «${auditTerm}».` : 'Aún no se han registrado eventos Android.'}
          />
        ) : (
          <>
            <div className="launcher-table-wrap">
              <table className="titan-table launcher-table">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Operador</th>
                    <th>Acción</th>
                    <th>Objetivo</th>
                    <th>Resultado</th>
                    <th>Correlación</th>
                  </tr>
                </thead>
                <tbody>
                  {audit.items.map((event: AndroidAuditEvent) => (
                    <tr key={event.id}>
                      <td>{formatDate(event.createdAtUtc)}</td>
                      <td><strong>{event.actorName}</strong><small>{event.actorEmail ?? 'Sistema'}</small></td>
                      <td>{event.action}</td>
                      <td>{event.targetType}{event.targetId ? ` · ${event.targetId}` : ''}</td>
                      <td><TitanStatusBadge label={event.result} tone={badgeTone(event.result)} /></td>
                      <td><small>{event.correlationId ?? event.ipAddress ?? '…"'}</small></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="launcher-tab-toolbar launcher-pagination">
              <span>
                Página {audit.page} de {auditTotalPages} · {audit.totalCount} eventos
              </span>
              <button
                type="button"
                className="titan-btn titan-btn--secondary"
                disabled={auditLoading || audit.page <= 1}
                onClick={() => setAuditPage(audit.page - 1)}
              >
                Anterior
              </button>
              <button
                type="button"
                className="titan-btn titan-btn--secondary"
                disabled={auditLoading || audit.page >= auditTotalPages}
                onClick={() => setAuditPage(audit.page + 1)}
              >
                Siguiente
              </button>
            </div>
          </>
        )}
      </TitanSectionCard>

      {selected ? (
        <TitanSectionCard title="Auditoría operativa" description="Línea de tiempo derivada de la política, publicación y asignaciones; no contiene eventos simulados." icon={<FileClock size={20} />}>
          <div className="launcher-audit-list">{timeline.map((event, index) => <article key={`${event.label}-${index}`}><span /><div><strong>{event.label}</strong><small>{formatDate(event.date)}</small></div><TitanStatusBadge label={event.state} tone={badgeTone(event.state)} /></article>)}</div>
        </TitanSectionCard>
      ) : (
        <TitanEmptyState icon={<FileClock size={28} />} title="Sin contexto de auditoría" description="Selecciona un perfil para revisar su línea de tiempo." />
      )}
    </>
  )
}
