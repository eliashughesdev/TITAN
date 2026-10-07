import {
  Activity,
  ArrowRight,
  FileBarChart,
  Gauge,
  Timer,
} from 'lucide-react'

import {
  useNavigate,
} from 'react-router-dom'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  helpdeskPermissions,
} from '../../auth/helpdeskAccess'

import './HelpdeskPages.css'

interface AnalyticsCard {
  title: string
  description: string
  path: string
  icon: typeof Gauge
  permissions: string[]
}

export function HelpdeskAnalyticsHome() {
  const navigate =
    useNavigate()

  const {
    hasPermission,
  } =
    useAuth()

  const cards:
    AnalyticsCard[] = [
      {
        title:
          'SLA y seguimiento',

        description:
          'Casos en riesgo, vencimientos, primera respuesta, resolución y seguimiento del cumplimiento.',

        path:
          '/helpdesk/seguimiento?workspace=helpdesk',

        icon:
          Timer,

        permissions: [
          helpdeskPermissions
            .slaView,
        ],
      },

      {
        title:
          'KPI operativos',

        description:
          'Resumen rápido del backlog, tickets activos, autoasignación, carga y cumplimiento operativo.',

        path:
          '/helpdesk/centro/kpis?workspace=helpdesk',

        icon:
          Gauge,

        permissions: [
          helpdeskPermissions
            .kpiView,
        ],
      },

      {
        title:
          'Reportes y análisis',

        description:
          'Panel detallado con tendencias, SLA, prioridades, categorías, Sites, técnicos, automatización y exportación.',

        path:
          '/helpdesk/reportes?workspace=helpdesk',

        icon:
          FileBarChart,

        permissions: [
          helpdeskPermissions
            .reportsView,

          helpdeskPermissions
            .analyticsView,
        ],
      },
    ]

  const visible =
    cards.filter(
      card =>
        card.permissions.some(
          hasPermission,
        )
        ||
        hasPermission(
          helpdeskPermissions
            .adminAccess,
        ),
    )

  return (
    <main
      className={
        'titan-page ' +
        'helpdesk-page ' +
        'helpdesk-hub'
      }
    >
      <header
        className="helpdesk-hub__hero"
      >
        <div>
          <span
            className="helpdesk-hub__eyebrow"
          >
            <Activity
              size={15}
            />

            SERVICE INTELLIGENCE
          </span>

          <h1>
            Analítica de Mesa de Ayuda
          </h1>

          <p>
            Accede únicamente a vistas
            que aportan una función
            diferente: seguimiento SLA,
            indicadores rápidos y
            reportes detallados.
          </p>
        </div>
      </header>

      <section
        className="helpdesk-hub__grid"
      >
        {visible.map(
          card => {
            const Icon =
              card.icon

            return (
              <button
                key={
                  card.title
                }
                type="button"
                className="helpdesk-hub__card"
                onClick={
                  () =>
                    navigate(
                      card.path,
                    )
                }
              >
                <span
                  className="helpdesk-hub__icon"
                >
                  <Icon
                    size={22}
                  />
                </span>

                <div>
                  <h2>
                    {
                      card.title
                    }
                  </h2>

                  <p>
                    {
                      card.description
                    }
                  </p>
                </div>

                <ArrowRight
                  size={18}
                />
              </button>
            )
          },
        )}
      </section>
    </main>
  )
}