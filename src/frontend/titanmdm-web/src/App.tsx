import type {
  ReactNode,
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

import {
  AccessDeniedPage,
} from './pages/AccessDeniedPage'

import {
  DashboardPage,
} from './pages/DashboardPage'

import {
  EntraLoginCallbackPage,
} from './pages/EntraLoginCallbackPage'

import {
  LoginPage,
} from './pages/LoginPage'

import {
  LaunchpadPage,
} from './pages/launchpad/LaunchpadPage'

import {
  DevicesPage,
} from './pages/devices/DevicesPage'

import {
  DeviceEntryPage,
} from './pages/devices/DeviceEntryPage'

import {
  DeviceDetailPage,
} from './pages/devices/DeviceDetailPage'

import {
  WindowsControlCenterPage,
} from './pages/devices/WindowsControlCenterPage'

import EnrollmentPage
  from './pages/enrollment/EnrollmentPage'

import {
  PoliciesPage,
} from './pages/policies/PoliciesPage'

import {
  PolicyEditorPage,
} from './pages/policies/PolicyEditorPage'

import {
  DeviceGroupsPage,
} from './pages/groups/DeviceGroupsPage'

import {
  AppsPage,
} from './pages/apps/AppsPage'

import {
  SecurityPage,
} from './pages/security/SecurityPage'

import {
  CompliancePage,
} from './pages/compliance/CompliancePage'

import {
  KioskPage,
} from './pages/kiosk/KioskPage'

import {
  GeofencingPage,
} from './pages/geofencing/GeofencingPage'

import {
  AutomationPage,
} from './pages/automation/AutomationPage'

import {
  RemotePage,
} from './pages/remote/RemotePage'

import {
  ReportsPage,
} from './pages/reports/ReportsPage'

import {
  AuditPage,
} from './pages/audit/AuditPage'

import {
  UsersPage,
} from './pages/users/UsersPage'

import {
  RolesPage,
} from './pages/roles/RolesPage'

import {
  SitesPage,
} from './pages/sites/SitesPage'

import {
  SettingsPage,
} from './pages/settings/SettingsPage'

import {
  MyHelpdeskPage,
} from './pages/helpdesk/MyHelpdeskPage'

import {
  HelpdeskRequesterCreatePage,
} from './pages/helpdesk/HelpdeskRequesterCreatePage'

import {
  HelpdeskInboxPage,
} from './pages/helpdesk/HelpdeskInboxPage'

import {
  HelpdeskTicketPage,
} from './pages/helpdesk/HelpdeskTicketPage'

import {
  HelpdeskEntraSettingsPage,
} from './pages/helpdesk/HelpdeskEntraSettingsPage'

import {
  HelpdeskOperationsPage,
} from './pages/helpdesk/HelpdeskOperationsPage'

import {
  HelpdeskSpecialtiesPage,
} from './pages/helpdesk/HelpdeskSpecialtiesPage'

import {
  HelpdeskReportsPage,
} from './pages/helpdesk/HelpdeskReportsPage'

import {
  HelpdeskCoveragePage,
} from './pages/helpdesk/HelpdeskCoveragePage'

import {
  HelpdeskFollowupPage,
} from './pages/helpdesk/HelpdeskFollowupPage'

import {
  HelpdeskCenterPage,
} from './pages/helpdesk/HelpdeskCenterPage'

import {
  MyHelpdeskActionsPanel,
} from './pages/helpdesk/MyHelpdeskActionsPanel'

import {
  PonchesPage,
} from './pages/ponches/PonchesPage'

import {
  HelpdeskAnalyticsHome,
} from './pages/helpdesk/HelpdeskAnalyticsHome'

import {
  HelpdeskAdminHome,
} from './pages/helpdesk/HelpdeskAdminHome'

import {
  HelpdeskMailSettingsPage,
} from './pages/helpdesk/HelpdeskMailSettingsPage'
import {
  HelpdeskTemplatesAdminPage,
} from './pages/helpdesk/HelpdeskTemplatesAdminPage'

interface ApplicationRoute {
  path: string
  page: ReactNode
  permissions: string[]
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
        'geofencing',

      page:
        <GeofencingPage />,

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
        'devices.commands',
      ],
    },

    {
      path:
        'remote',

      page:
        <RemotePage />,

      permissions: [
        'remote.view',
      ],
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
        'settings',

      page:
        <SettingsPage />,

      permissions: [
        'settings.view',
      ],
    },
  ]

function App() {
  return (
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
                  helpdeskConsole={
                    route.helpdeskConsole
                  }
                >
                  {route.page}
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
  )
}

export default App