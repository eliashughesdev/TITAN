/*
 * Catálogo de permisos del frontend TitanMDM.
 * Los valores deben coincidir con TitanMdmSeeder en el backend.
 */

export const Permissions = {
  dashboard: {
    view: 'dashboard.view',
    globalView: 'dashboard.global.view',
  },

  workspace: {
    windows: {
      view: 'workspace.windows.view',
    },
    android: {
      view: 'workspace.android.view',
    },
    administration: {
      view: 'workspace.administration.view',
    },
    helpdesk: {
      view: 'workspace.helpdesk.view',
    },
  },

  devices: {
    view: 'devices.view',
    create: 'devices.create',
    update: 'devices.update',
    delete: 'devices.delete',
    commands: 'devices.commands',
  },

  enrollment: {
    view: 'enrollment.view',
    manage: 'enrollment.manage',
  },

  policies: {
    view: 'policies.view',
    manage: 'policies.manage',
  },

  applications: {
    view: 'apps.view',
    manage: 'apps.manage',
  },

  compliance: {
    view: 'compliance.view',
    manage: 'compliance.manage',
  },

  security: {
    view: 'security.view',
    manage: 'security.manage',
  },

  kiosk: {
    view: 'kiosk.view',
    manage: 'kiosk.manage',
  },

  geofencing: {
    view: 'geofencing.view',
    manage: 'geofencing.manage',
  },

  remote: {
    view: 'remote.view',
    manage: 'remote.manage',
  },

  reports: {
    view: 'reports.view',
    export: 'reports.export',
  },

  users: {
    view: 'users.view',
    manage: 'users.manage',
  },

  roles: {
    view: 'roles.view',
    manage: 'roles.manage',
  },

  audit: {
    view: 'audit.view',
  },

  helpdesk: {
    view: 'helpdesk.view',
    manage: 'helpdesk.manage',
    tickets: {
      view: 'tickets.view',
      create: 'tickets.create',
      assign: 'tickets.assign',
      comment: 'tickets.comment',
      close: 'tickets.close',
    },
  },

  settings: {
    view: 'settings.view',
    manage: 'settings.manage',
  },
} as const

export type PermissionCatalog = typeof Permissions