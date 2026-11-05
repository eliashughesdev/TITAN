import {
  lazy,
  Suspense,
  type ReactNode,
} from 'react'

import {
  Navigate,
  Route,
  Routes,
} from 'react-router-dom'

import {
  ProtectedRoute,
} from './auth/ProtectedRoute'

import {
  PermissionRoute,
} from './auth/PermissionRoute'

import {
  helpdeskPermissions,
} from './auth/helpdeskAccess'

import {
  AppLayout,
} from './components/layout/AppLayout'
import { RouteFallback } from './components/layout/RouteFallback'

import {
  AccessDeniedPage,
} from './pages/AccessDeniedPage'

const DashboardPage = lazy(() => import('./pages/DashboardPage').then(m => ({ default: m.DashboardPage })))

import {
  EntraLoginCallbackPage,
} from './pages/EntraLoginCallbackPage'

import {
  LoginPage,
} from './pages/LoginPage'

const LaunchpadPage = lazy(() => import('./pages/launchpad/LaunchpadPage').then(m => ({ default: m.LaunchpadPage })))

const DevicesPage = lazy(() => import('./pages/devices/DevicesPage').then(m => ({ default: m.DevicesPage })))

const DeviceEntryPage = lazy(() => import('./pages/devices/DeviceEntryPage').then(m => ({ default: m.DeviceEntryPage })))

const DeviceDetailPage = lazy(() => import('./pages/devices/DeviceDetailPage').then(m => ({ default: m.DeviceDetailPage })))

const WindowsControlCenterPage = lazy(() => import('./pages/devices/WindowsControlCenterPage').then(m => ({ default: m.WindowsControlCenterPage })))

const EnrollmentPage = lazy(() => import('./pages/enrollment/EnrollmentPage'))

const PoliciesPage = lazy(() => import('./pages/policies/PoliciesPage').then(m => ({ default: m.PoliciesPage })))

const PolicyEditorPage = lazy(() => import('./pages/policies/PolicyEditorPage').then(m => ({ default: m.PolicyEditorPage })))

const DeviceGroupsPage = lazy(() => import('./pages/groups/DeviceGroupsPage').then(m => ({ default: m.DeviceGroupsPage })))

const AppsPage = lazy(() => import('./pages/apps/AppsPage').then(m => ({ default: m.AppsPage })))

const SecurityPage = lazy(() => import('./pages/security/SecurityPage').then(m => ({ default: m.SecurityPage })))

const CompliancePage = lazy(() => import('./pages/compliance/CompliancePage').then(m => ({ default: m.CompliancePage })))

const KioskPage = lazy(() => import('./pages/kiosk/KioskPage').then(m => ({ default: m.KioskPage })))

const TitanLauncherPage = lazy(() => import('./pages/android-launcher/TitanLauncherPage').then(m => ({ default: m.TitanLauncherPage })))

const AndroidFleetCommandCenter = lazy(() => import('./pages/devices/components/AndroidFleetCommandCenter').then(m => ({ default: m.AndroidFleetCommandCenter })))

const GeofencingPage = lazy(() => import('./pages/geofencing/GeofencingPage').then(m => ({ default: m.GeofencingPage })))

const FleetMapPage = lazy(() => import('./pages/fleet-map/FleetMapPage').then(m => ({ default: m.FleetMapPage })))

const AutomationPage = lazy(() => import('./pages/automation/AutomationPage').then(m => ({ default: m.AutomationPage })))

const RemotePage = lazy(() => import('./pages/remote/RemotePage').then(m => ({ default: m.RemotePage })))

const ReportsPage = lazy(() => import('./pages/reports/ReportsPage').then(m => ({ default: m.ReportsPage })))

const AuditPage = lazy(() => import('./pages/audit/AuditPage').then(m => ({ default: m.AuditPage })))

const UsersPage = lazy(() => import('./pages/users/UsersPage').then(m => ({ default: m.UsersPage })))

const RolesPage = lazy(() => import('./pages/roles/RolesPage').then(m => ({ default: m.RolesPage })))

const SitesPage = lazy(() => import('./pages/sites/SitesPage').then(m => ({ default: m.SitesPage })))

const SettingsPage = lazy(() => import('./pages/settings/SettingsPage').then(m => ({ default: m.SettingsPage })))
const AssistantProviderSettingsPage = lazy(() => import('./pages/settings/AssistantProviderSettingsPage').then(m => ({ default: m.AssistantProviderSettingsPage })))

const MyHelpdeskPage = lazy(() => import('./pages/helpdesk/MyHelpdeskPage').then(m => ({ default: m.MyHelpdeskPage })))

const HelpdeskRequesterCreatePage = lazy(() => import('./pages/helpdesk/HelpdeskRequesterCreatePage').then(m => ({ default: m.HelpdeskRequesterCreatePage })))

