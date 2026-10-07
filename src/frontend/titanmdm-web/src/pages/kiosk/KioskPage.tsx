import {
  Monitor,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  devicesApi,
} from '../../api/devicesApi'

import {
  kioskApi,
  type WindowsKioskApplication,
  type WindowsKioskRestrictions,
  type KioskMode,
} from '../../api/kioskApi'

import type {
  Policy,
} from '../../api/policiesApi'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  KioskPlatformSelector,
  type KioskPlatform,
} from './components/KioskPlatformSelector'

import {
  KioskProfilesList,
} from './components/KioskProfilesList'

import {
  WindowsKioskEditor,
} from './components/WindowsKioskEditor'

import './KioskPage.css'

export function KioskPage() {
  const [
    platform,
    setPlatform,
  ] =
    useState<KioskPlatform>(
      'Windows',
    )

  const [
    profiles,
    setProfiles,
  ] =
    useState<Policy[]>([])

  const [
    windowsDevices,
    setWindowsDevices,
  ] =
    useState<
      DeviceListItem[]
    >([])

  const [
    selectedProfile,
    setSelectedProfile,
  ] =
    useState<
      Policy | null
    >(null)

  const [
    selectedDeviceId,
    setSelectedDeviceId,
  ] =
    useState('')

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    saving,
    setSaving,
  ] =
    useState(false)

  const [
    message,
    setMessage,
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

  const load =
    useCallback(
      async () => {
        try {
          setLoading(true)
          setError(null)

          const [
            profileResult,
            deviceResult,
          ] =
            await Promise.all([
              platform ===
                'Windows'
                ? kioskApi
                    .getWindowsProfiles()
                : kioskApi
                    .getAndroidProfiles(),

              devicesApi
                .getDevices({
                  platform,
                  page:
                    1,

                  pageSize:
                    200,
                }),
            ])

          setProfiles(
            profileResult,
          )

          if (
            platform ===
            'Windows')
          {
            setWindowsDevices(
              deviceResult.items,
            )
          }
        } catch {
          setError(
            'No fue posible cargar Kiosk.',
          )
        } finally {
          setLoading(false)
        }
      },
      [
        platform,
      ],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  useEffect(
    () => {
      setSelectedProfile(
        null,
      )

      setSelectedDeviceId('')
      setMessage(null)
      setError(null)
    },
    [
      platform,
    ],
  )

  const managedWindowsDevices =
    useMemo(
      () =>
        windowsDevices.filter(
          device =>
            device.isManaged,
        ),
      [
        windowsDevices,
      ],
    )

  async function createWindowsProfile(
    request: {
      name: string
      description?: string
      mode: KioskMode
      account: string
      applications:
        WindowsKioskApplication[]
      restrictions:
        WindowsKioskRestrictions
    },
  ) {
    try {
      setSaving(true)
      setError(null)
      setMessage(null)

      const created =
        await kioskApi
          .createWindowsProfile({
            name:
              request.name,

            description:
              request.description,

            configuration: {
              titanProfileType:
                'kiosk',

              kioskMode:
                request.mode,

              account:
                request.account,

              applications:
                request.applications,

              restrictions:
                request.restrictions,
            },
          })

      await kioskApi
        .activateWindows(
          created.id,
        )

      setMessage(
        'Perfil Windows Kiosk creado y activado.',
      )

      await load()
    } catch {
      setError(
        'No fue posible crear el perfil Windows Kiosk.',
      )
    } finally {
      setSaving(false)
    }
  }

  async function assignWindows() {
    if (
      !selectedProfile
      ||
      !selectedDeviceId)
    {
      setError(
        'Selecciona un perfil y un dispositivo Windows.',
      )

      return
    }

    try {
      setSaving(true)
      setError(null)
      setMessage(null)

      await kioskApi
        .assignWindows(
          selectedProfile.id,
          selectedDeviceId,
        )

      setMessage(
        'Perfil enviado al dispositivo. El agente aplicará Assigned Access.',
      )

      await load()
    } catch {
      setError(
        'No fue posible asignar el perfil Kiosk.',
      )
    } finally {
      setSaving(false)
    }
  }

  async function queryStatus() {
    if (!selectedDeviceId) {
      setError(
        'Selecciona un dispositivo.',
      )

      return
    }

    const command =
      await kioskApi
        .getWindowsStatus(
          selectedDeviceId,
        )

    setMessage(
      `Consulta Kiosk enviada. Command ID: ${command.id}`,
    )
  }

  async function removeKiosk() {
    if (!selectedDeviceId) {
      setError(
        'Selecciona un dispositivo.',
      )

      return
    }

    if (
      !window.confirm(
        '¿Eliminar Assigned Access y restaurar las restricciones previas del dispositivo?',
      ))
    {
      return
    }

    const command =
      await kioskApi
        .removeWindowsKiosk(
          selectedDeviceId,
        )

    setMessage(
      `Retiro Kiosk enviado. Command ID: ${command.id}`,
    )
  }

  return (
    <div className="kiosk-page">
      <header className="kiosk-header">
        <div>
          <span className="kiosk-eyebrow">
            TITANMDM KIOSK
          </span>

          <h1>
            Kiosk & Dedicated Devices
          </h1>

          <p>
            Administración de endpoints dedicados Windows y Android.
          </p>
        </div>

        <button
          type="button"
          className="kiosk-secondary"
          onClick={() =>
            void load()
          }
        >
          <RefreshCw size={16} />

          Actualizar
        </button>
      </header>

      <KioskPlatformSelector
        value={platform}
        onChange={
          setPlatform
        }
      />

      {message && (
        <div className="kiosk-message success">
          {message}
        </div>
      )}

      {error && (
        <div className="kiosk-message error">
          {error}
        </div>
      )}

      {platform ===
      'Windows' ? (
        <>
          <section className="kiosk-summary">
            <Summary
              label="Perfiles"
              value={profiles.length}
            />

            <Summary
              label="Dispositivos"
              value={
                managedWindowsDevices.length
              }
            />

            <Summary
              label="Tecnología"
              value="Assigned Access"
            />

            <Summary
              label="Control"
              value="Titan Agent"
            />
          </section>

          <div className="kiosk-layout">
            <WindowsKioskEditor
              saving={saving}
              onCreate={
                createWindowsProfile
              }
            />

            <KioskProfilesList
              loading={loading}
              profiles={profiles}
              selectedId={
                selectedProfile?.id
                ??
                ''
              }
              onSelect={
                setSelectedProfile
              }
            />
          </div>

          <section className="kiosk-windows-deployment">
            <header>
              <div>
                <Monitor size={18} />

                <strong>
                  Deployment Windows
                </strong>
              </div>

              <span>
                Aplicación y retiro remoto de Assigned Access
              </span>
            </header>

            <div className="kiosk-deployment-grid">
              <label>
                Perfil
                <input
                  readOnly
                  value={
                    selectedProfile?.name
                    ??
                    'Selecciona un perfil'
                  }
                />
              </label>

              <label>
                Dispositivo
                <select
                  value={
                    selectedDeviceId
                  }
                  onChange={
                    event =>
                      setSelectedDeviceId(
                        event.target.value,
                      )
                  }
                >
                  <option value="">
                    Seleccionar...
                  </option>

                  {managedWindowsDevices.map(
                    device => (
                      <option
                        key={device.id}
                        value={device.id}
                      >
                        {device.deviceName}
                        {' · '}
                        {device.status}
                      </option>
                    ),
                  )}
                </select>
              </label>
            </div>

            <div className="kiosk-deployment-actions">
              <button
                type="button"
                disabled={
                  saving
                  ||
                  !selectedProfile
                  ||
                  !selectedDeviceId
                }
                onClick={() =>
                  void assignWindows()
                }
              >
                <ShieldCheck size={16} />

                Aplicar Kiosk
              </button>

              <button
                type="button"
                disabled={
                  !selectedDeviceId
                }
                onClick={() =>
                  void queryStatus()
                }
              >
                Consultar estado
              </button>

              <button
                type="button"
                className="danger"
                disabled={
                  !selectedDeviceId
                }
                onClick={() =>
                  void removeKiosk()
                }
              >
                Retirar Kiosk
              </button>
            </div>
          </section>
        </>
      ) : (
        <section className="kiosk-android-preserved">
          <strong>
            Android Enterprise
          </strong>

          <p>
            La implementación Android existente se conserva. La retomaremos en la fase Android después de Windows y Mesa de Ayuda.
          </p>

          <KioskProfilesList
            loading={loading}
            profiles={profiles}
            selectedId=""
            onSelect={() => {
            }}
          />
        </section>
      )}
    </div>
  )
}

function Summary({
  label,
  value,
}: {
  label: string

  value:
    string | number
}) {
  return (
    <article className="kiosk-summary-card">
      <div>
        <ShieldCheck size={19} />
      </div>

      <span>
        {label}
      </span>

      <strong>
        {value}
      </strong>
    </article>
  )
}