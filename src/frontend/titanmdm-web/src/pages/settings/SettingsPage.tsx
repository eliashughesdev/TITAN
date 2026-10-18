import {
  Building2,
  CheckCircle2,
  Clock3,
  CloudCog,
  Cpu,
  Mail,
  MapPinned,
  Settings,
  ShieldCheck,
  Tags,
  ClipboardList,
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

import {
  SettingsCard,
  SettingsPageHeader,
  SettingsSection,
  SettingsStatus,
} from '../../components/settings'

import './SettingsPage.css'

interface ConfigurationCard {
  title: string
  description: string
  path: string
  icon: typeof Settings
  permissions: string[]
  badge?: string
}

interface ConfigurationSection {
  id: string
  title: string
  description: string
  cards: ConfigurationCard[]
}

export function SettingsPage() {
  const navigate =
    useNavigate()

  const {
    hasPermission,
  } =
    useAuth()

  const isHelpdeskAdmin =
    hasPermission(
      helpdeskPermissions
        .adminAccess,
    )
    ||
    hasPermission(
      'helpdesk.manage',
    )
    ||
    hasPermission(
      'settings.manage',
    )

  const sections:
    ConfigurationSection[] = [
      {
        id:
          'helpdesk',

        title:
          'Mesa de Ayuda',

        description:
          'Configuración empresarial de tickets, SLA, Microsoft 365, Entra ID, técnicos, localidades y routing.',

        cards: [
          {
            title:
              'SLA y reapertura',

            description:
              'Tiempos de primera respuesta y resolución por prioridad, pausa esperando usuario, reapertura y escalamiento.',

            path:
              '/helpdesk/centro/configuracion?workspace=helpdesk',

            icon:
              Clock3,

            permissions: [
              helpdeskPermissions
                .slaManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',

              'settings.manage',
            ],

            badge:
              'Configurado',
          },

          {
            title:
              'Correo Microsoft 365',

            description:
              'Buzón de Mesa de Ayuda, entrada y salida, polling, reintentos, Outbox, diagnóstico Graph y estado operativo.',

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

              'helpdesk.manage',

              'settings.manage',
            ],

            badge:
              'Microsoft Graph',
          },
          {
  title:
    'Plantillas de tickets',

  description:
    'Crea solicitudes guiadas con categoría, tipo y preguntas específicas para cada necesidad del colaborador.',

  path:
    '/helpdesk/templates?workspace=helpdesk',

  icon:
    ClipboardList,

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

  badge:
    'Catálogo',
},

          {
            title:
              'Microsoft Entra ID',

            description:
              'Tenant, App Registration, sincronización del directorio, grupos permitidos y acceso corporativo.',

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

            badge:
              'Identidad',
          },

          {
            title:
              'Localidades y cobertura',

            description:
              'Sites, sublocalidades y cobertura territorial utilizada por el motor de asignación automática.',

            path:
              '/helpdesk/operations?workspace=helpdesk',

            icon:
              MapPinned,

            permissions: [
              helpdeskPermissions
                .sitesView,

              helpdeskPermissions
                .sitesManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',
            ],
          },

          {
            title:
              'Técnicos',

            description:
              'Personal TIC disponible para Mesa de Ayuda, capacidad máxima, disponibilidad y asignación automática.',

            path:
              '/helpdesk/operations?tab=technicians&workspace=helpdesk',

            icon:
              Users,

            permissions: [
              helpdeskPermissions
                .techniciansView,

              helpdeskPermissions
                .techniciansManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',
            ],
          },

          {
            title:
              'Grupos y especialidades',

            description:
              'Equipos de trabajo, categorías atendidas, integrantes y reglas de cobertura operativa.',

            path:
              '/helpdesk/especialidades?workspace=helpdesk',

            icon:
              Tags,

            permissions: [
              helpdeskPermissions
                .groupsView,

              helpdeskPermissions
                .groupsManage,

              helpdeskPermissions
                .categoriesView,

              helpdeskPermissions
                .categoriesManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',
            ],
          },

          {
            title:
              'Turnos y capacidad',

            description:
              'Horarios semanales, zona horaria, disponibilidad y prioridad utilizada por el routing Enterprise.',

            path:
              '/helpdesk/especialidades?workspace=helpdesk',

            icon:
              Building2,

            permissions: [
              helpdeskPermissions
                .schedulesView,

              helpdeskPermissions
                .schedulesManage,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',
            ],
          },

          {
            title:
              'Validación de cobertura',

            description:
              'Diagnóstico de Sites, Locations, grupos, categorías y técnicos antes de activar autoasignación.',

            path:
              '/helpdesk/cobertura?workspace=helpdesk',

            icon:
              ShieldCheck,

            permissions: [
              helpdeskPermissions
                .sitesView,

              helpdeskPermissions
                .groupsView,

              helpdeskPermissions
                .adminAccess,

              'helpdesk.manage',
            ],
          },
        ],
      },

      {
        id:
          'platform',

        title:
          'Administración de plataforma',

        description:
          'Configuraciones generales relacionadas con identidad, seguridad y estructura administrativa de TitanMDM.',

        cards: [
          {
            title:
              'Proveedores de inteligencia artificial',

            description:
              'Estado seguro de OpenRouter y Ollama, modelo efectivo, latencia, timeout y límites del asistente.',

            path:
              '/settings/ai-providers?workspace=administration',

            icon:
              Cpu,

            permissions: [
              'settings.view',
              'settings.manage',
            ],

            badge:
              'AI-2',
          },
          {
            title:
              'Usuarios y roles',

            description:
              'Gestiona usuarios, permisos y roles que determinan qué módulos y acciones puede utilizar cada persona.',

            path:
              '/roles?workspace=administration',

            icon:
              ShieldCheck,

            permissions: [
              'roles.view',
              'roles.manage',
              'settings.manage',
            ],
          },

          {
            title:
              'Sites corporativos',

            description:
              'Estructura física principal utilizada por dispositivos, usuarios y servicios empresariales.',

            path:
              '/sites?workspace=administration',

            icon:
              Building2,

            permissions: [
              'sites.view',
              'sites.manage',
              'settings.manage',
            ],
          },
        ],
      },
    ]

  const visibleSections =
    sections
      .map(
        section => ({
          ...section,

          cards:
            section.cards.filter(
              card =>
                card.permissions
                  .some(
                    hasPermission,
                  )
                ||
                isHelpdeskAdmin,
            ),
        }),
      )
      .filter(
        section =>
          section.cards.length >
          0,
      )

  return (
    <main
      className="titan-page titan-settings"
    >
      <SettingsPageHeader
        eyebrow="Administración"
        title="Configuración"
        description={
          'Centraliza la configuración ' +
          'administrativa de TitanMDM sin ' +
          'depender de cambios manuales en ' +
          'código o archivos del servidor.'
        }
        icon={
          <Settings
            size={16}
          />
        }
        status={
          <div
            className="titan-settings__hero-status"
          >
            <CheckCircle2
              size={18}
            />

            <div>
              <strong>
                Configuración centralizada
              </strong>

              <span>
                Los parámetros operativos
                deben administrarse desde
                estas pantallas.
              </span>
            </div>
          </div>
        }
      />

      <SettingsStatus
        tone="neutral"
        icon={
          <ShieldCheck
            size={19}
          />
        }
        title="Configuración de Mesa de Ayuda"
      >
        <span>
          SLA, Microsoft 365, Entra ID,
          técnicos, localidades, grupos,
          turnos y cobertura se administran
          desde aquí. No es necesario
          modificar el código para cambiar
          el buzón o los parámetros
          operativos.
        </span>
      </SettingsStatus>

      <div
        className="titan-settings__sections"
      >
        {visibleSections.map(
          section => (
            <SettingsSection
              key={
                section.id
              }
              title={
                section.title
              }
              description={
                section.description
              }
              className="titan-settings__section"
            >
              <div
                className="titan-settings__grid"
              >
                {section.cards.map(
                  card => {
                    const Icon =
                      card.icon

                    return (
                      <SettingsCard
                        key={
                          card.title
                        }
                        title={
                          card.title
                        }
                        description={
                          card.description
                        }
                        icon={
                          <Icon
                            size={21}
                          />
                        }
                        badge={
                          card.badge
                        }
                        onClick={
                          () =>
                            navigate(
                              card.path,
                            )
                        }
                      />
                    )
                  },
                )}
              </div>
            </SettingsSection>
          ),
        )}
      </div>
    </main>
  )
}