const HelpdeskInboxPage = lazy(() => import('./pages/helpdesk/HelpdeskInboxPage').then(m => ({ default: m.HelpdeskInboxPage })))

const HelpdeskTicketPage = lazy(() => import('./pages/helpdesk/HelpdeskTicketPage').then(m => ({ default: m.HelpdeskTicketPage })))

const HelpdeskEntraSettingsPage = lazy(() => import('./pages/helpdesk/HelpdeskEntraSettingsPage').then(m => ({ default: m.HelpdeskEntraSettingsPage })))

const HelpdeskOperationsPage = lazy(() => import('./pages/helpdesk/HelpdeskOperationsPage').then(m => ({ default: m.HelpdeskOperationsPage })))

const HelpdeskSpecialtiesPage = lazy(() => import('./pages/helpdesk/HelpdeskSpecialtiesPage').then(m => ({ default: m.HelpdeskSpecialtiesPage })))

const HelpdeskReportsPage = lazy(() => import('./pages/helpdesk/HelpdeskReportsPage').then(m => ({ default: m.HelpdeskReportsPage })))

const HelpdeskCoveragePage = lazy(() => import('./pages/helpdesk/HelpdeskCoveragePage').then(m => ({ default: m.HelpdeskCoveragePage })))

const HelpdeskFollowupPage = lazy(() => import('./pages/helpdesk/HelpdeskFollowupPage').then(m => ({ default: m.HelpdeskFollowupPage })))

const HelpdeskCenterPage = lazy(() => import('./pages/helpdesk/HelpdeskCenterPage').then(m => ({ default: m.HelpdeskCenterPage })))

const MyHelpdeskActionsPanel = lazy(() => import('./pages/helpdesk/MyHelpdeskActionsPanel').then(m => ({ default: m.MyHelpdeskActionsPanel })))

const PonchesPage = lazy(() => import('./pages/ponches/PonchesPage').then(m => ({ default: m.PonchesPage })))

const InventoryPage = lazy(() => import('./pages/inventory/InventoryPage').then(m => ({ default: m.InventoryPage })))
import { INVENTORY_ACCESS_PERMISSIONS } from './auth/permissions'
const InventoryEmbedPage = lazy(() => import('./pages/inventory/InventoryEmbed').then(m => ({ default: m.InventoryEmbedPage })))

const HelpdeskAnalyticsHome = lazy(() => import('./pages/helpdesk/HelpdeskAnalyticsHome').then(m => ({ default: m.HelpdeskAnalyticsHome })))

const HelpdeskAdminHome = lazy(() => import('./pages/helpdesk/HelpdeskAdminHome').then(m => ({ default: m.HelpdeskAdminHome })))

const HelpdeskMailSettingsPage = lazy(() => import('./pages/helpdesk/HelpdeskMailSettingsPage').then(m => ({ default: m.HelpdeskMailSettingsPage })))
const HelpdeskTemplatesAdminPage = lazy(() => import('./pages/helpdesk/HelpdeskTemplatesAdminPage').then(m => ({ default: m.HelpdeskTemplatesAdminPage })))

const RemoteSupportDirectoryPage = lazy(() => import('./pages/remote/RemoteSupportDirectoryPage').then(m => ({ default: m.RemoteSupportDirectoryPage })))
const HelpdeskExperience = lazy(() => import('./pages/helpdesk/HelpdeskExperience').then(m => ({ default: m.HelpdeskExperience })))


interface ApplicationRoute {
  path: string
  page: ReactNode
  permissions: string[]
  allPermissions?: string[]
  helpdeskConsole?: boolean
}

