export const helpdeskPermissions = {
  portalAccess: 'helpdesk.portal.access',

  agentAccess: 'helpdesk.agent.access',

  adminAccess: 'helpdesk.admin.access',

  requestCreate: 'helpdesk.request.create',

  requestOwnView: 'helpdesk.request.own.view',

  requestOwnComment: 'helpdesk.request.own.comment',

  requestOwnReopen: 'helpdesk.request.own.reopen',

  requestOwnConfirm: 'helpdesk.request.own.confirm',

  inboxMyWork: 'helpdesk.inbox.my-work',

  inboxUnassigned: 'helpdesk.inbox.unassigned',

  inboxAll: 'helpdesk.inbox.all',

  inboxEscalated: 'helpdesk.inbox.escalated',

  kanbanView: 'helpdesk.kanban.view',

  ticketDetails: 'helpdesk.ticket.details.view',

  ticketComment: 'helpdesk.ticket.comment',

  ticketInternalNote: 'helpdesk.ticket.internal-note',

  ticketTake: 'helpdesk.ticket.take',

  ticketAssign: 'helpdesk.ticket.assign',

  ticketTransfer: 'helpdesk.ticket.transfer',

  ticketTransition: 'helpdesk.ticket.transition',

  ticketResolve: 'helpdesk.ticket.resolve',

  ticketClose: 'helpdesk.ticket.close',

  ticketReopen: 'helpdesk.ticket.reopen',

  slaView: 'helpdesk.sla.view',

  slaManage: 'helpdesk.sla.manage',

  kpiView: 'helpdesk.kpi.view',

  analyticsView: 'helpdesk.analytics.view',

  reportsView: 'helpdesk.reports.view',

  reportsExport: 'helpdesk.reports.export',

  sitesView: 'helpdesk.sites.view',

  sitesManage: 'helpdesk.sites.manage',

  groupsView: 'helpdesk.groups.view',

  groupsManage: 'helpdesk.groups.manage',

  techniciansView: 'helpdesk.technicians.view',

  techniciansManage: 'helpdesk.technicians.manage',

  schedulesView: 'helpdesk.schedules.view',

  schedulesManage: 'helpdesk.schedules.manage',

  categoriesView: 'helpdesk.categories.view',

  categoriesManage: 'helpdesk.categories.manage',

  templatesView: 'helpdesk.templates.view',

  templatesManage: 'helpdesk.templates.manage',

  workflowsView: 'helpdesk.workflows.view',

  workflowsManage: 'helpdesk.workflows.manage',

  automationView: 'helpdesk.automation.view',

  automationManage: 'helpdesk.automation.manage',

  mailView: 'helpdesk.mail.view',

  mailManage: 'helpdesk.mail.manage',

  aiUse: 'helpdesk.ai.use',

  aiView: 'helpdesk.ai.view',

  aiManage: 'helpdesk.ai.manage',
} as const

export const legacyHelpdeskPermissions = {
  view: 'helpdesk.view',

  manage: 'helpdesk.manage',

  ticketView: 'tickets.view',

  ticketCreate: 'tickets.create',

  ticketAssign: 'tickets.assign',

  ticketComment: 'tickets.comment',

  ticketClose: 'tickets.close',
} as const

type PermissionEvaluator =
  (
    permission: string,
  ) => boolean

/*
 * ============================================================
 * PORTAL DEL COLABORADOR
 * ============================================================
 */

export function canUseHelpdeskPortal(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.portalAccess,
    )
    ||
    hasPermission(
      helpdeskPermissions.requestCreate,
    )
    ||
    hasPermission(
      helpdeskPermissions.requestOwnView,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions.ticketCreate,
    )
  )
}

export function canCreateHelpdeskRequest(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.requestCreate,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions.ticketCreate,
    )
  )
}

export function canViewOwnHelpdeskRequests(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.requestOwnView,
    )
    ||
    hasPermission(
      helpdeskPermissions.portalAccess,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions.ticketCreate,
    )
  )
}

/*
 * ============================================================
 * CONSOLA TIC
 *
 * IMPORTANTE:
 * tener tickets.view o helpdesk.view NO convierte a alguien
 * automáticamente en agente.
 *
 * La puerta principal es helpdesk.agent.access.
 * Mantenemos helpdesk.manage como compatibilidad administrativa.
 * ============================================================
 */

export function canUseHelpdeskConsole(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.agentAccess,
    )
    ||
    hasPermission(
      helpdeskPermissions.adminAccess,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions.manage,
    )
  )
}

/*
 * ============================================================
 * ANALÍTICA
 * ============================================================
 */

export function canUseHelpdeskAnalytics(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.kpiView,
    )
    ||
    hasPermission(
      helpdeskPermissions.analyticsView,
    )
    ||
    hasPermission(
      helpdeskPermissions.reportsView,
    )
    ||
    hasPermission(
      helpdeskPermissions.slaView,
    )
    ||
    hasPermission(
      helpdeskPermissions.adminAccess,
    )
  )
}

/*
 * ============================================================
 * ADMINISTRACIÓN
 * ============================================================
 */

export function canManageHelpdesk(
  hasPermission:
    PermissionEvaluator,
): boolean {
  return (
    hasPermission(
      helpdeskPermissions.adminAccess,
    )
    ||
    hasPermission(
      legacyHelpdeskPermissions.manage,
    )
    ||
    hasPermission(
      'settings.manage',
    )
  )
}

/*
 * ============================================================
 * LANDING
 * ============================================================
 */

export function resolveHelpdeskLandingPath(
  hasPermission:
    PermissionEvaluator,
): string {
  if (
    canUseHelpdeskConsole(
      hasPermission,
    )
  ) {
    return (
      '/helpdesk' +
      '?workspace=helpdesk'
    )
  }

  return (
    '/my-support' +
    '?workspace=helpdesk'
  )
}