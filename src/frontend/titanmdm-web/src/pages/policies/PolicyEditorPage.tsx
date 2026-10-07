import {
  ArrowLeft,
  CheckCircle2,
  ChevronRight,
  Cloud,
  CloudCog,
  ExternalLink,
  KeyRound,
  Laptop,
  Loader2,
  LockKeyhole,
  Network,
  RefreshCw,
  Save,
  Shield,
  ShieldCheck,
  Smartphone,
  Usb,
  Wifi,
  XCircle,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  useNavigate,
  useParams,
} from 'react-router-dom'

import {
  policiesApi,
  type AndroidPolicyPublication,
  type AndroidPolicyRemoteVerificationResult,
  type PolicyPlatform,
} from '../../api/policiesApi'

import './PolicyEditorPage.css'
import AndroidPolicyAssignmentPanel
  from './AndroidPolicyAssignmentPanel'
// ============================================================
// CONFIGURATION TYPES
// ============================================================

interface WindowsPolicyConfiguration {
  password: {
    enabled: boolean
    minimumLength: number
    requireUppercase: boolean
    requireLowercase: boolean
    requireNumber: boolean
    requireSpecialCharacter: boolean
    maximumAgeDays: number
  }

  screenLock: {
    enabled: boolean
    timeoutMinutes: number
  }

  defender: {
    enabled: boolean
    realTimeProtection: boolean
    cloudProtection: boolean
  }

  firewall: {
    enabled: boolean
    domainProfile: boolean
    privateProfile: boolean
    publicProfile: boolean
  }

  usb: {
    blockRemovableStorage: boolean
  }

  windowsUpdate: {
    enabled: boolean
    automaticUpdates: boolean
    restartOutsideActiveHours: boolean
  }
}

interface AndroidPolicyConfiguration {
  password: {
    enabled: boolean
    minimumLength: number
    complexity:
      | 'NONE'
      | 'LOW'
      | 'MEDIUM'
      | 'HIGH'
    maxFailedAttempts: number
    screenLockTimeout: number
    requireNumeric: boolean
    requireComplex: boolean
  }

  restrictions: {
    blockCamera: boolean
    blockScreenCapture: boolean
    blockUsbFileTransfer: boolean
    blockBluetooth: boolean
    blockUnknownSources: boolean
    blockDebugging: boolean
    blockFactoryReset: boolean
    blockSafeBoot: boolean
    blockAddUser: boolean
    blockRemoveUser: boolean
    blockModifyAccounts: boolean
    blockOutgoingBeam: boolean
    blockPrinting: boolean
    blockMicrophone: boolean
  }

  applications: {
    allowAppInstallation: boolean
    allowAppUninstallation: boolean
    installMode:
      | 'AVAILABLE'
      | 'FORCE_INSTALLED'
      | 'BLOCKED'
    playStoreMode:
      | 'WHITELIST'
      | 'BLACKLIST'
      | 'UNSPECIFIED'
    permittedInputMethods: string
  }

  network: {
    wifiConfigDisabled: boolean
    bluetoothConfigDisabled: boolean
    tetheringDisabled: boolean
    vpnConfigDisabled: boolean
    privateDnsMode:
      | 'UNSPECIFIED'
      | 'OPPORTUNISTIC'
      | 'OFF'
  }

  location: {
    mode:
      | 'UNSPECIFIED'
      | 'ENFORCED'
      | 'USER_CHOICE'
      | 'DISABLED'
  }

  systemUpdate: {
    type:
      | 'AUTOMATIC'
      | 'WINDOWED'
      | 'POSTPONE'
    startMinutes: number
    endMinutes: number
  }

  kiosk: {
    enabled: boolean
    applicationId: string
    statusBar: boolean
    systemNavigation: boolean
    keyguard: boolean
  }

  compliance: {
    minimumApiLevel: number
    minimumSecurityPatch: string
    requireEncryption: boolean
    requireDeviceIntegrity: boolean
  }
}

interface PolicyConfiguration {
  schemaVersion: number
  windows: WindowsPolicyConfiguration
  android: AndroidPolicyConfiguration
}

// ============================================================
// DEFAULT CONFIGURATION
// ============================================================

const defaultConfiguration =
  (): PolicyConfiguration => ({
    schemaVersion: 2,

    windows: {
      password: {
        enabled: false,
        minimumLength: 8,
        requireUppercase: true,
        requireLowercase: true,
        requireNumber: true,
        requireSpecialCharacter: false,
        maximumAgeDays: 90,
      },

      screenLock: {
        enabled: false,
        timeoutMinutes: 15,
      },

      defender: {
        enabled: false,
        realTimeProtection: true,
        cloudProtection: true,
      },

      firewall: {
        enabled: false,
        domainProfile: true,
        privateProfile: true,
        publicProfile: true,
      },

      usb: {
        blockRemovableStorage: false,
      },

      windowsUpdate: {
        enabled: false,
        automaticUpdates: true,
        restartOutsideActiveHours: true,
      },
    },

    android: {
      password: {
        enabled: false,
        minimumLength: 6,
        complexity: 'MEDIUM',
        maxFailedAttempts: 10,
        screenLockTimeout: 5,
        requireNumeric: true,
        requireComplex: false,
      },

      restrictions: {
        blockCamera: false,
        blockScreenCapture: false,
        blockUsbFileTransfer: false,
        blockBluetooth: false,
        blockUnknownSources: true,
        blockDebugging: true,
        blockFactoryReset: false,
        blockSafeBoot: false,
        blockAddUser: false,
        blockRemoveUser: false,
        blockModifyAccounts: false,
        blockOutgoingBeam: false,
        blockPrinting: false,
        blockMicrophone: false,
      },

      applications: {
        allowAppInstallation: true,
        allowAppUninstallation: true,
        installMode: 'AVAILABLE',
        playStoreMode: 'UNSPECIFIED',
        permittedInputMethods: '',
      },

      network: {
        wifiConfigDisabled: false,
        bluetoothConfigDisabled: false,
        tetheringDisabled: false,
        vpnConfigDisabled: false,
        privateDnsMode: 'UNSPECIFIED',
      },

      location: {
        mode: 'UNSPECIFIED',
      },

      systemUpdate: {
        type: 'AUTOMATIC',
        startMinutes: 120,
        endMinutes: 300,
      },

      kiosk: {
        enabled: false,
        applicationId: '',
        statusBar: false,
        systemNavigation: false,
        keyguard: false,
      },

      compliance: {
        minimumApiLevel: 0,
        minimumSecurityPatch: '',
        requireEncryption: true,
        requireDeviceIntegrity: true,
      },
    },
  })