const applicationRoutes:
  ApplicationRoute[] = [
    {
      path:
        'helpdesk',

      page:
        <HelpdeskInboxPage />,

      permissions: [
        helpdeskPermissions
          .agentAccess,

        helpdeskPermissions
          .inboxMyWork,

        helpdeskPermissions
          .inboxUnassigned,

        helpdeskPermissions
          .inboxAll,

        'helpdesk.view',

        'tickets.view',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/tickets/:ticketId',

      page:
        <HelpdeskTicketPage />,

      permissions: [
        helpdeskPermissions
          .ticketDetails,

        helpdeskPermissions
          .agentAccess,

        'tickets.view',
      ],

      helpdeskConsole:
        true,
    },
    {
  path:
    'helpdesk/templates',

  page:
    <HelpdeskTemplatesAdminPage />,

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

  helpdeskConsole:
    true,
},

    {
      path:
        'helpdesk/seguimiento',

      page:
        <HelpdeskFollowupPage />,

      permissions: [
        helpdeskPermissions
          .slaView,

        'tickets.comment',

        'tickets.assign',

        'helpdesk.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/centro/kpis',

      page:
        <HelpdeskCenterPage />,

      permissions: [
        helpdeskPermissions
          .kpiView,

        'helpdesk.view',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/centro/alertas',

      page:
        <HelpdeskCenterPage />,

      permissions: [
        helpdeskPermissions
          .automationView,

        'helpdesk.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/centro/configuracion',

      page:
        <HelpdeskCenterPage />,

      permissions: [
        helpdeskPermissions
          .adminAccess,

        'helpdesk.manage',

        'settings.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/centro/preparacion',

      page:
        <HelpdeskCenterPage />,

      permissions: [
        helpdeskPermissions
          .adminAccess,

        'helpdesk.manage',

        'settings.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/operations',

      page:
        <HelpdeskOperationsPage />,

      permissions: [
        helpdeskPermissions
          .sitesView,

        helpdeskPermissions
          .techniciansView,

        helpdeskPermissions
          .adminAccess,

        'helpdesk.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/especialidades',

      page:
        <HelpdeskSpecialtiesPage />,

      permissions: [
        helpdeskPermissions
          .groupsView,

        helpdeskPermissions
          .categoriesView,

        helpdeskPermissions
          .schedulesView,

        helpdeskPermissions
          .adminAccess,

        'helpdesk.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/cobertura',

      page:
        <HelpdeskCoveragePage />,

      permissions: [
        helpdeskPermissions
          .sitesView,

        helpdeskPermissions
          .groupsView,

        'helpdesk.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'helpdesk/reportes',

      page:
        <HelpdeskReportsPage />,

      permissions: [
        helpdeskPermissions
          .reportsView,

        'tickets.view',
      ],

      helpdeskConsole:
        true,
    },{
  path:
    'helpdesk/analytics',

  page:
    <HelpdeskAnalyticsHome />,

  permissions: [
    helpdeskPermissions.slaView,
    helpdeskPermissions.kpiView,
    helpdeskPermissions.analyticsView,
    helpdeskPermissions.reportsView,
    helpdeskPermissions.adminAccess,
  ],

  helpdeskConsole:
    true,
},

{
  path:
    'helpdesk/admin',

  page:
    <HelpdeskAdminHome />,

  permissions: [
    helpdeskPermissions.adminAccess,
  ],

  helpdeskConsole:
    true,
},
{
  path:
    'helpdesk/mail',

  page:
    <HelpdeskMailSettingsPage />,

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

  helpdeskConsole:
    true,
},

    {
      path:
        'helpdesk/entra',

      page:
        <HelpdeskEntraSettingsPage />,

      permissions: [
        helpdeskPermissions
          .adminAccess,

        'helpdesk.manage',

        'settings.manage',
      ],

      helpdeskConsole:
        true,
    },

    {
      path:
        'dashboard',

      page:
        <DashboardPage />,

      permissions: [
        'dashboard.view',
      ],
    },

    {
      path:
        'ponches',

      page:
        <PonchesPage />,

      permissions: [
        'workspace.ponches.view',
        'ponches.manage',
      ],
    },

    {
      path:
        'inventory',

      page:
        <InventoryPage />,

      // Solo permisos de inventario (configurables en Roles).
      permissions: [
        ...INVENTORY_ACCESS_PERMISSIONS,
      ],
    },

    {
      path:
        'devices',

      page:
        <DevicesPage />,

      permissions: [
        'devices.view',
      ],
    },

    {
      path:
        'devices/:deviceId',

      page:
        <DeviceEntryPage />,

      permissions: [
        'devices.view',
      ],
    },

    {
      path:
        'devices/:deviceId/android',

      page:
        <DeviceDetailPage />,

      permissions: [
        'devices.view',
      ],
    },

    {
      path:
        'devices/:deviceId/control-center',

      page:
        <WindowsControlCenterPage />,

      permissions: [
        'devices.commands',
      ],
    },

    {
      path:
        'enrollment',

      page:
        <EnrollmentPage />,

      permissions: [
        'enrollment.view',
      ],
    },

    {
      path:
        'policies',

      page:
        <PoliciesPage />,

      permissions: [
        'policies.view',
      ],
    },

    {
      path:
        'policies/new',

      page:
        <PolicyEditorPage />,

      permissions: [
        'policies.manage',
      ],
    },

    {
      path:
        'policies/:policyId',

      page:
        <PolicyEditorPage />,

      permissions: [
        'policies.view',
        'policies.manage',
      ],
    },

    {
      path:
        'groups',

      page:
        <DeviceGroupsPage />,

      permissions: [
        'devices.view',
      ],
    },

    {
      path:
        'apps',

      page:
        <AppsPage />,

      permissions: [
        'apps.view',
      ],
    },

    {
      path:
        'security',

      page:
        <SecurityPage />,

      permissions: [
        'security.view',
      ],
    },

    {
      path:
        'compliance',

      page:
        <CompliancePage />,

      permissions: [
        'compliance.view',
      ],
    },

    {
      path:
        'kiosk',

      page:
        <KioskPage />,

      permissions: [
        'kiosk.view',
      ],
    },

    {
      path:
        'android/titan-launcher',

      page:
        <TitanLauncherPage />,

      permissions: [
        'android.enterprise.view',
      ],

      allPermissions: [
        'policies.view',
      ],
    },

    {
      path:
        'android/control-center',

      page:
        <AndroidFleetCommandCenter />,

      permissions: [
        'android.enterprise.view',
      ],
    },

    {
      path:
        'geofencing',

      page:
        <GeofencingPage />,

      permissions: [
        'geofencing.view',
      ],
    },

    {
      path:
        'fleet-map',

      page:
        <FleetMapPage />,

      permissions: [
        'geofencing.view',
      ],
    },

    {
      path:
        'automation',

      page:
        <AutomationPage />,

      permissions: [
        'automation.view',
        'automation.manage',
        'automation.admin',
      ],
    },

    {
      path: 'remote',
      page: <RemoteSupportDirectoryPage />,
      permissions: ['remote.view'],
      allPermissions: ['devices.view'],
    },
    {
      path: 'remote/viewer',
      page: <RemotePage />,
      permissions: ['remote.view'],
    },

    {
      path:
        'reports',

      page:
        <ReportsPage />,

      permissions: [
        'reports.view',
      ],
    },

    {
      path:
        'audit',

      page:
        <AuditPage />,

      permissions: [
        'audit.view',
      ],
    },

    {
      path:
        'users',

      page:
        <UsersPage />,

      permissions: [
        'users.view',
      ],
    },

    {
      path:
        'roles',

      page:
        <RolesPage />,

      permissions: [
        'roles.view',
      ],
    },

    {
      path:
        'sites',

      page:
        <SitesPage />,

      permissions: [
        'sites.view',
      ],
    },

    {
      path:
        'settings/ai-providers',

      page:
        <AssistantProviderSettingsPage />,

      permissions: [
        'settings.view',
      ],
    },

    {
      path:
        'settings',

      page:
        <SettingsPage />,

      permissions: [
        'settings.view',
      ],
    },
  ]

/*
 * Las paginas se cargan bajo demanda: el bundle inicial solo trae
 * login, layout y autenticacion. AppLayout tiene su propio Suspense
 * para que el menu no desaparezca al navegar.
 */
function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
    <Routes>
      <Route
        path="/login"
        element={
          <LoginPage />
        }
      />

      <Route
        path="/login/entra"
        element={
          <EntraLoginCallbackPage />
        }
      />

      <Route
        path="/inventory/embed"
        element={
          <PermissionRoute
            anyOf={[
              'inventory.manage',
              'inventory.assets.manage',
            ]}
          >
            <InventoryEmbedPage />
          </PermissionRoute>
        }
      />

      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route
          index
          element={
            <LaunchpadPage />
          }
        />

        <Route
          path="forbidden"
          element={
            <AccessDeniedPage />
          }
        />

        <Route
          path="my-support"
          element={
            <PermissionRoute
              anyOf={[
                helpdeskPermissions
                  .portalAccess,

                helpdeskPermissions
                  .requestOwnView,

                'tickets.create',
              ]}
            >
              <MyHelpdeskPage />
            </PermissionRoute>
          }
        />

        <Route
          path="my-support/new"
          element={
            <PermissionRoute
              anyOf={[
                helpdeskPermissions
                  .requestCreate,

                'tickets.create',
              ]}
            >
              <HelpdeskRequesterCreatePage />
            </PermissionRoute>
          }
        />

        <Route
          path="my-support/:ticketId"
          element={
            <PermissionRoute
              anyOf={[
                helpdeskPermissions
                  .requestOwnView,

                'tickets.create',
              ]}
            >
              <>
                <MyHelpdeskPage />

                <MyHelpdeskActionsPanel />
              </>
            </PermissionRoute>
          }
        />

        {applicationRoutes.map(
          route => (
            <Route
              key={
                route.path
              }
              path={
                route.path
              }
              element={
                <PermissionRoute
                  anyOf={
                    route.permissions
                  }
                  allOf={
                    route.allPermissions
                  }
                  helpdeskConsole={
                    route.helpdeskConsole
                  }
                >
                 {route.helpdeskConsole ? (
                  <HelpdeskExperience variant="console">
                    {route.page}
                  </HelpdeskExperience>
                ) : (
                  route.page
                )}
                </PermissionRoute>
              }
            />
          ),
        )}
      </Route>

      <Route
        path="*"
        element={
          <Navigate
            to="/"
            replace
          />
        }
      />
    </Routes>
    </Suspense>
  )
}

export default App
