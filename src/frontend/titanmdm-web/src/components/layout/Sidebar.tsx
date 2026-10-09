import {
  ChevronLeft,
  ChevronRight,
  Clock3,
  Headphones,
  Home,
  MonitorCog,
  Settings,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'

import {
  NavLink,
  useLocation,
} from 'react-router-dom'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  canUseHelpdeskPortal,
  resolveHelpdeskLandingPath,
} from '../../auth/helpdeskAccess'

import {
  useWorkspace,
} from '../../workspace/WorkspaceContext'

interface Props {
  collapsed: boolean
  onToggle: () => void
}

export function Sidebar({
  collapsed,
  onToggle,
}: Props) {
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

  /*
   * ============================================================
   * WORKSPACE ACCESS
   * ============================================================
   */

  const canWindows =
    hasPermission(
      'workspace.windows.view',
    )

  const canAndroid =
    hasPermission(
      'workspace.android.view',
    )

  const canHelpdesk =
    hasPermission(
      'workspace.helpdesk.view',
    )
    ||
    canUseHelpdeskPortal(
      hasPermission,
    )
    ||
    hasPermission(
      'helpdesk.agent.access',
    )
    ||
    hasPermission(
      'helpdesk.admin.access',
    )

  const hasAdministrationFeature =
    hasPermission(
      'settings.view',
    )
    ||
    hasPermission(
      'settings.manage',
    )
    ||
    hasPermission(
      'users.view',
    )
    ||
    hasPermission(
      'users.manage',
    )
    ||
    hasPermission(
      'roles.view',
    )
    ||
    hasPermission(
      'roles.manage',
    )
    ||
    hasPermission(
      'sites.view',
    )
    ||
    hasPermission(
      'sites.manage',
    )
    ||
    hasPermission(
      'audit.view',
    )

  const canAdmin =
    hasPermission(
      'workspace.administration.view',
    )
    &&
    hasAdministrationFeature

  const canPonches =
    hasPermission(
      'workspace.ponches.view',
    )
    ||
    hasPermission(
      'ponches.manage',
    )

  /*
   * ============================================================
   * MODULES
   * ============================================================
   */

  const modules = [
    {
      label:
        'Inicio',

      path:
        '/',

      icon:
        Home,

      enabled:
        true,

      active:
        location.pathname ===
          '/',
    },

    {
      label:
        'Windows',

      path:
        '/dashboard?workspace=windows',

      icon:
        MonitorCog,

      enabled:
        canWindows,

      active:
        activeWorkspaceId ===
          'windows',
    },

    {
      label:
        'Android',

      path:
        '/dashboard?workspace=android',

      icon:
        Smartphone,

      enabled:
        canAndroid,

      active:
        activeWorkspaceId ===
          'android',
    },

    {
      label:
        'Mesa de ayuda',

      path:
        resolveHelpdeskLandingPath(
          hasPermission,
        ),

      icon:
        Headphones,

      enabled:
        canHelpdesk,

      active:
        activeWorkspaceId ===
          'helpdesk'
        ||
        location.pathname
          .startsWith(
            '/my-support',
          )
        ||
        location.pathname
          .startsWith(
            '/helpdesk',
          ),
    },

    {
      label:
        'Configuración',

      path:
        '/settings?workspace=administration',

      icon:
        Settings,

      enabled:
        canAdmin,

      active:
        activeWorkspaceId ===
          'administration',
    },
  ]
    .filter(
      item =>
        item.enabled,
    )

  return (
    <aside
      className={
        collapsed
          ? 'sidebar sidebar--collapsed'
          : 'sidebar'
      }
    >
      <div
        className="sidebar__brand"
      >
        <div
          className="sidebar__brand-mark"
        >
          T
        </div>

        {!collapsed && (
          <div
            className="sidebar__brand-text"
          >
            <strong>
              TitanMDM
            </strong>

            <span>
              Enterprise
            </span>
          </div>
        )}

        <button
          type="button"
          className="sidebar__collapse"
          onClick={
            onToggle
          }
          aria-label={
            collapsed
              ? 'Expandir menú'
              : 'Contraer menú'
          }
          title={
            collapsed
              ? 'Expandir menú'
              : 'Contraer menú'
          }
        >
          {collapsed ? (
            <ChevronRight
              size={18}
            />
          ) : (
            <ChevronLeft
              size={18}
            />
          )}
        </button>
      </div>

      <div
        className="sidebar__section-title"
      >
        {!collapsed &&
          'Módulos'}
      </div>

      <nav
        className="sidebar__nav"
        aria-label="Módulos principales"
      >
        {modules.map(
          item => {
            const Icon =
              item.icon

            return (
              <NavLink
                key={
                  item.label
                }
                to={
                  item.path
                }
                end={
                  item.path ===
                  '/'
                }
                title={
                  collapsed
                    ? item.label
                    : undefined
                }
                className={
                  item.active
                    ? 'sidebar__link sidebar__link--active'
                    : 'sidebar__link'
                }
              >
                <Icon
                  size={19}
                />

                {!collapsed && (
                  <span>
                    {item.label}
                  </span>
                )}
              </NavLink>
            )
          },
        )}

        {canPonches && (
          <NavLink
            to="/ponches"
            className={
              ({
                isActive,
              }) =>
                isActive
                  ? 'sidebar__link sidebar__link--active'
                  : 'sidebar__link'
            }
            title={
              collapsed
                ? 'Ponches'
                : undefined
            }
          >
            <Clock3
              size={19}
            />

            {!collapsed && (
              <span>
                Ponches
              </span>
            )}
          </NavLink>
        )}
      </nav>

      <div
        className="sidebar__footer"
      >
        <div
          className="sidebar__security"
        >
          <ShieldCheck
            size={18}
          />

          {!collapsed && (
            <div>
              <strong>
                Sistema protegido
              </strong>

              <span>
                RBAC + Scope activo
              </span>
            </div>
          )}
        </div>
      </div>
    </aside>
  )
}