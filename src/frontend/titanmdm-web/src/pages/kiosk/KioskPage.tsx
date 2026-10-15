import {
  AppWindow,
  CircleAlert,
  Monitor,
  RefreshCw,
  Rocket,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { useSearchParams } from 'react-router-dom'

import { devicesApi } from '../../api/devicesApi'
import {
  kioskApi,
  type AndroidKioskConfiguration,
  type KioskMode,
  type WindowsKioskApplication,
  type WindowsKioskRestrictions,
} from '../../api/kioskApi'
import type { Policy } from '../../api/policiesApi'
import { useAuth } from '../../auth/AuthContext'
import {
  TitanEmptyState,
  TitanPageHeader,
  TitanSectionCard,
  TitanStatCard,
  TitanStatusBadge,
} from '../../components/ui'
import type { DeviceListItem } from '../../types/device'
import { useWorkspace } from '../../workspace/WorkspaceContext'
import { AndroidKioskEditor } from './components/AndroidKioskEditor'
import {
  KioskPlatformSelector,
  type KioskPlatform,
} from './components/KioskPlatformSelector'
import { KioskProfilesList } from './components/KioskProfilesList'
import { WindowsKioskEditor } from './components/WindowsKioskEditor'
import { resolveKioskPlatform } from './kioskPlatform'
import './KioskPage.css'

function getErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as {
      response?: { data?: { message?: string } }
    }).response
    if (response?.data?.message) return response.data.message
  }

  return error instanceof Error ? error.message : fallback
}

