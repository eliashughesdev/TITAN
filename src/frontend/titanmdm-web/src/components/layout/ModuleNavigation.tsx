import {
  BarChart3,
  Building2,
  ClipboardList,
  Cog,
  FileBarChart,
  Headphones,
  Inbox,
  LayoutDashboard,
  MapPin,
  Monitor,
  Package,
  Plus,
  ScrollText,
  Settings,
  Shield,
  Smartphone,
  UserRound,
  Users,
  Workflow,
  type LucideIcon,
} from 'lucide-react'

import {
  NavLink,
  useLocation,
} from 'react-router-dom'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  canCreateHelpdeskRequest,
  canManageHelpdesk,
  canUseHelpdeskAnalytics,
  canUseHelpdeskConsole,
  helpdeskPermissions,
} from '../../auth/helpdeskAccess'

import {
  useWorkspace,
} from '../../workspace/WorkspaceContext'

import './ModuleNavigation.css'

interface ModuleLink {
  label: string
  path: string
  icon: LucideIcon
  permissions?: string[]
}

const link = (
  label: string,
  path: string,
  icon: LucideIcon,
  ...permissions: string[]
): ModuleLink => ({
  label,
  path,
  icon,
  permissions,
})

const windowsLinks:
  ModuleLink[] = [
    link(
      'Resumen',
      '/dashboard?workspace=windows',
      Monitor,
      'dashboard.view',
    ),

    link(
      'Dispositivos',
      '/devices?workspace=windows',
      Monitor,
      'devices.view',
    ),

    link(
      'Grupos',
      '/groups?workspace=windows',
      Users,
      'devices.view',
    ),

    link(
      'Inscripción',
      '/enrollment?workspace=windows',
      Smartphone,
      'enrollment.view',
    ),

    link(
      'Políticas',
      '/policies?workspace=windows',
      ClipboardList,
      'policies.view',
    ),

    link(
      'Aplicaciones',
      '/apps?workspace=windows',
      Package,
      'apps.view',
    ),

    link(
      'Seguridad',
      '/security?workspace=windows',
      Shield,
      'security.view',
    ),

    link(
      'Soporte remoto',
      '/remote?workspace=windows',
      Headphones,
      'remote.view',
    ),

    link(
      'Reportes',
      '/reports?workspace=windows',
      FileBarChart,
      'reports.view',
    ),
  ]

const androidLinks:
  ModuleLink[] = [
    link(
      'Resumen',
      '/dashboard?workspace=android',
      BarChart3,
      'dashboard.view',
    ),

    link(
      'Dispositivos',
      '/devices?workspace=android',
      Smartphone,
      'devices.view',
    ),

    link(
      'Inscripción',
      '/enrollment?workspace=android',
      Smartphone,
      'enrollment.view',
    ),

    link(
      'Políticas',
      '/policies?workspace=android',
      ClipboardList,
      'policies.view',
    ),

    link(
      'Aplicaciones',
      '/apps?workspace=android',
      Package,
      'apps.view',
    ),

    link(
      'Kiosk',
      '/kiosk?workspace=android',
      Cog,
      'kiosk.view',
    ),

    link(
      'Geofencing',
      '/geofencing?workspace=android',
      MapPin,
      'geofencing.view',
    ),
  ]

const requesterLinks:
  ModuleLink[] = [
    link(
      'Mis solicitudes',
      '/my-support?workspace=helpdesk',
      Inbox,
      helpdeskPermissions.requestOwnView,
    ),

    link(
      'Crear solicitud',
      '/my-support/new?workspace=helpdesk',
      Plus,
      helpdeskPermissions.requestCreate,
    ),
  ]

const helpdeskOperationLinks:
  ModuleLink[] = [
    link(
      'Mi trabajo',
      '/helpdesk?workspace=helpdesk',
      ClipboardList,
      helpdeskPermissions.inboxMyWork,
    ),

    link(
      'Sin asignar',
      '/helpdesk?view=unassigned&workspace=helpdesk',
      Inbox,
      helpdeskPermissions.inboxUnassigned,
    ),

    link(
      'Todos',
      '/helpdesk?view=all&workspace=helpdesk',
      ClipboardList,
      helpdeskPermissions.inboxAll,
    ),

    link(
      'Kanban',
      '/helpdesk?view=kanban&workspace=helpdesk',
      Workflow,
      helpdeskPermissions.kanbanView,
    ),

    link(
      'Mis solicitudes',
      '/my-support?workspace=helpdesk',
      UserRound,
      helpdeskPermissions.requestOwnView,
    ),
  ]



const helpdeskAdminEntry:
  ModuleLink[] = [
    link(
      'Administración',
      '/helpdesk/admin?workspace=helpdesk',
      Settings,
      helpdeskPermissions.adminAccess,
    ),
  ]

