import {
  ArrowRight,
  Building2,
  Clock3,
  CloudCog,
  FileText,
  Mail,
  Settings,
  Tags,
  Users,
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

interface AdminAction {
  title: string
  description: string
  path: string
  icon: typeof Settings
  permissions: string[]
}

interface AdminSection {
  title: string
  description: string
  actions: AdminAction[]
}

export function HelpdeskAdminHome() {
  const navigate =
    useNavigate()

  const {
    hasPermission,
  } =
    useAuth()

  const sections:
    AdminSection[] = [
      {
        title:
          'Organización operativa',

        description:
          'Define dónde se presta soporte, quién atiende y cómo se distribuye el trabajo.',

        actions: [
          {
            title:
              'Localidades y cobertura',

            description:
              'Administra Sites, sublocalidades y la cobertura territorial de la Mesa de Ayuda.',

            path:
              '/helpdesk/operations?workspace=helpdesk',

            icon:
              Building2,

            permissions: [
              helpdeskPermissions
                .sitesView,

              helpdeskPermissions
                .sitesManage,
            ],
          },

          {
            title:
              'Técnicos',

            description:
              'Configura disponibilidad, capacidad, ubicación y elegibilidad del personal TIC.',

            path:
              '/helpdesk/operations?tab=technicians&workspace=helpdesk',

            icon:
              Users,

            permissions: [
              helpdeskPermissions
                .techniciansView,

              helpdeskPermissions
                .techniciansManage,
            ],
          },

          {
            title:
              'Grupos de trabajo',

            description:
              'Organiza áreas TIC, categorías, cobertura, técnicos y reglas de asignación.',

            path:
              '/helpdesk/especialidades?workspace=helpdesk',

            icon:
              Users,

            permissions: [
              helpdeskPermissions
                .groupsView,

              helpdeskPermissions
                .groupsManage,
            ],
          },

          {
            title:
              'Categorías',

            description:
              'Define las especialidades y tipos de solicitudes que atiende cada grupo.',

            path:
              '/helpdesk/especialidades?tab=categories&workspace=helpdesk',

            icon:
              Tags,

            permissions: [
              helpdeskPermissions
                .categoriesView,

              helpdeskPermissions
                .categoriesManage,
            ],
          },

          {
            title:
              'Turnos y capacidad',

            description:
              'Configura horarios, disponibilidad, prioridad y capacidad máxima de los técnicos.',

            path:
              '/helpdesk/especialidades?tab=schedules&workspace=helpdesk',

            icon:
              Clock3,

            permissions: [
              helpdeskPermissions
                .schedulesView,

              helpdeskPermissions
                .schedulesManage,
            ],
          },
        ],
      },

      {
        title:
          'Experiencia del solicitante',

        description:
          'Configura cómo los usuarios crean solicitudes y qué información debe recopilar TitanMDM.',

        actions: [
          {
            title:
              'Plantillas de tickets',

            description:
              'Crea formularios guiados por categoría con preguntas obligatorias para solicitudes frecuentes.',

            path:
              '/helpdesk/templates?workspace=helpdesk',

            icon:
              FileText,

            permissions: [
              helpdeskPermissions
                .templatesView,

              helpdeskPermissions
                .templatesManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',

              'settings.manage',
            ],
          },
        ],
      },

      {
        title:
          'Integraciones',

        description:
          'Conecta TitanMDM con Microsoft 365 y el directorio corporativo.',

        actions: [
          {
            title:
              'Microsoft Entra ID',

            description:
              'Sincronización del directorio, vinculación de usuarios e inicio de sesión corporativo.',

            path:
              '/helpdesk/entra?workspace=helpdesk',

            icon:
              CloudCog,

            permissions: [
              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',

              'settings.manage',
            ],
          },

          {
            title:
              'Correo Microsoft 365',

            description:
              'Configura el buzón, recepción, envío, reintentos, dead-letter y diagnóstico Microsoft Graph.',

            path:
              '/helpdesk/mail?workspace=helpdesk',

            icon:
              Mail,

            permissions: [
              helpdeskPermissions
                .mailView,

              helpdeskPermissions
                .mailManage,

              helpdeskPermissions
                .adminAccess,
            ],
          },
        ],
      },

      {
        title:
          'Configuración del servicio',

        description:
          'Define los parámetros que gobiernan el ciclo de vida y los compromisos de atención.',

        actions: [
          {
            title:
              'SLA y reapertura',

            description:
              'Configura primera respuesta, resolución, pausas, escalamiento y período de reapertura.',

            path:
              '/helpdesk/centro/configuracion?workspace=helpdesk',

            icon:
              Settings,

            permissions: [
              helpdeskPermissions
                .adminAccess,

              helpdeskPermissions
                .slaManage,
            ],
          },
        ],
      },
    ]

  return (
    <main
      className={
        'titan-page ' +
        'helpdesk-page ' +
        'helpdesk-admin-home'
      }
    >
      <header
        className="helpdesk-admin-home__hero"
      >
        <div>
          <span
            className="helpdesk-hub__eyebrow"
          >
            <Settings
              size={15}
            />

            ADMINISTRACIÓN HELPDESK
          </span>

          <h1>
            Administración de Mesa de Ayuda
          </h1>

          <p>
            Configura la operación,
            experiencia del solicitante,
            integraciones y parámetros
            del servicio desde un único
            punto.
          </p>
        </div>
      </header>

      <div
        className="helpdesk-admin-home__sections"
      >
        {sections.map(
          section => {
            const actions =
              section.actions.filter(
                action =>
                  action.permissions.some(
                    hasPermission,
                  )
                  ||
                  hasPermission(
                    helpdeskPermissions
                      .adminAccess,
                  ),
              )

            if (
              !actions.length
            ) {
              return null
            }

            return (
              <section
                key={
                  section.title
                }
                className="helpdesk-admin-home__section"
              >
                <header>
                  <h2>
                    {
                      section.title
                    }
                  </h2>

                  <p>
                    {
                      section.description
                    }
                  </p>
                </header>

                <div
                  className="helpdesk-admin-home__grid"
                >
                  {actions.map(
                    action => {
                      const Icon =
                        action.icon

                      return (
                        <button
                          key={
                            action.title
                          }
                          type="button"
                          className="helpdesk-admin-home__card"
                          onClick={
                            () =>
                              navigate(
                                action.path,
                              )
                          }
                        >
                          <span
                            className="helpdesk-admin-home__icon"
                          >
                            <Icon
                              size={20}
                            />
                          </span>

                          <div>
                            <h3>
                              {
                                action.title
                              }
                            </h3>

                            <p>
                              {
                                action.description
                              }
                            </p>
                          </div>

                          <ArrowRight
                            size={17}
                          />
                        </button>
                      )
                    },
                  )}
                </div>
              </section>
            )
          },
        )}
      </div>
    </main>
  )
}