// ============================================================
// CONFIGURATION PARSER
// ============================================================

function parseConfiguration(
  value: string,
): PolicyConfiguration {
  const defaults =
    defaultConfiguration()

  try {
    const parsed =
      JSON.parse(
        value,
      ) as Partial<PolicyConfiguration>

    return {
      schemaVersion:
        parsed.schemaVersion ??
        defaults.schemaVersion,

      windows: {
        password: {
          ...defaults.windows.password,
          ...parsed.windows?.password,
        },

        screenLock: {
          ...defaults.windows.screenLock,
          ...parsed.windows?.screenLock,
        },

        defender: {
          ...defaults.windows.defender,
          ...parsed.windows?.defender,
        },

        firewall: {
          ...defaults.windows.firewall,
          ...parsed.windows?.firewall,
        },

        usb: {
          ...defaults.windows.usb,
          ...parsed.windows?.usb,
        },

        windowsUpdate: {
          ...defaults.windows.windowsUpdate,
          ...parsed.windows?.windowsUpdate,
        },
      },

      android: {
        password: {
          ...defaults.android.password,
          ...parsed.android?.password,
        },

        restrictions: {
          ...defaults.android.restrictions,
          ...parsed.android?.restrictions,
        },

        applications: {
          ...defaults.android.applications,
          ...parsed.android?.applications,
        },

        network: {
          ...defaults.android.network,
          ...parsed.android?.network,
        },

        location: {
          ...defaults.android.location,
          ...parsed.android?.location,
        },

        systemUpdate: {
          ...defaults.android.systemUpdate,
          ...parsed.android?.systemUpdate,
        },

        kiosk: {
          ...defaults.android.kiosk,
          ...parsed.android?.kiosk,
        },

        compliance: {
          ...defaults.android.compliance,
          ...parsed.android?.compliance,
        },
      },
    }
  } catch {
    return defaults
  }
}

// ============================================================
// HELPERS
// ============================================================