const adminLinks:
  ModuleLink[] = [
    link(
      'General',
      '/settings?workspace=administration',
      Settings,
      'settings.view',
    ),

    link(
      'Usuarios',
      '/users?workspace=administration',
      Users,
      'users.view',
    ),

    link(
      'Roles',
      '/roles?workspace=administration',
      Shield,
      'roles.view',
    ),

    link(
      'Localidades',
      '/sites?workspace=administration',
      Building2,
      'sites.view',
    ),

    link(
      'Auditoría',
      '/audit?workspace=administration',
      ScrollText,
      'audit.view',
    ),
  ]

export function ModuleNavigation() {
  const {
    hasPermission,
  } =
    useAuth()

  const {
    activeWorkspaceId,
  } =
    useWorkspace()

  const location =
    useLocation()

  if (
    location.pathname ===
      '/ponches'
    ||
    location.pathname
      .startsWith(
        '/ponches/',
      )
  ) {
    return null
  }

  const staff =
    canUseHelpdeskConsole(
      hasPermission,
    )

  const analytics =
    canUseHelpdeskAnalytics(
      hasPermission,
    )

  const admin =
    canManageHelpdesk(
      hasPermission,
    )

  const workspace =
    location.pathname
      .startsWith(
        '/my-support',
      )
    ||
    location.pathname
      .startsWith(
        '/helpdesk',
      )
      ? 'helpdesk'
      : location.pathname
          .startsWith(
            '/sites',
          )
        ? 'administration'
        : activeWorkspaceId

  let title =
    ''

  let links:
    ModuleLink[] = []

  if (
    workspace ===
    'windows'
  ) {
    title =
      'Windows'

    links =
      windowsLinks
  }
  else if (
    workspace ===
    'android'
  ) {
    title =
      'Android'

    links =
      androidLinks
  }
  else if (
    workspace ===
    'administration'
  ) {
    title =
      'Configuración'

    links =
      adminLinks
  }
  else if (
    workspace ===
    'helpdesk'
  ) {
    /*
     * ==========================================================
     * COLABORADOR
     * ==========================================================
     */

    if (
      !staff
    ) {
      title =
        'Mesa de ayuda'

      links =
        requesterLinks
    }

    /*
     * ==========================================================
     * TIC
     * ==========================================================
     */

    if (
      staff
    ) {
      title =
        'Mesa de ayuda · TIC'

      links = [
        ...helpdeskOperationLinks,

        ...(
          analytics
            ? [
                link(
                  'Analítica',
                  '/helpdesk/analytics?workspace=helpdesk',
                  LayoutDashboard,
                ),
              ]
            : []
        ),

        ...(
          admin
            ? helpdeskAdminEntry
            : []
        ),
      ]
    }
  }

  const visible =
    links.filter(
      item =>
        !item.permissions
          ?.length
        ||
        item.permissions
          .some(
            hasPermission,
          ),
    )

  if (
    workspace ===
      'helpdesk'
    &&
    !staff
    &&
    canCreateHelpdeskRequest(
      hasPermission,
    )
    &&
    !visible.some(
      item =>
        item.path.startsWith(
          '/my-support/new',
        ),
    )
  ) {
    visible.push(
      requesterLinks[1],
    )
  }

  if (
    !visible.length
  ) {
    return null
  }

  return (
    <nav
      className="module-navigation"
      aria-label={
        `Opciones de ${title}`
      }
    >
      <span
        className="module-navigation__title"
      >
        {title}
      </span>

      <div
        className="module-navigation__links"
      >
        {visible.map(
          item => {
            const Icon =
              item.icon

            const basePath =
              item.path
                .split('?')[0]

            const query =
  new URLSearchParams(
    item.path.split('?')[1]
    ?? '',
  )

const itemView =
  query.get(
    'view',
  )

const currentView =
  new URLSearchParams(
    location.search,
  ).get(
    'view',
  )

const helpdeskInboxActive =
  basePath ===
    '/helpdesk'
  &&
  location.pathname ===
    '/helpdesk'
  &&
  (
    itemView
      ? currentView ===
          itemView
      : !currentView
        ||
        currentView ===
          'mine'
  )

const active =
  helpdeskInboxActive
  ||
  (
    basePath !==
      '/helpdesk'
    &&
    location.pathname ===
      basePath
  )
  ||
  (
    basePath ===
      '/helpdesk/admin'
    &&
    location.pathname
      .startsWith(
        '/helpdesk/admin',
      )
  )
  ||
  (
    basePath ===
      '/helpdesk/analytics'
    &&
    (
      location.pathname
        .startsWith(
          '/helpdesk/analytics',
        )
      ||
      location.pathname
        .startsWith(
          '/helpdesk/centro/kpis',
        )
      ||
      location.pathname
        .startsWith(
          '/helpdesk/reportes',
        )
      ||
      location.pathname
        .startsWith(
          '/helpdesk/seguimiento',
        )
    )
  )

            return (
              <NavLink
                key={
                  item.path
                }
                to={
                  item.path
                }
                end
                className={
                  active
                    ? 'module-navigation__link module-navigation__link--active'
                    : 'module-navigation__link'
                }
              >
                <Icon
                  size={15}
                />

                {item.label}
              </NavLink>
            )
          },
        )}
      </div>
    </nav>
  )
}