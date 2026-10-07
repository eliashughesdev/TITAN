import {
  AppWindow,
  Boxes,
  MonitorSmartphone,
  PackagePlus,
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
  applicationsApi,
  type ApplicationSummary,
  type SoftwareDeployment,
  type SoftwarePackage,
  type UploadSoftwarePackageRequest,
} from '../../api/applicationsApi'

import {
  deviceGroupsApi,
  type DeviceGroup,
} from '../../api/deviceGroupsApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  ApplicationsInventoryTab,
} from './components/ApplicationsInventoryTab'

import {
  SoftwareCatalogTab,
} from './components/SoftwareCatalogTab'

import {
  SoftwareDeploymentsTab,
} from './components/SoftwareDeploymentsTab'

import {
  SoftwarePackageModal,
} from './components/SoftwarePackageModal'

import './AppsPage.css'

type MainTab =
  | 'inventory'
  | 'catalog'
  | 'deployments'

type FilterType =
  | 'all'
  | 'user'
  | 'system'

export function AppsPage() {
  const [
    activeTab,
    setActiveTab,
  ] =
    useState<MainTab>(
      'inventory',
    )

  const [
    applications,
    setApplications,
  ] =
    useState<
      ApplicationSummary[]
    >([])

  const [
    packages,
    setPackages,
  ] =
    useState<
      SoftwarePackage[]
    >([])

  const [
    deployments,
    setDeployments,
  ] =
    useState<
      SoftwareDeployment[]
    >([])

  const [
    devices,
    setDevices,
  ] =
    useState<
      DeviceListItem[]
    >([])

  const [
    groups,
    setGroups,
  ] =
    useState<
      DeviceGroup[]
    >([])

  const [
    search,
    setSearch,
  ] =
    useState('')

  const [
    filter,
    setFilter,
  ] =
    useState<FilterType>(
      'all',
    )

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    busy,
    setBusy,
  ] =
    useState(false)

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
    showUpload,
    setShowUpload,
  ] =
    useState(false)

  const [
    deployPackage,
    setDeployPackage,
  ] =
    useState<
      SoftwarePackage | null
    >(null)

  const [
    targetType,
    setTargetType,
  ] =
    useState<
      'Device' |
      'Group'
    >('Device')

  const [
    targetId,
    setTargetId,
  ] =
    useState('')

  const loadInventory =
    useCallback(
      async () => {
        const systemApp =
          filter ===
          'system'
            ? true
            : filter ===
                'user'
              ? false
              : undefined

        const result =
          await applicationsApi
            .getAll({
              search:
                search.trim()
                ||
                undefined,

              systemApp,
            })

        setApplications(
          result,
        )
      },
      [
        filter,
        search,
      ],
    )

  const loadManagement =
    useCallback(
      async () => {
        const [
          packageResult,
          deploymentResult,
          deviceResult,
          groupResult,
        ] =
          await Promise.all([
            applicationsApi
              .getPackages(),

            applicationsApi
              .getDeployments(),

            devicesApi
              .getDevices({
                platform:
                  'Windows',

                page:
                  1,

                pageSize:
                  100,
              }),

            deviceGroupsApi
              .getAll(),
          ])

        setPackages(
          packageResult,
        )

        setDeployments(
          deploymentResult,
        )

        setDevices(
          deviceResult.items,
        )

        setGroups(
          groupResult,
        )
      },
      [],
    )

  const loadAll =
    useCallback(
      async () => {
        try {
          setLoading(true)
          setError(null)

          await Promise.all([
            loadInventory(),
            loadManagement(),
          ])
        } catch {
          setError(
            'No fue posible cargar la gestión de aplicaciones.',
          )
        } finally {
          setLoading(false)
        }
      },
      [
        loadInventory,
        loadManagement,
      ],
    )

  useEffect(
    () => {
      const timer =
        window.setTimeout(
          () => {
            void loadAll()
          },
          250,
        )

      return () =>
        window.clearTimeout(
          timer,
        )
    },
    [
      loadAll,
    ],
  )

  const statistics =
    useMemo(
      () => {
        const installations =
          applications.reduce(
            (
              total,
              item,
            ) =>
              total
              +
              item.deviceCount,
            0,
          )

        return {
          inventory:
            applications.length,

          packages:
            packages.length,

          deployments:
            deployments.length,

          installations,
        }
      },
      [
        applications,
        packages,
        deployments,
      ],
    )

  async function uploadPackage(
    request:
      UploadSoftwarePackageRequest,
  ) {
    try {
      setBusy(true)
      setError(null)
      setMessage(null)

      await applicationsApi
        .uploadPackage(
          request,
        )

      setShowUpload(false)

      setMessage(
        'Paquete agregado correctamente al catálogo.',
      )

      await loadManagement()

      setActiveTab(
        'catalog',
      )
    } catch {
      setError(
        'No fue posible cargar el paquete.',
      )
    } finally {
      setBusy(false)
    }
  }

  async function runDeployment() {
    if (
      !deployPackage
      ||
      !targetId
    ) {
      window.alert(
        'Selecciona un destino.',
      )

      return
    }

    const confirmed =
      window.confirm(
        `¿Desplegar ${deployPackage.name} ${deployPackage.version} al destino seleccionado?`,
      )

    if (!confirmed) {
      return
    }

    try {
      setBusy(true)
      setError(null)
      setMessage(null)

      const result =
        await applicationsApi
          .deployPackage(
            deployPackage.id,
            {
              targetType,
              targetId,
            },
          )

      setMessage(
        `Deployment creado. ${result.queuedDevices} dispositivo(s) en cola.`,
      )

      setDeployPackage(null)
      setTargetId('')

      await loadManagement()

      setActiveTab(
        'deployments',
      )
    } catch {
      setError(
        'No fue posible crear el deployment.',
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="apps-page">
      <header className="apps-header">
        <div>
          <span className="apps-eyebrow">
            TITANMDM SOFTWARE MANAGEMENT
          </span>

          <h1>
            Aplicaciones
          </h1>

          <p>
            Inventario, catálogo y
            despliegue empresarial de
            software.
          </p>
        </div>

        <button
          type="button"
          className="apps-refresh-button"
          disabled={
            loading
            ||
            busy
          }
          onClick={() =>
            void loadAll()
          }
        >
          <RefreshCw
            size={16}
          />

          {loading
            ? 'Actualizando...'
            : 'Actualizar'}
        </button>
      </header>

      {message && (
        <div className="apps-message">
          {message}
        </div>
      )}

      {error && (
        <div className="apps-error">
          {error}
        </div>
      )}

      <section className="apps-stat-grid">
        <StatCard
          icon={
            <AppWindow
              size={20}
            />
          }
          title="Inventario"
          value={
            statistics.inventory
          }
          description="Apps detectadas"
        />

        <StatCard
          icon={
            <Boxes
              size={20}
            />
          }
          title="Catálogo"
          value={
            statistics.packages
          }
          description="Paquetes administrados"
        />

        <StatCard
          icon={
            <PackagePlus
              size={20}
            />
          }
          title="Deployments"
          value={
            statistics.deployments
          }
          description="Despliegues registrados"
        />

        <StatCard
          icon={
            <MonitorSmartphone
              size={20}
            />
          }
          title="Instalaciones"
          value={
            statistics.installations
          }
          description="Presencia reportada"
        />
      </section>

      <section className="apps-panel">
        <div className="apps-tabs">
          <button
            type="button"
            className={
              activeTab ===
              'inventory'
                ? 'active'
                : ''
            }
            onClick={() =>
              setActiveTab(
                'inventory',
              )
            }
          >
            <AppWindow
              size={16}
            />

            Inventario
          </button>

          <button
            type="button"
            className={
              activeTab ===
              'catalog'
                ? 'active'
                : ''
            }
            onClick={() =>
              setActiveTab(
                'catalog',
              )
            }
          >
            <Boxes
              size={16}
            />

            Catálogo
          </button>

          <button
            type="button"
            className={
              activeTab ===
              'deployments'
                ? 'active'
                : ''
            }
            onClick={() =>
              setActiveTab(
                'deployments',
              )
            }
          >
            <ShieldCheck
              size={16}
            />

            Deployments
          </button>
        </div>

        {activeTab ===
          'inventory' && (
          <ApplicationsInventoryTab
            applications={
              applications
            }
            search={search}
            filter={filter}
            loading={loading}
            onSearchChange={
              setSearch
            }
            onFilterChange={
              setFilter
            }
          />
        )}

        {activeTab ===
          'catalog' && (
          <SoftwareCatalogTab
            packages={packages}
            busy={busy}
            onUpload={() =>
              setShowUpload(
                true,
              )
            }
            onDeploy={
              packageItem => {
                setDeployPackage(
                  packageItem,
                )

                setTargetType(
                  'Device',
                )

                setTargetId('')
              }
            }
          />
        )}

        {activeTab ===
          'deployments' && (
          <SoftwareDeploymentsTab
            deployments={
              deployments
            }
          />
        )}
      </section>

      {showUpload && (
        <SoftwarePackageModal
          busy={busy}
          onClose={() =>
            setShowUpload(
              false,
            )
          }
          onUpload={
            uploadPackage
          }
        />
      )}

      {deployPackage && (
        <div className="apps-modal-backdrop">
          <div className="apps-modal">
            <header>
              <div>
                <span>
                  SOFTWARE DEPLOYMENT
                </span>

                <h3>
                  Desplegar{' '}
                  {
                    deployPackage
                      .name
                  }
                </h3>
              </div>
            </header>

            <div className="apps-modal-form">
              <label>
                <span>
                  Tipo de destino
                </span>

                <select
                  value={targetType}
                  onChange={
                    event => {
                      setTargetType(
                        event.target
                          .value as
                          | 'Device'
                          | 'Group',
                      )

                      setTargetId('')
                    }
                  }
                >
                  <option value="Device">
                    Dispositivo
                  </option>

                  <option value="Group">
                    Grupo
                  </option>
                </select>
              </label>

              <label>
                <span>
                  Destino
                </span>

                <select
                  value={targetId}
                  onChange={
                    event =>
                      setTargetId(
                        event.target.value,
                      )
                  }
                >
                  <option value="">
                    Seleccionar...
                  </option>

                  {targetType ===
                  'Device'
                    ? devices.map(
                        device => (
                          <option
                            key={
                              device.id
                            }
                            value={
                              device.id
                            }
                          >
                            {
                              device
                                .deviceName
                            }
                            {' · '}
                            {
                              device
                                .status
                            }
                          </option>
                        ),
                      )
                    : groups.map(
                        group => (
                          <option
                            key={
                              group.id
                            }
                            value={
                              group.id
                            }
                          >
                            {
                              group
                                .name
                            }
                            {' · '}
                            {
                              group
                                .deviceCount
                            }{' '}
                            dispositivos
                          </option>
                        ),
                      )}
                </select>
              </label>

              <div className="apps-deploy-summary">
                <span>
                  Paquete
                </span>

                <strong>
                  {
                    deployPackage
                      .originalFileName
                  }
                </strong>

                <span>
                  Versión
                </span>

                <strong>
                  {
                    deployPackage
                      .version
                  }
                </strong>
              </div>
            </div>

            <footer>
              <button
                type="button"
                disabled={busy}
                onClick={() =>
                  setDeployPackage(
                    null,
                  )
                }
              >
                Cancelar
              </button>

              <button
                type="button"
                className="primary"
                disabled={
                  busy
                  ||
                  !targetId
                }
                onClick={() =>
                  void runDeployment()
                }
              >
                {busy
                  ? 'Procesando...'
                  : 'Desplegar'}
              </button>
            </footer>
          </div>
        </div>
      )}
    </div>
  )
}

interface StatCardProps {
  icon:
    React.ReactNode

  title: string

  value: number

  description: string
}

function StatCard({
  icon,
  title,
  value,
  description,
}: StatCardProps) {
  return (
    <article className="apps-stat-card">
      <div className="apps-stat-icon">
        {icon}
      </div>

      <div>
        <span>
          {title}
        </span>

        <strong>
          {value}
        </strong>

        <small>
          {description}
        </small>
      </div>
    </article>
  )
}