function getErrorMessage(
  error: unknown,
): string {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error
  ) {
    const response =
      (
        error as {
          response?: {
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (response?.data?.message) {
      return response.data.message
    }
  }

  if (
    error instanceof Error &&
    error.message
  ) {
    return error.message
  }

  return 'No fue posible completar la operación.'
}

function formatDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return 'N/D'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function publicationStatusLabel(
  status: string | null | undefined,
): string {
  switch (status) {
    case 'Published':
      return 'Publicada'

    case 'Publishing':
      return 'Publicando'

    case 'Pending':
      return 'Pendiente'

    case 'Failed':
      return 'Fallida'

    case 'Deleted':
      return 'Eliminada'

    default:
      return 'No publicada'
  }
}

function publicationStatusClass(
  status: string | null | undefined,
): string {
  switch (status) {
    case 'Published':
      return 'android-publication-status android-publication-status--published'

    case 'Publishing':
      return 'android-publication-status android-publication-status--publishing'

    case 'Failed':
      return 'android-publication-status android-publication-status--failed'

    default:
      return 'android-publication-status'
  }
}

// ============================================================
// TOGGLE
// ============================================================

interface ToggleProps {
  checked: boolean
  onChange: (value: boolean) => void
  disabled?: boolean
}

function Toggle({
  checked,
  onChange,
  disabled = false,
}: ToggleProps) {
  return (
    <button
      type="button"
      className={
        checked
          ? 'policy-toggle policy-toggle--active'
          : 'policy-toggle'
      }
      disabled={disabled}
      aria-pressed={checked}
      onClick={() =>
        onChange(!checked)
      }
    >
      <span />
    </button>
  )
}

// ============================================================
// MAIN PAGE
// ============================================================

export function PolicyEditorPage() {
  const navigate = useNavigate()

  const { policyId } =
    useParams<{
      policyId: string
    }>()

  const editing =
    Boolean(policyId)

  const [name, setName] =
    useState('')

  const [description, setDescription] =
    useState('')

  const [platform, setPlatform] =
    useState<PolicyPlatform>(
      'Windows',
    )

  const [
    configuration,
    setConfiguration,
  ] =
    useState<PolicyConfiguration>(
      defaultConfiguration,
    )

  const [loading, setLoading] =
    useState(editing)

  const [saving, setSaving] =
    useState(false)

  const [error, setError] =
    useState<string | null>(null)

  const [message, setMessage] =
    useState<string | null>(null)

  const [
    publication,
    setPublication,
  ] =
    useState<AndroidPolicyPublication | null>(
      null,
    )

  const [
    loadingPublication,
    setLoadingPublication,
  ] =
    useState(false)

  const [
    publishing,
    setPublishing,
  ] =
    useState(false)

  const [
    verifying,
    setVerifying,
  ] =
    useState(false)

  const [
    verification,
    setVerification,
  ] =
    useState<AndroidPolicyRemoteVerificationResult | null>(
      null,
    )

  // ==========================================================
  // LOAD POLICY
  // ==========================================================

  const loadPolicy =
    useCallback(async () => {
      if (!policyId) {
        return
      }

      try {
        setLoading(true)
        setError(null)

        const policy =
          await policiesApi.getById(
            policyId,
          )

        setName(
          policy.name,
        )

        setDescription(
          policy.description ?? '',
        )

        setPlatform(
          policy.platform,
        )

        setConfiguration(
          parseConfiguration(
            policy.configurationJson,
          ),
        )
      } catch (loadError) {
        console.error(
          loadError,
        )

        setError(
          getErrorMessage(
            loadError,
          ),
        )
      } finally {
        setLoading(false)
      }
    }, [policyId])

  // ==========================================================
  // LOAD ANDROID PUBLICATION
  // ==========================================================

  const loadAndroidPublication =
    useCallback(async () => {
      if (!policyId) {
        setPublication(null)
        return
      }

      try {
        setLoadingPublication(
          true,
        )

        const result =
          await policiesApi
            .getAndroidPublication(
              policyId,
            )

        setPublication(
          result,
        )
      } catch (publicationError) {
        console.error(
          publicationError,
        )

        setError(
          getErrorMessage(
            publicationError,
          ),
        )
      } finally {
        setLoadingPublication(
          false,
        )
      }
    }, [policyId])

  useEffect(() => {
    void loadPolicy()
  }, [loadPolicy])

  useEffect(() => {
    if (
      editing &&
      platform === 'Android'
    ) {
      void loadAndroidPublication()
    }
  }, [
    editing,
    platform,
    loadAndroidPublication,
  ])

  useEffect(() => {
    document.title =
      editing
        ? 'Editar política | TitanMDM'
        : 'Nueva política | TitanMDM'
  }, [editing])

  // ==========================================================
  // JSON
  // ==========================================================

  const configurationJson =
    useMemo(
      () =>
        JSON.stringify(
          configuration,
          null,
          2,
        ),
      [configuration],
    )

  // ==========================================================
  // WINDOWS UPDATE
  // ==========================================================

  const updateWindows =
    <
      K extends keyof WindowsPolicyConfiguration,
    >(
      section: K,
      values: Partial<
        WindowsPolicyConfiguration[K]
      >,
    ) => {
      setConfiguration(
        (current) => ({
          ...current,

          windows: {
            ...current.windows,

            [section]: {
              ...current.windows[
                section
              ],

              ...values,
            },
          },
        }),
      )
    }

  // ==========================================================
  // ANDROID UPDATE
  // ==========================================================

  const updateAndroid =
    <
      K extends keyof AndroidPolicyConfiguration,
    >(
      section: K,
      values: Partial<
        AndroidPolicyConfiguration[K]
      >,
    ) => {
      setConfiguration(
        (current) => ({
          ...current,

          android: {
            ...current.android,

            [section]: {
              ...current.android[
                section
              ],

              ...values,
            },
          },
        }),
      )
    }

  // ==========================================================
  // SAVE
  // ==========================================================

  const savePolicy =
    async () => {
      if (!name.trim()) {
        setError(
          'El nombre de la política es obligatorio.',
        )
        return
      }

      try {
        setSaving(true)
        setError(null)
        setMessage(null)
        setVerification(null)

        if (
          editing &&
          policyId
        ) {
          const result =
            await policiesApi.update(
              policyId,
              {
                name:
                  name.trim(),

                description:
                  description.trim() ||
                  null,

                configurationJson,
              },
            )

          setPublication(null)

          setMessage(
            `Política guardada. Nueva versión: v${result.currentVersion}.`,
          )

          if (
            platform ===
            'Android'
          ) {
            await loadAndroidPublication()
          }

          return
        }

        const result =
          await policiesApi.create(
            {
              name:
                name.trim(),

              description:
                description.trim() ||
                null,

              platform,

              configurationJson,
            },
          )

        navigate(
          `/policies/${result.id}`,
          {
            replace: true,
          },
        )
      } catch (saveError) {
        console.error(
          saveError,
        )

        setError(
          getErrorMessage(
            saveError,
          ),
        )
      } finally {
        setSaving(false)
      }
    }

  // ==========================================================
  // PUBLISH ANDROID
  // ==========================================================

  const publishAndroid =
    async () => {
      if (!policyId) {
        setError(
          'Primero debes guardar la política antes de publicarla.',
        )
        return
      }

      try {
        setPublishing(true)
        setError(null)
        setMessage(null)
        setVerification(null)

        const result =
          await policiesApi
            .publishAndroid(
              policyId,
            )

        setMessage(
          `Política Android v${result.policyVersion} publicada correctamente en Google Android Management.`,
        )

        await loadAndroidPublication()
      } catch (publishError) {
        console.error(
          publishError,
        )

        setError(
          getErrorMessage(
            publishError,
          ),
        )

        await loadAndroidPublication()
      } finally {
        setPublishing(false)
      }
    }

  // ==========================================================
  // VERIFY GOOGLE
  // ==========================================================

  const verifyAndroid =
    async () => {
      if (!policyId) {
        return
      }

      try {
        setVerifying(true)
        setError(null)
        setMessage(null)

        const result =
          await policiesApi
            .verifyAndroidPublication(
              policyId,
            )

        setVerification(
          result,
        )

        setMessage(
          result.existsInGoogle
            ? 'La política fue verificada directamente en Google Android Management.'
            : 'Google no reportó la política.',
        )
      } catch (verifyError) {
        console.error(
          verifyError,
        )

        setVerification(null)

        setError(
          getErrorMessage(
            verifyError,
          ),
        )
      } finally {
        setVerifying(false)
      }
    }

  // ==========================================================
  // LOADING
  // ==========================================================

  if (loading) {
    return (
      <div className="policy-editor-loading">
        Cargando política...
      </div>
    )
  }

  // ==========================================================
  // RENDER
  // ==========================================================

  return (
    <div className="policy-editor-page">
      <button
        type="button"
        className="policy-editor-back"
        onClick={() =>
          navigate('/policies')
        }
      >
        <ArrowLeft size={16} />
        Políticas
      </button>

      <header className="policy-editor-header">
        <div>
          <h1>
            {editing
              ? 'Editar política'
              : 'Nueva política'}
          </h1>

          <p>
            Define la configuración que
            TitanMDM aplicará a los
            dispositivos administrados.
          </p>
        </div>

        <button
          type="button"
          className="policy-editor-save"
          disabled={
            saving ||
            publishing
          }
          onClick={() => {
            void savePolicy()
          }}
        >
          {saving ? (
            <Loader2
              size={16}
              className="policy-spin"
            />
          ) : (
            <Save size={16} />
          )}

          {saving
            ? 'Guardando...'
            : 'Guardar política'}
        </button>
      </header>

      {error && (
        <div className="policy-editor-notice policy-editor-notice--error">
          <XCircle size={17} />
          {error}
        </div>
      )}

      {message && (
        <div className="policy-editor-notice policy-editor-notice--success">
          <CheckCircle2 size={17} />
          {message}
        </div>
      )}

      {/* ======================================================
          GENERAL
      ====================================================== */}

      <section className="policy-editor-card">
        <div className="policy-editor-card__header">
          <ShieldCheck size={18} />

          <div>
            <h2>
              Información general
            </h2>

            <p>
              Identificación y plataforma
              de administración.
            </p>
          </div>
        </div>

        <div className="policy-form-grid">
          <label>
            <span>
              Nombre de la política
            </span>

            <input
              value={name}
              maxLength={200}
              placeholder="Ej. Seguridad Android corporativa"
              onChange={(event) =>
                setName(
                  event.target.value,
                )
              }
            />
          </label>

          <label>
            <span>Plataforma</span>

            <select
              value={platform}
              disabled={editing}
              onChange={(event) =>
                setPlatform(
                  event.target
                    .value as PolicyPlatform,
                )
              }
            >
              <option value="Windows">
                Windows
              </option>

              <option value="Android">
                Android
              </option>
            </select>
          </label>

          <label className="policy-form-full">
            <span>Descripción</span>

            <textarea
              value={description}
              maxLength={1000}
              rows={3}
              placeholder="Describe el objetivo de esta política..."
              onChange={(event) =>
                setDescription(
                  event.target.value,
                )
              }
            />
          </label>
        </div>
      </section>

      {/* ======================================================
          PLATFORM
      ====================================================== */}

      <div className="policy-editor-platform">
        <button
          type="button"
          className={
            platform === 'Windows'
              ? 'active'
              : ''
          }
          disabled={editing}
          onClick={() =>
            setPlatform('Windows')
          }
        >
          <Laptop size={18} />
          Windows
        </button>

        <button
          type="button"
          className={
            platform === 'Android'
              ? 'active'
              : ''
          }
          disabled={editing}
          onClick={() =>
            setPlatform('Android')
          }
        >
          <Smartphone size={18} />
          Android
        </button>
      </div>

      {/* ======================================================
          WINDOWS
      ====================================================== */}

      {platform === 'Windows' ? (
        <WindowsPolicyEditor
          configuration={
            configuration.windows
          }
          updateWindows={
            updateWindows
          }
        />
      ) : (
        <>
          {/* ==================================================
              ANDROID ENTERPRISE PUBLICATION
          ================================================== */}

          {editing && (
            <section className="android-publication-card">
              <div className="android-publication-header">
                <div className="android-publication-title">
                  <CloudCog size={22} />

                  <div>
                    <h2>
                      Android Enterprise
                    </h2>

                    <p>
                      Publicación y
                      sincronización con
                      Google Android
                      Management API.
                    </p>
                  </div>
                </div>


                {loadingPublication ? (
                  <span className="android-publication-status">
                    <Loader2
                      size={14}
                      className="policy-spin"
                    />
                    Consultando...
                  </span>
                ) : (
                  <span
                    className={
                      publicationStatusClass(
                        publication?.status,
                      )
                    }
                  >
                    {publicationStatusLabel(
                      publication?.status,
                    )}
                  </span>
                )}
              </div>

              <div className="android-publication-grid">
                <PublicationField
                  label="Versión TitanMDM"
                  value={
                    publication
                      ? `v${publication.policyVersion}`
                      : 'Sin publicar'
                  }
                />

                <PublicationField
                  label="Google Policy ID"
                  value={
                    publication?.googlePolicyId ??
                    'Pendiente'
                  }
                  mono
                />

                <PublicationField
                  label="Google Policy Name"
                  value={
                    publication?.googlePolicyName ??
                    'Pendiente'
                  }
                  mono
                />

                <PublicationField
                  label="Publicada"
                  value={formatDate(
                    publication?.publishedAtUtc,
                  )}
                />

                <PublicationField
                  label="Último intento"
                  value={formatDate(
                    publication?.lastAttemptAtUtc,
                  )}
                />

                <PublicationField
                  label="Estado"
                  value={publicationStatusLabel(
                    publication?.status,
                  )}
                />
              </div>

              {publication?.errorMessage && (
                <div className="android-publication-error">
                  <XCircle size={16} />

                  <div>
                    <strong>
                      {publication.errorCode ??
                        'Error de publicación'}
                    </strong>

                    <span>
                      {
                        publication.errorMessage
                      }
                    </span>
                  </div>
                </div>
              )}

              <div className="android-publication-actions">
                <button
                  type="button"
                  className="android-publish-button"
                  disabled={
                    publishing ||
                    saving
                  }
                  onClick={() => {
                    void publishAndroid()
                  }}
                >
                  {publishing ? (
                    <Loader2
                      size={16}
                      className="policy-spin"
                    />
                  ) : (
                    <Cloud size={16} />
                  )}

                  {publishing
                    ? 'Publicando...'
                    : publication
                      ? 'Republicar versión'
                      : 'Publicar en Android Enterprise'}
                </button>

                <button
                  type="button"
                  className="android-verify-button"
                  disabled={
                    verifying ||
                    publishing ||
                    publication?.status !==
                      'Published'
                  }
                  onClick={() => {
                    void verifyAndroid()
                  }}
                >
                  {verifying ? (
                    <Loader2
                      size={16}
                      className="policy-spin"
                    />
                  ) : (
                    <RefreshCw size={16} />
                  )}

                  {verifying
                    ? 'Verificando...'
                    : 'Verificar en Google'}
                </button>

                <button
                  type="button"
                  className="android-refresh-button"
                  disabled={
                    loadingPublication ||
                    publishing
                  }
                  onClick={() => {
                    void loadAndroidPublication()
                  }}
                >
                  <RefreshCw size={15} />
                  Actualizar estado
                </button>
              </div>

              {verification && (
                <div className="android-verification-result">
                  <CheckCircle2 size={18} />

                  <div>
                    <strong>
                      Política verificada
                      directamente en Google
                    </strong>

                    <span>
                      {
                        verification.googlePolicyName
                      }
                    </span>

                    <span>
                      Google Policy ID:{' '}
                      {
                        verification.googlePolicyId
                      }
                    </span>
                  </div>

                  <ExternalLink size={16} />
                </div>
              )}
           </section>
                )}

                {editing &&
                  platform === 'Android' &&
                  policyId && (
                    <AndroidPolicyAssignmentPanel
                      policyId={policyId}
                      publication={publication}
                      policyDirty={false}
                    />
                  )}

          {/* ==================================================
              ANDROID SETTINGS
          ================================================== */}

          <AndroidPolicyEditor
            configuration={
              configuration.android
            }
            updateAndroid={
              updateAndroid
            }
          />
        </>
      )}
       </div>
  )
}

// ============================================================
// WINDOWS EDITOR
// ============================================================

interface WindowsPolicyEditorProps {
  configuration:
    WindowsPolicyConfiguration

  updateWindows:
    <
      K extends keyof WindowsPolicyConfiguration,
    >(
      section: K,
      values: Partial<
        WindowsPolicyConfiguration[K]
      >,
    ) => void
}

function WindowsPolicyEditor({
  configuration,
  updateWindows,
}: WindowsPolicyEditorProps) {
  return (
    <div className="policy-settings-grid">
      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <LockKeyhole size={18} />

            <span>
              <strong>
                Contraseña
              </strong>

              <small>
                Requisitos de contraseña
                local.
              </small>
            </span>
          </div>

          <Toggle
            checked={
              configuration.password
                .enabled
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  enabled: value,
                },
              )
            }
          />
        </div>

        <div className="policy-setting-body">
          <NumberField
            label="Longitud mínima"
            min={4}
            max={64}
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .minimumLength
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  minimumLength: value,
                },
              )
            }
          />

          <NumberField
            label="Vigencia máxima (días)"
            min={0}
            max={365}
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .maximumAgeDays
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  maximumAgeDays: value,
                },
              )
            }
          />

          <CheckOption
            label="Mayúsculas"
            checked={
              configuration.password
                .requireUppercase
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  requireUppercase:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Minúsculas"
            checked={
              configuration.password
                .requireLowercase
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  requireLowercase:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Números"
            checked={
              configuration.password
                .requireNumber
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  requireNumber:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Caracteres especiales"
            checked={
              configuration.password
                .requireSpecialCharacter
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateWindows(
                'password',
                {
                  requireSpecialCharacter:
                    value,
                },
              )
            }
          />
        </div>
      </section>

      <SimpleToggleCard
        icon={
          <Shield size={18} />
        }
        title="Microsoft Defender"
        description="Protección antimalware y supervisión en tiempo real."
        checked={
          configuration.defender.enabled
        }
        onChange={(value) =>
          updateWindows(
            'defender',
            {
              enabled: value,
            },
          )
        }
      >
        <CheckOption
          label="Protección en tiempo real"
          checked={
            configuration.defender
              .realTimeProtection
          }
          disabled={
            !configuration.defender
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'defender',
              {
                realTimeProtection:
                  value,
              },
            )
          }
        />

        <CheckOption
          label="Protección en la nube"
          checked={
            configuration.defender
              .cloudProtection
          }
          disabled={
            !configuration.defender
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'defender',
              {
                cloudProtection:
                  value,
              },
            )
          }
        />
      </SimpleToggleCard>

      <SimpleToggleCard
        icon={<Wifi size={18} />}
        title="Firewall"
        description="Control de los perfiles de Windows Firewall."
        checked={
          configuration.firewall.enabled
        }
        onChange={(value) =>
          updateWindows(
            'firewall',
            {
              enabled: value,
            },
          )
        }
      >
        <CheckOption
          label="Perfil de dominio"
          checked={
            configuration.firewall
              .domainProfile
          }
          disabled={
            !configuration.firewall
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'firewall',
              {
                domainProfile: value,
              },
            )
          }
        />

        <CheckOption
          label="Perfil privado"
          checked={
            configuration.firewall
              .privateProfile
          }
          disabled={
            !configuration.firewall
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'firewall',
              {
                privateProfile: value,
              },
            )
          }
        />

        <CheckOption
          label="Perfil público"
          checked={
            configuration.firewall
              .publicProfile
          }
          disabled={
            !configuration.firewall
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'firewall',
              {
                publicProfile: value,
              },
            )
          }
        />
      </SimpleToggleCard>

      <SimpleToggleCard
        icon={<Usb size={18} />}
        title="Almacenamiento USB"
        description="Restringe almacenamiento extraíble."
        checked={
          configuration.usb
            .blockRemovableStorage
        }
        onChange={(value) =>
          updateWindows(
            'usb',
            {
              blockRemovableStorage:
                value,
            },
          )
        }
      />

      <SimpleToggleCard
        icon={
          <LockKeyhole size={18} />
        }
        title="Bloqueo de pantalla"
        description="Bloqueo automático por inactividad."
        checked={
          configuration.screenLock
            .enabled
        }
        onChange={(value) =>
          updateWindows(
            'screenLock',
            {
              enabled: value,
            },
          )
        }
      >
        <NumberField
          label="Tiempo de espera (minutos)"
          min={1}
          max={120}
          disabled={
            !configuration.screenLock
              .enabled
          }
          value={
            configuration.screenLock
              .timeoutMinutes
          }
          onChange={(value) =>
            updateWindows(
              'screenLock',
              {
                timeoutMinutes:
                  value,
              },
            )
          }
        />
      </SimpleToggleCard>

      <SimpleToggleCard
        icon={
          <ChevronRight size={18} />
        }
        title="Windows Update"
        description="Administración de actualizaciones."
        checked={
          configuration.windowsUpdate
            .enabled
        }
        onChange={(value) =>
          updateWindows(
            'windowsUpdate',
            {
              enabled: value,
            },
          )
        }
      >
        <CheckOption
          label="Actualizaciones automáticas"
          checked={
            configuration.windowsUpdate
              .automaticUpdates
          }
          disabled={
            !configuration.windowsUpdate
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'windowsUpdate',
              {
                automaticUpdates:
                  value,
              },
            )
          }
        />

        <CheckOption
          label="Reinicio fuera de horas activas"
          checked={
            configuration.windowsUpdate
              .restartOutsideActiveHours
          }
          disabled={
            !configuration.windowsUpdate
              .enabled
          }
          onChange={(value) =>
            updateWindows(
              'windowsUpdate',
              {
                restartOutsideActiveHours:
                  value,
              },
            )
          }
        />
      </SimpleToggleCard>
    </div>
  )
}

