import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  useNavigate,
  useSearchParams,
} from 'react-router-dom'

import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Database,
  Monitor,
  RefreshCw,
  Server,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  TerminalSquare,
  Wifi,
  WifiOff,
} from 'lucide-react'

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import {
  dashboardApi,
  type DashboardSummary,
  type DashboardWorkspace,
} from '../api/dashboardApi'

import {
  WorkspaceInsights,
} from '../components/dashboard/WorkspaceInsights'

import {
  useWorkspace,
} from '../workspace/WorkspaceContext'

import './DashboardPage.css'

/*
 * ================================================================
 * INITIAL STATE
 * ================================================================
 */

const initialSummary:
  DashboardSummary = {
    devices: {
      total: 0,
      online: 0,
      offline: 0,
      pending: 0,
      enrolling: 0,
      quarantined: 0,
      retired: 0,
      managed: 0,
    },

    platforms: {
      windows: 0,
      android: 0,
      unknown: 0,
    },

    compliance: {
      compliant: 0,
      nonCompliant: 0,
      evaluating: 0,
      quarantined: 0,
      unknown: 0,
      compliancePercentage: null,
    },

    commands: {
      total: 0,
      pending: 0,
      queued: 0,
      dispatching: 0,
      sent: 0,
      delivered: 0,
      executing: 0,
      success: 0,
      failed: 0,
      timeout: 0,
      cancelled: 0,
      active: 0,
      problems: 0,
    },

    system: {
      api: 'Unknown',
      database: 'Unknown',
    },

    windows: null,

    android: null,

    generatedAtUtc: '',
  }

/*
 * ================================================================
 * TOOLTIP
 * ================================================================
 */

interface ChartTooltipProps {
  active?: boolean

  payload?: Array<{
    name?: string
    value?: number
  }>

  label?: string
}

function ChartTooltip({
  active,
  payload,
  label,
}: ChartTooltipProps) {
  if (
    !active
    ||
    !payload
    ||
    payload.length === 0
  ) {
    return null
  }

  return (
    <div className="dashboard-chart-tooltip">
      {label && (
        <strong>
          {label}
        </strong>
      )}

      {payload.map(
        (
          entry,
          index,
        ) => (
          <div
            key={
              `${entry.name ?? 'item'}-${index}`
            }
          >
            <span>
              {entry.name ?? 'Valor'}
            </span>

            <strong>
              {entry.value ?? 0}
            </strong>
          </div>
        ),
      )}
    </div>
  )
}

/*
 * ================================================================
 * DASHBOARD
 * ================================================================
 */