export function KioskPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const { activeWorkspaceId, selectWorkspace } = useWorkspace()
  const { hasPermission } = useAuth()
  const requestedPlatform = resolveKioskPlatform(
    searchParams.get('workspace'),
    activeWorkspaceId,
  )
  const platform = requestedPlatform
  const [profiles, setProfiles] = useState<Policy[]>([])
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [selectedProfile, setSelectedProfile] = useState<Policy | null>(null)
  const [selectedDeviceId, setSelectedDeviceId] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)

  const canManage = platform === 'Android'
    ? hasPermission('android.enterprise.manage') && hasPermission('policies.manage')
    : hasPermission('kiosk.manage') && hasPermission('policies.manage')

  const load = useCallback(async () => {
    const currentRequest = ++requestId.current
    setLoading(true)
    setError(null)

    try {
      const [profileResult, deviceResult] = await Promise.all([
        platform === 'Windows'
          ? kioskApi.getWindowsProfiles()
          : kioskApi.getAndroidProfiles(),
        devicesApi.getDevices({
          platform,
          page: 1,
          pageSize: 200,
        }),
      ])

      if (currentRequest !== requestId.current) return

      const matchingDevices = deviceResult.items.filter(
        (device) => device.platform === platform,
      )
      setProfiles(profileResult.filter((profile) => profile.platform === platform))
      setDevices(matchingDevices)
      setSelectedProfile((current) =>
        profileResult.find((profile) => profile.id === current?.id) ?? null,
      )
      setSelectedDeviceId((current) =>
        matchingDevices.some((device) => device.id === current) ? current : '',
      )
    } catch (loadError) {
      if (currentRequest === requestId.current) {
        setProfiles([])
        setDevices([])
        setError(getErrorMessage(loadError, 'No fue posible cargar Kiosk.'))
      }
    } finally {
      if (currentRequest === requestId.current) setLoading(false)
    }
  }, [platform])

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timeout)
  }, [load])

  const managedDevices = useMemo(
    () => devices.filter((device) => device.isManaged),
    [devices],
  )

  function changePlatform(nextPlatform: KioskPlatform) {
    requestId.current++
    const workspace = nextPlatform.toLowerCase() as 'android' | 'windows'
    const next = new URLSearchParams(searchParams)
    next.set('workspace', workspace)
    setSearchParams(next)
    selectWorkspace(workspace)
    setSelectedProfile(null)
    setSelectedDeviceId('')
    setMessage(null)
    setError(null)
  }

  async function createWindowsProfile(request: {
    name: string
    description?: string
    mode: KioskMode
    account: string
    applications: WindowsKioskApplication[]
    restrictions: WindowsKioskRestrictions
  }) {
    if (!canManage) return
    try {
      setSaving(true)
      setError(null)
      setMessage(null)
      const created = await kioskApi.createWindowsProfile({
        name: request.name,
        description: request.description,
        configuration: {
          titanProfileType: 'kiosk',
          kioskMode: request.mode,
          account: request.account,
          applications: request.applications,
          restrictions: request.restrictions,
        },
      })
      await kioskApi.activateWindows(created.id)
      setMessage('Perfil Windows Kiosk creado y activado.')
      await load()
    } catch (saveError) {
      setError(getErrorMessage(saveError, 'No fue posible crear el perfil Windows Kiosk.'))
    } finally {
      setSaving(false)
    }
  }

  async function createAndroidProfile(request: {
    name: string
    description?: string
    configuration: AndroidKioskConfiguration
  }): Promise<boolean> {
    if (!canManage) return false
    try {
      setSaving(true)
      setError(null)
      setMessage(null)
      const created = await kioskApi.createAndroidProfile(request)
      await kioskApi.activateAndroid(created.id)
      setMessage('Perfil Android creado, activado y publicado en Android Enterprise.')
      await load()
      return true
    } catch (saveError) {
      setError(getErrorMessage(saveError, 'No fue posible crear o publicar el perfil Android.'))
      return false
    } finally {
      setSaving(false)
    }
  }

  async function assignSelected() {
    if (!selectedProfile || !selectedDeviceId || !canManage) return
    if (selectedProfile.status !== 'Active') {
      setError('Solo los perfiles activos se pueden asignar.')
      return
    }

    try {
      setSaving(true)
      setError(null)
      setMessage(null)
      if (platform === 'Android') {
        await kioskApi.assignAndroid(selectedProfile.id, selectedDeviceId)
        setMessage('Asignación aceptada por Android Management API. La aplicación queda pendiente de sincronización.')
      } else {
        await kioskApi.assignWindows(selectedProfile.id, selectedDeviceId)
        setMessage('Perfil enviado al dispositivo. El agente aplicará Assigned Access.')
      }
      await load()
    } catch (assignError) {
      setError(getErrorMessage(assignError, 'No fue posible asignar el perfil Kiosk.'))
    } finally {
      setSaving(false)
    }
  }

  async function queryWindowsStatus() {
    if (!selectedDeviceId) return
    try {
      setError(null)
      const command = await kioskApi.getWindowsStatus(selectedDeviceId)
      setMessage(`Consulta Kiosk enviada. Command ID: ${command.id}`)
    } catch (statusError) {
      setError(getErrorMessage(statusError, 'No fue posible consultar Assigned Access.'))
    }
  }

  async function removeWindowsKiosk() {
    if (!selectedDeviceId || !canManage) return
    if (!window.confirm('¿Eliminar Assigned Access y restaurar las restricciones previas del dispositivo?')) return
    try {
      setError(null)
      const command = await kioskApi.removeWindowsKiosk(selectedDeviceId)
      setMessage(`Retiro Kiosk enviado. Command ID: ${command.id}`)
    } catch (removeError) {
      setError(getErrorMessage(removeError, 'No fue posible retirar Assigned Access.'))
    }
  }

  const activeProfiles = profiles.filter((profile) => profile.status === 'Active')
  const PlatformIcon = platform === 'Android' ? Smartphone : Monitor

  return (
    <main className="titan-page kiosk-page">
      <TitanPageHeader
        eyebrow="TITANMDM KIOSK"
        title="Kiosk & Dedicated Devices"
        description={platform === 'Android'
          ? 'Configura una experiencia single-app y publícala mediante Android Enterprise.'
          : 'Administra perfiles Assigned Access y su aplicación remota mediante Titan Agent.'}
        icon={<PlatformIcon size={17} />}
        actions={(
          <button
            type="button"
            className="titan-btn titan-btn--secondary"
            onClick={() => void load()}
            disabled={loading}
          >
            <RefreshCw size={16} className={loading ? 'kiosk-spin' : ''} />
            Actualizar
          </button>
        )}
      />

      <KioskPlatformSelector value={platform} onChange={changePlatform} />

      {message && <div className="kiosk-notice success" role="status">{message}</div>}
      {error && <div className="kiosk-notice error" role="alert"><CircleAlert size={17} />{error}</div>}

      <section className="titan-kpi-grid kiosk-stats">
        <TitanStatCard icon={<AppWindow size={22} />} label="Perfiles" value={profiles.length} loading={loading} />
        <TitanStatCard icon={<ShieldCheck size={22} />} label="Activos" value={activeProfiles.length} loading={loading} />
        <TitanStatCard icon={<PlatformIcon size={22} />} label="Administrados" value={managedDevices.length} loading={loading} />
        <TitanStatCard
          icon={<Rocket size={22} />}
          label="Tecnología"
          value={platform === 'Android' ? 'AMAPI' : 'Assigned Access'}
          hint={platform === 'Android' ? 'Google policy' : 'Titan Agent'}
        />
      </section>

      <section className="kiosk-workspace-grid">
        <TitanSectionCard
          title={platform === 'Android' ? 'Nuevo Kiosk Android' : 'Nuevo Kiosk Windows'}
          description={platform === 'Android'
            ? 'Una aplicación, controles dedicados y publicación canónica.'
            : 'Configura Assigned Access single-app o multi-app.'}
          icon={platform === 'Android' ? <Smartphone size={20} /> : <Monitor size={20} />}
          className="kiosk-editor-card"
        >
          {platform === 'Android' ? (
            <AndroidKioskEditor saving={saving} canManage={canManage} onCreate={createAndroidProfile} />
          ) : (
            <WindowsKioskEditor saving={saving} canManage={canManage} onCreate={createWindowsProfile} />
          )}
        </TitanSectionCard>

        <TitanSectionCard
          title={`Perfiles ${platform}`}
          description="Selecciona un perfil activo para desplegarlo. Activo no implica aplicado."
          icon={<ShieldCheck size={20} />}
          className="kiosk-profiles-card"
        >
          <KioskProfilesList
            loading={loading}
            profiles={profiles}
            selectedId={selectedProfile?.id ?? ''}
            onSelect={setSelectedProfile}
          />
        </TitanSectionCard>
      </section>

      <TitanSectionCard
        title={`Despliegue ${platform}`}
        description={platform === 'Android'
          ? 'Solo perfiles activos y dispositivos Android administrados.'
          : 'Aplicación, consulta y retiro remoto de Assigned Access.'}
        icon={<Rocket size={20} />}
      >
        {!loading && managedDevices.length === 0 ? (
          <TitanEmptyState
            icon={<PlatformIcon size={28} />}
            title={`No hay dispositivos ${platform} administrados`}
            description="Sincroniza o inscribe dispositivos antes de asignar este perfil."
          />
        ) : (
          <div className="kiosk-deployment">
            <div className="kiosk-deployment-fields">
              <label>
                <span>Perfil</span>
                <input readOnly value={selectedProfile?.name ?? 'Selecciona un perfil'} />
              </label>
              <label>
                <span>Dispositivo {platform}</span>
                <select value={selectedDeviceId} onChange={(event) => setSelectedDeviceId(event.target.value)}>
                  <option value="">Seleccionar...</option>
                  {managedDevices.map((device) => (
                    <option key={device.id} value={device.id}>
                      {device.deviceName} · {device.status}
                    </option>
                  ))}
                </select>
              </label>
              <div className="kiosk-selected-status">
                <span>Elegibilidad</span>
                <TitanStatusBadge
                  label={!selectedProfile
                    ? 'Sin selección'
                    : selectedProfile.status === 'Active' ? 'Listo' : 'Requiere perfil activo'}
                  tone={selectedProfile?.status === 'Active' ? 'success' : 'warning'}
                />
              </div>
            </div>
            <div className="kiosk-deployment-actions">
              <button
                type="button"
                className="titan-btn titan-btn--primary"
                disabled={!canManage || saving || !selectedDeviceId || selectedProfile?.status !== 'Active'}
                onClick={() => void assignSelected()}
              >
                <ShieldCheck size={16} /> Aplicar Kiosk
              </button>
              {platform === 'Windows' && (
                <>
                  <button
                    type="button"
                    className="titan-btn titan-btn--secondary"
                    disabled={!selectedDeviceId}
                    onClick={() => void queryWindowsStatus()}
                  >
                    Consultar estado
                  </button>
                  <button
                    type="button"
                    className="titan-btn kiosk-danger"
                    disabled={!canManage || !selectedDeviceId}
                    onClick={() => void removeWindowsKiosk()}
                  >
                    Retirar Kiosk
                  </button>
                </>
              )}
            </div>
            {!canManage && (
              <div className="kiosk-permission-note">
                Las acciones están deshabilitadas porque tu cuenta no posee los permisos de administración requeridos.
              </div>
            )}
          </div>
        )}
      </TitanSectionCard>
    </main>
  )
}