// ============================================================
// ANDROID EDITOR
// ============================================================

interface AndroidPolicyEditorProps {
  configuration:
    AndroidPolicyConfiguration

  updateAndroid:
    <
      K extends keyof AndroidPolicyConfiguration,
    >(
      section: K,
      values: Partial<
        AndroidPolicyConfiguration[K]
      >,
    ) => void
}

function AndroidPolicyEditor({
  configuration,
  updateAndroid,
}: AndroidPolicyEditorProps) {
  return (
    <div className="policy-settings-grid">
      {/* PASSWORD */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <KeyRound size={18} />

            <span>
              <strong>
                Contraseña Android
              </strong>

              <small>
                Seguridad de acceso al
                dispositivo.
              </small>
            </span>
          </div>

          <Toggle
            checked={
              configuration.password
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  enabled: value,
                },
              )
            }
          />
        </div>

        <div className="policy-setting-body">
          <NumberField
            label="Longitud mínima"
            min={4}
            max={32}
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .minimumLength
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  minimumLength: value,
                },
              )
            }
          />

          <SelectField
            label="Complejidad"
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .complexity
            }
            options={[
              ['NONE', 'Sin requisito'],
              ['LOW', 'Baja'],
              ['MEDIUM', 'Media'],
              ['HIGH', 'Alta'],
            ]}
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  complexity:
                    value as AndroidPolicyConfiguration['password']['complexity'],
                },
              )
            }
          />

          <NumberField
            label="Intentos fallidos máximos"
            min={0}
            max={20}
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .maxFailedAttempts
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  maxFailedAttempts:
                    value,
                },
              )
            }
          />

          <NumberField
            label="Bloqueo de pantalla (min)"
            min={1}
            max={120}
            disabled={
              !configuration.password
                .enabled
            }
            value={
              configuration.password
                .screenLockTimeout
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  screenLockTimeout:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Requerir PIN numérico"
            checked={
              configuration.password
                .requireNumeric
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  requireNumeric: value,
                },
              )
            }
          />

          <CheckOption
            label="Contraseña compleja"
            checked={
              configuration.password
                .requireComplex
            }
            disabled={
              !configuration.password
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'password',
                {
                  requireComplex: value,
                },
              )
            }
          />
        </div>
      </section>

      {/* CORE RESTRICTIONS */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <Shield size={18} />

            <span>
              <strong>
                Restricciones principales
              </strong>

              <small>
                Hardware y funciones
                sensibles.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <CheckOption
            label="Bloquear cámara"
            checked={
              configuration.restrictions
                .blockCamera
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockCamera: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear capturas"
            checked={
              configuration.restrictions
                .blockScreenCapture
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockScreenCapture:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear transferencia USB"
            checked={
              configuration.restrictions
                .blockUsbFileTransfer
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockUsbFileTransfer:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear Bluetooth"
            checked={
              configuration.restrictions
                .blockBluetooth
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockBluetooth: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear fuentes desconocidas"
            checked={
              configuration.restrictions
                .blockUnknownSources
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockUnknownSources:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear depuración"
            checked={
              configuration.restrictions
                .blockDebugging
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockDebugging: value,
                },
              )
            }
          />
        </div>
      </section>

      {/* ADVANCED RESTRICTIONS */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <ShieldCheck size={18} />

            <span>
              <strong>
                Restricciones avanzadas
              </strong>

              <small>
                Administración y protección
                del sistema.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <CheckOption
            label="Bloquear restablecimiento"
            checked={
              configuration.restrictions
                .blockFactoryReset
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockFactoryReset:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear modo seguro"
            checked={
              configuration.restrictions
                .blockSafeBoot
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockSafeBoot: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear agregar usuario"
            checked={
              configuration.restrictions
                .blockAddUser
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockAddUser: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear eliminar usuario"
            checked={
              configuration.restrictions
                .blockRemoveUser
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockRemoveUser:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear modificación de cuentas"
            checked={
              configuration.restrictions
                .blockModifyAccounts
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockModifyAccounts:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear Android Beam"
            checked={
              configuration.restrictions
                .blockOutgoingBeam
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockOutgoingBeam:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear impresión"
            checked={
              configuration.restrictions
                .blockPrinting
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockPrinting: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear micrófono"
            checked={
              configuration.restrictions
                .blockMicrophone
            }
            onChange={(value) =>
              updateAndroid(
                'restrictions',
                {
                  blockMicrophone: value,
                },
              )
            }
          />
        </div>
      </section>

      {/* NETWORK */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <Network size={18} />

            <span>
              <strong>
                Red y conectividad
              </strong>

              <small>
                Configuración administrada
                de conexiones.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <CheckOption
            label="Bloquear configuración Wi-Fi"
            checked={
              configuration.network
                .wifiConfigDisabled
            }
            onChange={(value) =>
              updateAndroid(
                'network',
                {
                  wifiConfigDisabled:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear configuración Bluetooth"
            checked={
              configuration.network
                .bluetoothConfigDisabled
            }
            onChange={(value) =>
              updateAndroid(
                'network',
                {
                  bluetoothConfigDisabled:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear tethering"
            checked={
              configuration.network
                .tetheringDisabled
            }
            onChange={(value) =>
              updateAndroid(
                'network',
                {
                  tetheringDisabled:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear configuración VPN"
            checked={
              configuration.network
                .vpnConfigDisabled
            }
            onChange={(value) =>
              updateAndroid(
                'network',
                {
                  vpnConfigDisabled:
                    value,
                },
              )
            }
          />

          <SelectField
            label="Private DNS"
            value={
              configuration.network
                .privateDnsMode
            }
            options={[
              [
                'UNSPECIFIED',
                'Sin especificar',
              ],
              [
                'OPPORTUNISTIC',
                'Oportunista',
              ],
              ['OFF', 'Desactivado'],
            ]}
            onChange={(value) =>
              updateAndroid(
                'network',
                {
                  privateDnsMode:
                    value as AndroidPolicyConfiguration['network']['privateDnsMode'],
                },
              )
            }
          />

          <SelectField
            label="Ubicación"
            value={
              configuration.location.mode
            }
            options={[
              [
                'UNSPECIFIED',
                'Sin especificar',
              ],
              [
                'ENFORCED',
                'Forzada',
              ],
              [
                'USER_CHOICE',
                'Elección del usuario',
              ],
              [
                'DISABLED',
                'Desactivada',
              ],
            ]}
            onChange={(value) =>
              updateAndroid(
                'location',
                {
                  mode:
                    value as AndroidPolicyConfiguration['location']['mode'],
                },
              )
            }
          />
        </div>
      </section>

      {/* APPLICATIONS */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <Smartphone size={18} />

            <span>
              <strong>
                Aplicaciones
              </strong>

              <small>
                Instalación y catálogo
                administrado.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <CheckOption
            label="Permitir instalación"
            checked={
              configuration.applications
                .allowAppInstallation
            }
            onChange={(value) =>
              updateAndroid(
                'applications',
                {
                  allowAppInstallation:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Permitir desinstalación"
            checked={
              configuration.applications
                .allowAppUninstallation
            }
            onChange={(value) =>
              updateAndroid(
                'applications',
                {
                  allowAppUninstallation:
                    value,
                },
              )
            }
          />

          <SelectField
            label="Modo de instalación"
            value={
              configuration.applications
                .installMode
            }
            options={[
              [
                'AVAILABLE',
                'Disponible',
              ],
              [
                'FORCE_INSTALLED',
                'Instalación forzada',
              ],
              [
                'BLOCKED',
                'Bloqueada',
              ],
            ]}
            onChange={(value) =>
              updateAndroid(
                'applications',
                {
                  installMode:
                    value as AndroidPolicyConfiguration['applications']['installMode'],
                },
              )
            }
          />

          <SelectField
            label="Modo Google Play"
            value={
              configuration.applications
                .playStoreMode
            }
            options={[
              [
                'UNSPECIFIED',
                'Sin especificar',
              ],
              [
                'WHITELIST',
                'Lista permitida',
              ],
              [
                'BLACKLIST',
                'Lista bloqueada',
              ],
            ]}
            onChange={(value) =>
              updateAndroid(
                'applications',
                {
                  playStoreMode:
                    value as AndroidPolicyConfiguration['applications']['playStoreMode'],
                },
              )
            }
          />

          <TextField
            label="Métodos de entrada permitidos"
            value={
              configuration.applications
                .permittedInputMethods
            }
            placeholder="Ej. com.google.android.inputmethod.latin"
            onChange={(value) =>
              updateAndroid(
                'applications',
                {
                  permittedInputMethods:
                    value,
                },
              )
            }
          />
        </div>
      </section>

      {/* SYSTEM UPDATE */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <RefreshCw size={18} />

            <span>
              <strong>
                Actualizaciones Android
              </strong>

              <small>
                Política de actualización
                del sistema operativo.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <SelectField
            label="Tipo"
            value={
              configuration.systemUpdate
                .type
            }
            options={[
              [
                'AUTOMATIC',
                'Automática',
              ],
              [
                'WINDOWED',
                'Ventana programada',
              ],
              [
                'POSTPONE',
                'Posponer',
              ],
            ]}
            onChange={(value) =>
              updateAndroid(
                'systemUpdate',
                {
                  type:
                    value as AndroidPolicyConfiguration['systemUpdate']['type'],
                },
              )
            }
          />

          <NumberField
            label="Inicio ventana (minutos)"
            min={0}
            max={1439}
            disabled={
              configuration.systemUpdate
                .type !== 'WINDOWED'
            }
            value={
              configuration.systemUpdate
                .startMinutes
            }
            onChange={(value) =>
              updateAndroid(
                'systemUpdate',
                {
                  startMinutes: value,
                },
              )
            }
          />

          <NumberField
            label="Fin ventana (minutos)"
            min={0}
            max={1439}
            disabled={
              configuration.systemUpdate
                .type !== 'WINDOWED'
            }
            value={
              configuration.systemUpdate
                .endMinutes
            }
            onChange={(value) =>
              updateAndroid(
                'systemUpdate',
                {
                  endMinutes: value,
                },
              )
            }
          />
        </div>
      </section>

      {/* KIOSK */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <Smartphone size={18} />

            <span>
              <strong>
                Kiosk / Dedicated
              </strong>

              <small>
                Configuración para
                dispositivos dedicados.
              </small>
            </span>
          </div>

          <Toggle
            checked={
              configuration.kiosk.enabled
            }
            onChange={(value) =>
              updateAndroid(
                'kiosk',
                {
                  enabled: value,
                },
              )
            }
          />
        </div>

        <div className="policy-setting-body">
          <TextField
            label="Application ID"
            value={
              configuration.kiosk
                .applicationId
            }
            disabled={
              !configuration.kiosk
                .enabled
            }
            placeholder="com.empresa.aplicacion"
            onChange={(value) =>
              updateAndroid(
                'kiosk',
                {
                  applicationId: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear barra de estado"
            checked={
              configuration.kiosk
                .statusBar
            }
            disabled={
              !configuration.kiosk
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'kiosk',
                {
                  statusBar: value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear navegación del sistema"
            checked={
              configuration.kiosk
                .systemNavigation
            }
            disabled={
              !configuration.kiosk
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'kiosk',
                {
                  systemNavigation:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Bloquear Keyguard"
            checked={
              configuration.kiosk
                .keyguard
            }
            disabled={
              !configuration.kiosk
                .enabled
            }
            onChange={(value) =>
              updateAndroid(
                'kiosk',
                {
                  keyguard: value,
                },
              )
            }
          />
        </div>
      </section>

      {/* COMPLIANCE */}

      <section className="policy-setting-card">
        <div className="policy-setting-heading">
          <div>
            <ShieldCheck size={18} />

            <span>
              <strong>
                Cumplimiento
              </strong>

              <small>
                Requisitos mínimos de
                seguridad Android.
              </small>
            </span>
          </div>
        </div>

        <div className="policy-setting-body">
          <NumberField
            label="API Level mínimo"
            min={0}
            max={100}
            value={
              configuration.compliance
                .minimumApiLevel
            }
            onChange={(value) =>
              updateAndroid(
                'compliance',
                {
                  minimumApiLevel:
                    value,
                },
              )
            }
          />

          <TextField
            label="Parche mínimo"
            value={
              configuration.compliance
                .minimumSecurityPatch
            }
            placeholder="YYYY-MM-DD"
            onChange={(value) =>
              updateAndroid(
                'compliance',
                {
                  minimumSecurityPatch:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Requerir cifrado"
            checked={
              configuration.compliance
                .requireEncryption
            }
            onChange={(value) =>
              updateAndroid(
                'compliance',
                {
                  requireEncryption:
                    value,
                },
              )
            }
          />

          <CheckOption
            label="Requerir integridad del dispositivo"
            checked={
              configuration.compliance
                .requireDeviceIntegrity
            }
            onChange={(value) =>
              updateAndroid(
                'compliance',
                {
                  requireDeviceIntegrity:
                    value,
                },
              )
            }
          />
        </div>
      </section>
    </div>
  )
}

// ============================================================
// REUSABLE COMPONENTS
// ============================================================

interface CheckOptionProps {
  label: string
  checked: boolean
  disabled?: boolean
  onChange: (value: boolean) => void
}

function CheckOption({
  label,
  checked,
  disabled = false,
  onChange,
}: CheckOptionProps) {
  return (
    <label className="policy-check-option">
      <input
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={(event) =>
          onChange(
            event.target.checked,
          )
        }
      />

      <span>{label}</span>
    </label>
  )
}

interface SimpleToggleCardProps {
  icon: ReactNode
  title: string
  description: string
  checked: boolean
  onChange: (value: boolean) => void
  children?: ReactNode
}

function SimpleToggleCard({
  icon,
  title,
  description,
  checked,
  onChange,
  children,
}: SimpleToggleCardProps) {
  return (
    <section className="policy-setting-card">
      <div className="policy-setting-heading">
        <div>
          {icon}

          <span>
            <strong>
              {title}
            </strong>

            <small>
              {description}
            </small>
          </span>
        </div>

        <Toggle
          checked={checked}
          onChange={onChange}
        />
      </div>

      {children && (
        <div className="policy-setting-body">
          {children}
        </div>
      )}
    </section>
  )
}

interface NumberFieldProps {
  label: string
  value: number
  min?: number
  max?: number
  disabled?: boolean
  onChange: (value: number) => void
}

function NumberField({
  label,
  value,
  min,
  max,
  disabled = false,
  onChange,
}: NumberFieldProps) {
  return (
    <label>
      {label}

      <input
        type="number"
        value={value}
        min={min}
        max={max}
        disabled={disabled}
        onChange={(event) =>
          onChange(
            Number(
              event.target.value,
            ),
          )
        }
      />
    </label>
  )
}

interface TextFieldProps {
  label: string
  value: string
  placeholder?: string
  disabled?: boolean
  onChange: (value: string) => void
}

function TextField({
  label,
  value,
  placeholder,
  disabled = false,
  onChange,
}: TextFieldProps) {
  return (
    <label>
      {label}

      <input
        type="text"
        value={value}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(event) =>
          onChange(
            event.target.value,
          )
        }
      />
    </label>
  )
}

interface SelectFieldProps {
  label: string
  value: string
  disabled?: boolean
  options: Array<
    [string, string]
  >
  onChange: (value: string) => void
}

function SelectField({
  label,
  value,
  disabled = false,
  options,
  onChange,
}: SelectFieldProps) {
  return (
    <label>
      {label}

      <select
        value={value}
        disabled={disabled}
        onChange={(event) =>
          onChange(
            event.target.value,
          )
        }
      >
        {options.map(
          ([optionValue, label]) => (
            <option
              key={optionValue}
              value={optionValue}
            >
              {label}
            </option>
          ),
        )}
      </select>
    </label>
  )
}

interface PublicationFieldProps {
  label: string
  value: string
  mono?: boolean
}

function PublicationField({
  label,
  value,
  mono = false,
}: PublicationFieldProps) {
  return (
    <div className="android-publication-field">
      <span>{label}</span>

      <strong
        className={
          mono
            ? 'android-publication-mono'
            : undefined
        }
      >
        {value}
      </strong>
    </div>
  )
}