export function DashboardPage() {
  const navigate =
    useNavigate()

  const [
    searchParams,
  ] =
    useSearchParams()

  const {
    activeWorkspaceId,
    activeModule,
  } =
    useWorkspace()

  /*
   * ==============================================================
   * WORKSPACE
   * ==============================================================
   */

  const dashboardWorkspace =
    useMemo<
      DashboardWorkspace
    >(
      () => {
        const requested =
          searchParams
            .get(
              'workspace',
            )
            ?.trim()
            .toLowerCase()

        if (
          requested ===
          'windows'
        ) {
          return 'windows'
        }

        if (
          requested ===
          'android'
        ) {
          return 'android'
        }

        if (
          requested ===
          'global'
        ) {
          return 'global'
        }

        if (
          activeWorkspaceId ===
          'windows'
        ) {
          return 'windows'
        }

        if (
          activeWorkspaceId ===
          'android'
        ) {
          return 'android'
        }

        return 'global'
      },
      [
        searchParams,
        activeWorkspaceId,
      ],
    )

  /*
   * ==============================================================
   * STATE
   * ==============================================================
   */

  const [
    summary,
    setSummary,
  ] =
    useState<DashboardSummary>(
      initialSummary,
    )

  const [
    isLoading,
    setIsLoading,
  ] =
    useState(true)

  const [
    error,
    setError,
  ] =
    useState<string | null>(
      null,
    )

  /*
   * ==============================================================
   * ROUTES
   * ==============================================================
   */

  const deviceQuery =
    useMemo(
      () => {
        if (
          dashboardWorkspace ===
          'windows'
        ) {
          return (
            'platform=Windows'
            +
            '&workspace=windows'
          )
        }

        if (
          dashboardWorkspace ===
          'android'
        ) {
          return (
            'platform=Android'
            +
            '&workspace=android'
          )
        }

        return 'workspace=global'
      },
      [
        dashboardWorkspace,
      ],
    )

  const devicesRoute =
    `/devices?${deviceQuery}`

  const onlineDevicesRoute =
    `/devices?${deviceQuery}&status=online`

  const offlineDevicesRoute =
    `/devices?${deviceQuery}&status=offline`

  const complianceRoute =
    `/compliance?workspace=${dashboardWorkspace}`

  const automationRoute =
    `/automation?workspace=${dashboardWorkspace}`

  /*
   * ==============================================================
   * DATA
   * ==============================================================
   */

  const loadDashboard =
    useCallback(
      async () => {
        try {
          setIsLoading(
            true,
          )

          setError(
            null,
          )

          const data =
            await dashboardApi
              .getSummary(
                dashboardWorkspace,
              )

          setSummary(
            data,
          )
        } catch (
          loadError
        ) {
          console.error(
            'Dashboard loading error:',
            loadError,
          )

          setError(
            'No fue posible obtener los datos del dashboard.',
          )
        } finally {
          setIsLoading(
            false,
          )
        }
      },
      [
        dashboardWorkspace,
      ],
    )

  /*
   * ==============================================================
   * INITIAL LOAD
   * ==============================================================
   */

  useEffect(
    () => {
      document.title =
        dashboardWorkspace ===
          'windows'
          ? 'Dashboard Windows | TitanMDM'
          : dashboardWorkspace ===
              'android'
            ? 'Dashboard Android | TitanMDM'
            : 'Dashboard General | TitanMDM'

      void loadDashboard()
    },
    [
      dashboardWorkspace,
      loadDashboard,
    ],
  )

  /*
   * ==============================================================
   * PRESENTATION
   * ==============================================================
   */

  const workspaceName =
    dashboardWorkspace ===
      'windows'
      ? 'Windows'
      : dashboardWorkspace ===
          'android'
        ? 'Android'
        : 'General'

  const workspaceDescription =
    dashboardWorkspace ===
      'windows'
      ? 'Estado operativo de la infraestructura Windows administrada por TitanMDM.'
      : dashboardWorkspace ===
          'android'
        ? 'Estado operativo de Android Enterprise y la flota móvil administrada.'
        : 'Visión consolidada de Windows, Android, cumplimiento, automatización y servicios TitanMDM.'

  /*
   * ==============================================================
   * CALCULATED VALUES
   * ==============================================================
   */

  const compliancePercentage =
    summary
      .compliance
      .compliancePercentage
    ??
    0

  const onlinePercentage =
    summary.devices.total >
    0
      ? Math.round(
          (
            summary.devices.online
            /
            summary.devices.total
          )
          *
          100,
        )
      : 0

  const managedPercentage =
    summary.devices.total >
    0
      ? Math.round(
          (
            summary.devices.managed
            /
            summary.devices.total
          )
          *
          100,
        )
      : 0

  const commandSuccessPercentage =
    summary.commands.total >
    0
      ? Math.round(
          (
            summary.commands.success
            /
            summary.commands.total
          )
          *
          100,
        )
      : 0

  const generatedAt =
    summary.generatedAtUtc
      ? new Date(
          summary.generatedAtUtc,
        ).toLocaleString()
      : '—'

  /*
   * ==============================================================
   * COLOR
   * ==============================================================
   */

  const workspaceChartColor =
    dashboardWorkspace ===
      'windows'
      ? '#2563eb'
      : dashboardWorkspace ===
          'android'
        ? '#16a34a'
        : (
            activeModule
              ?.theme
              .primary
            ??
            '#4169e1'
          )

  /*
   * ==============================================================
   * STATUS DATA
   * ==============================================================
   */

  const deviceStatusData =
    useMemo(
      () => [
        {
          name:
            'Online',

          value:
            summary.devices.online,

          color:
            '#22c55e',
        },

        {
          name:
            'Offline',

          value:
            summary.devices.offline,

          color:
            '#ef4444',
        },

        {
          name:
            'Pendientes',

          value:
            summary.devices.pending,

          color:
            '#f59e0b',
        },

        {
          name:
            'Inscribiendo',

          value:
            summary.devices.enrolling,

          color:
            '#3b82f6',
        },

        {
          name:
            'Cuarentena',

          value:
            summary.devices.quarantined,

          color:
            '#a855f7',
        },
      ],
      [
        summary.devices,
      ],
    )

  /*
   * ==============================================================
   * PLATFORM DATA
   * ==============================================================
   */

  const platformData =
    useMemo(
      () => [
        {
          name:
            'Windows',

          value:
            summary.platforms.windows,

          color:
            '#2563eb',
        },

        {
          name:
            'Android',

          value:
            summary.platforms.android,

          color:
            '#16a34a',
        },

        {
          name:
            'Otros',

          value:
            summary.platforms.unknown,

          color:
            '#94a3b8',
        },
      ],
      [
        summary.platforms,
      ],
    )

  /*
   * ==============================================================
   * COMPLIANCE DATA
   * ==============================================================
   */

  const complianceData =
    useMemo(
      () => [
        {
          name:
            'Conformes',

          value:
            summary.compliance.compliant,
        },

        {
          name:
            'No conformes',

          value:
            summary.compliance.nonCompliant,
        },

        {
          name:
            'Evaluando',

          value:
            summary.compliance.evaluating,
        },

        {
          name:
            'Desconocidos',

          value:
            summary.compliance.unknown,
        },
      ],
      [
        summary.compliance,
      ],
    )

  /*
   * ==============================================================
   * COMMAND DATA
   * ==============================================================
   */

  const commandData =
    useMemo(
      () => [
        {
          name:
            'Correctos',

          value:
            summary.commands.success,
        },

        {
          name:
            'Fallidos',

          value:
            summary.commands.failed,
        },

        {
          name:
            'Ejecutando',

          value:
            summary.commands.executing,
        },

        {
          name:
            'Pendientes',

          value:
            summary.commands.pending
            +
            summary.commands.queued,
        },

        {
          name:
            'Timeout',

          value:
            summary.commands.timeout,
        },
      ],
      [
        summary.commands,
      ],
    )

  /*
   * ==============================================================
   * ATTENTION
   * ==============================================================
   */

  const attentionCount =
    summary.devices.offline
    +
    summary.devices.quarantined
    +
    summary.compliance.nonCompliant
    +
    summary.commands.problems

  const platformTotal =
    summary.platforms.windows
    +
    summary.platforms.android
    +
    summary.platforms.unknown

  /*
   * ==============================================================
   * RENDER
   * ==============================================================
   */

  return (
    <div className="dashboard-page dashboard-page--graphical">
      {/* ======================================================
          HERO
         ====================================================== */}

      <section className="dashboard-heading dashboard-heading--hero">
        <div>
          <span className="dashboard-heading__eyebrow">
            TITAN OPERATIONS ·{' '}
            {workspaceName.toUpperCase()}
          </span>

          <h1>
            Dashboard operativo
          </h1>

          <p>
            {workspaceDescription}
          </p>
        </div>

        <div className="dashboard-heading__actions">
          <div className="dashboard-live-status">
            <span />

            Datos en vivo
          </div>

          <button
            type="button"
            className="dashboard-refresh-button"
            disabled={
              isLoading
            }
            onClick={() =>
              void loadDashboard()
            }
          >
            <RefreshCw
              size={16}
              className={
                isLoading
                  ? 'is-spinning'
                  : ''
              }
            />

            {isLoading
              ? 'Actualizando...'
              : 'Actualizar'}
          </button>
        </div>
      </section>

      {/* ======================================================
          ERROR
         ====================================================== */}

      {error && (
        <div className="dashboard-error">
          <ShieldAlert
            size={20}
          />

          <div>
            <strong>
              Error al cargar dashboard
            </strong>

            <span>
              {error}
            </span>
          </div>

          <button
            type="button"
            onClick={() =>
              void loadDashboard()
            }
          >
            Reintentar
          </button>
        </div>
      )}

      {/* ======================================================
          KPI
         ====================================================== */}

      <section className="dashboard-kpi-grid">
        <button
          type="button"
          className="dashboard-kpi-card"
          onClick={() =>
            navigate(
              devicesRoute,
            )
          }
        >
          <div className="dashboard-kpi-card__icon">
            <Monitor
              size={20}
            />
          </div>

          <div className="dashboard-kpi-card__content">
            <span>
              Dispositivos
            </span>

            <strong>
              {isLoading
                ? '...'
                : summary.devices.total}
            </strong>

            <small>
              {managedPercentage}% administrados
            </small>
          </div>

          <div className="dashboard-kpi-card__trend">
            {summary.devices.managed}
          </div>
        </button>

        <button
          type="button"
          className="dashboard-kpi-card"
          onClick={() =>
            navigate(
              onlineDevicesRoute,
            )
          }
        >
          <div className="dashboard-kpi-card__icon dashboard-kpi-card__icon--success">
            <Wifi
              size={20}
            />
          </div>

          <div className="dashboard-kpi-card__content">
            <span>
              En línea
            </span>

            <strong>
              {isLoading
                ? '...'
                : summary.devices.online}
            </strong>

            <small>
              {onlinePercentage}% conectados
            </small>
          </div>

          <div className="dashboard-kpi-card__trend dashboard-kpi-card__trend--success">
            {onlinePercentage}%
          </div>
        </button>

        <button
          type="button"
          className="dashboard-kpi-card"
          onClick={() =>
            navigate(
              offlineDevicesRoute,
            )
          }
        >
          <div className="dashboard-kpi-card__icon dashboard-kpi-card__icon--danger">
            <WifiOff
              size={20}
            />
          </div>

          <div className="dashboard-kpi-card__content">
            <span>
              Offline
            </span>

            <strong>
              {isLoading
                ? '...'
                : summary.devices.offline}
            </strong>

            <small>
              Requieren revisión
            </small>
          </div>

          <div className="dashboard-kpi-card__trend dashboard-kpi-card__trend--danger">
            {summary.devices.offline}
          </div>
        </button>

        <button
          type="button"
          className="dashboard-kpi-card"
          onClick={() =>
            navigate(
              complianceRoute,
            )
          }
        >
          <div className="dashboard-kpi-card__icon dashboard-kpi-card__icon--success">
            <ShieldCheck
              size={20}
            />
          </div>

          <div className="dashboard-kpi-card__content">
            <span>
              Cumplimiento
            </span>

            <strong>
              {summary
                .compliance
                .compliancePercentage ===
              null
                ? 'N/D'
                : `${compliancePercentage}%`}
            </strong>

            <small>
              {summary.compliance.compliant}{' '}
              conformes
            </small>
          </div>

          <div className="dashboard-kpi-card__trend">
            {summary
              .compliance
              .nonCompliant}{' '}
            alertas
          </div>
        </button>

        <button
          type="button"
          className="dashboard-kpi-card"
          onClick={() =>
            navigate(
              automationRoute,
            )
          }
        >
          <div className="dashboard-kpi-card__icon">
            <TerminalSquare
              size={20}
            />
          </div>

          <div className="dashboard-kpi-card__content">
            <span>
              Commands
            </span>

            <strong>
              {summary.commands.active}
            </strong>

            <small>
              {commandSuccessPercentage}% correctos
            </small>
          </div>

          <div className="dashboard-kpi-card__trend">
            {summary.commands.total}
          </div>
        </button>
      </section>

      {/* ======================================================
          DEVICE + PLATFORM
         ====================================================== */}

      <section className="dashboard-chart-grid">
        <article className="dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <div>
              <span>
                INVENTARIO
              </span>

              <h2>
                Estado de dispositivos
              </h2>

              <p>
                Distribución actual del inventario administrado.
              </p>
            </div>

            <Activity
              size={20}
            />
          </header>

          <div className="dashboard-donut-layout">
            <div className="dashboard-chart-container dashboard-chart-container--donut">
              <ResponsiveContainer
                width="100%"
                height={250}
              >
                <PieChart>
                  <Pie
                    data={
                      deviceStatusData
                    }
                    dataKey="value"
                    nameKey="name"
                    innerRadius={70}
                    outerRadius={98}
                    paddingAngle={3}
                    stroke="none"
                  >
                    {deviceStatusData.map(
                      item => (
                        <Cell
                          key={
                            item.name
                          }
                          fill={
                            item.color
                          }
                        />
                      ),
                    )}
                  </Pie>

                  <Tooltip
                    content={
                      <ChartTooltip />
                    }
                  />
                </PieChart>
              </ResponsiveContainer>

              <div className="dashboard-donut-center">
                <strong>
                  {summary.devices.total}
                </strong>

                <span>
                  Total
                </span>
              </div>
            </div>

            <div className="dashboard-chart-legend">
              {deviceStatusData.map(
                item => (
                  <div
                    key={
                      item.name
                    }
                  >
                    <span
                      className="dashboard-chart-legend__dot"
                      style={{
                        background:
                          item.color,
                      }}
                    />

                    <span>
                      {item.name}
                    </span>

                    <strong>
                      {item.value}
                    </strong>
                  </div>
                ),
              )}
            </div>
          </div>
        </article>

        <article className="dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <div>
              <span>
                PLATAFORMAS
              </span>

              <h2>
                Distribución de sistemas
              </h2>

              <p>
                {dashboardWorkspace ===
                'windows'
                  ? 'Inventario Windows administrado.'
                  : dashboardWorkspace ===
                      'android'
                    ? 'Inventario Android Enterprise administrado.'
                    : 'Distribución global Windows y Android.'}
              </p>
            </div>

            <Smartphone
              size={20}
            />
          </header>

          <div className="dashboard-donut-layout">
            <div className="dashboard-chart-container dashboard-chart-container--donut">
              <ResponsiveContainer
                width="100%"
                height={250}
              >
                <PieChart>
                  <Pie
                    data={
                      platformData
                    }
                    dataKey="value"
                    nameKey="name"
                    innerRadius={70}
                    outerRadius={98}
                    paddingAngle={4}
                    stroke="none"
                  >
                    {platformData.map(
                      item => (
                        <Cell
                          key={
                            item.name
                          }
                          fill={
                            item.color
                          }
                        />
                      ),
                    )}
                  </Pie>

                  <Tooltip
                    content={
                      <ChartTooltip />
                    }
                  />
                </PieChart>
              </ResponsiveContainer>

              <div className="dashboard-donut-center">
                <strong>
                  {platformTotal}
                </strong>

                <span>
                  Plataformas
                </span>
              </div>
            </div>

            <div className="dashboard-chart-legend">
              {platformData.map(
                item => (
                  <div
                    key={
                      item.name
                    }
                  >
                    <span
                      className="dashboard-chart-legend__dot"
                      style={{
                        background:
                          item.color,
                      }}
                    />

                    <span>
                      {item.name}
                    </span>

                    <strong>
                      {item.value}
                    </strong>
                  </div>
                ),
              )}
            </div>
          </div>
        </article>
      </section>

      {/* ======================================================
          COMPLIANCE + COMMANDS
         ====================================================== */}

      <section className="dashboard-chart-grid">
        <article className="dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <div>
              <span>
                SEGURIDAD
              </span>

              <h2>
                Cumplimiento
              </h2>

              <p>
                Estado actual de las evaluaciones de cumplimiento.
              </p>
            </div>

            <ShieldCheck
              size={20}
            />
          </header>

          <div className="dashboard-chart-container">
            <ResponsiveContainer
              width="100%"
              height={270}
            >
              <BarChart
                data={
                  complianceData
                }
                margin={{
                  top: 20,
                  right: 10,
                  left: -18,
                  bottom: 5,
                }}
              >
                <CartesianGrid
                  strokeDasharray="4 4"
                  vertical={false}
                  stroke="#edf1f6"
                />

                <XAxis
                  dataKey="name"
                  tickLine={false}
                  axisLine={false}
                  tick={{
                    fontSize: 10,
                    fill: '#7b8798',
                  }}
                />

                <YAxis
                  allowDecimals={false}
                  tickLine={false}
                  axisLine={false}
                  tick={{
                    fontSize: 10,
                    fill: '#9aa5b5',
                  }}
                />

                <Tooltip
                  content={
                    <ChartTooltip />
                  }
                />

                <Bar
                  dataKey="value"
                  name="Dispositivos"
                  fill={
                    workspaceChartColor
                  }
                  radius={[
                    7,
                    7,
                    0,
                    0,
                  ]}
                />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </article>

        <article className="dashboard-chart-card">
          <header className="dashboard-chart-card__header">
            <div>
              <span>
                AUTOMATIZACIÓN
              </span>

              <h2>
                Command Engine
              </h2>

              <p>
                Resultado de comandos enviados a los endpoints.
              </p>
            </div>

            <TerminalSquare
              size={20}
            />
          </header>

          <div className="dashboard-chart-container">
            <ResponsiveContainer
              width="100%"
              height={270}
            >
              <BarChart
                data={
                  commandData
                }
                layout="vertical"
                margin={{
                  top: 15,
                  right: 25,
                  left: 15,
                  bottom: 5,
                }}
              >
                <CartesianGrid
                  strokeDasharray="4 4"
                  horizontal={false}
                  stroke="#edf1f6"
                />

                <XAxis
                  type="number"
                  allowDecimals={false}
                  tickLine={false}
                  axisLine={false}
                  tick={{
                    fontSize: 10,
                    fill: '#9aa5b5',
                  }}
                />

                <YAxis
                  type="category"
                  dataKey="name"
                  tickLine={false}
                  axisLine={false}
                  width={80}
                  tick={{
                    fontSize: 10,
                    fill: '#7b8798',
                  }}
                />

                <Tooltip
                  content={
                    <ChartTooltip />
                  }
                />

                <Bar
                  dataKey="value"
                  name="Comandos"
                  fill={
                    workspaceChartColor
                  }
                  radius={[
                    0,
                    7,
                    7,
                    0,
                  ]}
                />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </article>
      </section>

      {/* ======================================================
          WORKSPACE ADVANCED INSIGHTS
         ====================================================== */}

      {dashboardWorkspace !==
        'global' && (
        <WorkspaceInsights
          workspace={
            dashboardWorkspace
          }
          summary={
            summary
          }
        />
      )}

      {/* ======================================================
          OPERATIONS
         ====================================================== */}

      <section className="dashboard-operations-grid">
        <article className="dashboard-operation-card dashboard-operation-card--attention">
          <header>
            <div>
              <AlertTriangle
                size={20}
              />

              <div>
                <span>
                  ATENCIÓN
                </span>

                <h2>
                  Requiere revisión
                </h2>
              </div>
            </div>

            <strong>
              {attentionCount}
            </strong>
          </header>

          <div className="dashboard-operation-list">
            <div>
              <span>
                Equipos offline
              </span>

              <strong>
                {summary.devices.offline}
              </strong>
            </div>

            <div>
              <span>
                No conformes
              </span>

              <strong>
                {
                  summary
                    .compliance
                    .nonCompliant
                }
              </strong>
            </div>

            <div>
              <span>
                Cuarentena
              </span>

              <strong>
                {
                  summary
                    .devices
                    .quarantined
                }
              </strong>
            </div>

            <div>
              <span>
                Problemas de comandos
              </span>

              <strong>
                {
                  summary
                    .commands
                    .problems
                }
              </strong>
            </div>
          </div>
        </article>

        <article className="dashboard-operation-card">
          <header>
            <div>
              <Server
                size={20}
              />

              <div>
                <span>
                  INFRAESTRUCTURA
                </span>

                <h2>
                  Salud de plataforma
                </h2>
              </div>
            </div>

            <CheckCircle2
              size={22}
              className="dashboard-health-ok"
            />
          </header>

          <div className="dashboard-operation-list">
            <div>
              <span>
                <Server
                  size={14}
                />

                API
              </span>

              <strong>
                {summary.system.api}
              </strong>
            </div>

            <div>
              <span>
                <Database
                  size={14}
                />

                SQL Server
              </span>

              <strong>
                {summary.system.database}
              </strong>
            </div>

            <div>
              <span>
                Cobertura administrada
              </span>

              <strong>
                {managedPercentage}%
              </strong>
            </div>

            <div>
              <span>
                Éxito de comandos
              </span>

              <strong>
                {commandSuccessPercentage}%
              </strong>
            </div>
          </div>
        </article>
      </section>

      {/* ======================================================
          FOOTER
         ====================================================== */}

      <footer className="dashboard-generated-at">
        <Clock3
          size={14}
        />

        Última actualización:
        {' '}
        {generatedAt}
      </footer>
    </div>
  )
}