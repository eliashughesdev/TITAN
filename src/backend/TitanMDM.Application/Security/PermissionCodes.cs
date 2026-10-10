namespace TitanMDM.Application.Security;

public static class PermissionCodes
{
    // ============================================================
    // DASHBOARD
    // ============================================================

    public static class Dashboard
    {
        public const string View =
            "dashboard.view";

        public const string GlobalView =
            "dashboard.global.view";
    }
    // ============================================================
    // WORKSPACES
    // ============================================================

    public static class Workspaces
    {
        public const string WindowsView =
            "workspace.windows.view";

        public const string AndroidView =
            "workspace.android.view";

        public const string AdministrationView =
            "workspace.administration.view";

        public const string HelpdeskView =
            "workspace.helpdesk.view";

        public const string PonchesView =
            "workspace.ponches.view";
    }

    // ============================================================
    // SITES / MULTI-LOCATION
    // ============================================================

    public static class Sites
    {
        public const string View =
            "sites.view";

        public const string Manage =
            "sites.manage";
    }

    // ============================================================
    // DEVICES
    // ============================================================

    public static class Devices
    {
        public const string View =
            "devices.view";

        public const string Create =
            "devices.create";

        public const string Update =
            "devices.update";

        public const string Delete =
            "devices.delete";

        public const string Commands =
            "devices.commands";
    }

    // ============================================================
    // ENROLLMENT
    // ============================================================

    public static class Enrollment
    {
        public const string View =
            "enrollment.view";

        public const string Manage =
            "enrollment.manage";
    }

    // ============================================================
    // POLICIES
    // ============================================================

    public static class Policies
    {
        public const string View =
            "policies.view";

        public const string Manage =
            "policies.manage";
    }

    // ============================================================
    // APPLICATIONS
    // ============================================================

    public static class Applications
    {
        public const string View =
            "apps.view";

        public const string Manage =
            "apps.manage";
    }

    // ============================================================
    // COMPLIANCE
    // ============================================================

    public static class Compliance
    {
        public const string View =
            "compliance.view";

        public const string Manage =
            "compliance.manage";
    }

    // ============================================================
    // SECURITY
    // ============================================================

    public static class Security
    {
        public const string View =
            "security.view";

        public const string Manage =
            "security.manage";
    }

    // ============================================================
    // REMOTE SUPPORT
    // ============================================================

    public static class Remote
    {
        public const string View =
            "remote.view";

        public const string Manage =
            "remote.manage";

        /// <summary>
        /// Autoriza solicitar operaciones administrativas
        /// remotas, sujeto a Site Scope, sesión conectada,
        /// lease exclusivo y ejecutor habilitado.
        ///
        /// No equivale a un token de administrador Windows.
        /// </summary>
        public const string Elevate =
            "remote.elevate";
    }


    // ============================================================
    // REPORTS
    // ============================================================

    public static class Reports
    {
        public const string View =
            "reports.view";

        public const string Export =
            "reports.export";
    }

    // ============================================================
    // USERS
    // ============================================================

    public static class Users
    {
        public const string View =
            "users.view";

        public const string Manage =
            "users.manage";
    }

    // ============================================================
    // ROLES
    // ============================================================

    public static class Roles
    {
        public const string View =
            "roles.view";

        public const string Manage =
            "roles.manage";
    }

    // ============================================================
    // AUDIT
    // ============================================================

    public static class Audit
    {
        public const string View =
            "audit.view";
    }

    // ============================================================
    // SETTINGS
    // ============================================================

    public static class Settings
    {
        public const string View =
            "settings.view";

        public const string Manage =
            "settings.manage";
    }

    // ============================================================
    // HELPDESK
    // ============================================================

    public static class Helpdesk
    {
        public const string View =
            "helpdesk.view";

        public const string Manage =
            "helpdesk.manage";

        public const string TicketsView =
            "tickets.view";

        public const string TicketsCreate =
            "tickets.create";

        public const string TicketsAssign =
            "tickets.assign";

        public const string TicketsComment =
            "tickets.comment";

        public const string TicketsClose =
            "tickets.close";
    }

    // ============================================================
    // KIOSK
    // ============================================================

    public static class Kiosk
    {
        public const string View =
            "kiosk.view";

        public const string Manage =
            "kiosk.manage";
    }

    // ============================================================
    // GEOFENCING
    // ============================================================

    public static class Geofencing
    {
        public const string View =
            "geofencing.view";

        public const string Manage =
            "geofencing.manage";
    }

    // ============================================================
    // PONCHES
    // ============================================================

    public static class Ponches
    {
        public const string Manage =
            "ponches.manage";

        public const string DashboardView =
            "ponches.dashboard.view";

        public const string RecordsView =
            "ponches.records.view";

        public const string HistoryView =
            "ponches.history.view";

        public const string RemoteCreate =
            "ponches.remote.create";

        public const string DevicesView =
            "ponches.devices.view";

        public const string DevicesManage =
            "ponches.devices.manage";

        public const string EmployeesView =
            "ponches.employees.view";

        public const string CollaboratorsView =
            "ponches.collaborators.view";

        public const string CollaboratorsManage =
            "ponches.collaborators.manage";

        public const string CollaboratorsSync =
            "ponches.collaborators.sync";

        public const string SchedulesView =
            "ponches.schedules.view";

        public const string SchedulesManage =
            "ponches.schedules.manage";

        public const string InventoryView =
            "ponches.inventory.view";

        public const string InventoryManage =
            "ponches.inventory.manage";

        public const string BulkExecute =
            "ponches.bulk.execute";

        public const string ReportsView =
            "ponches.reports.view";

        public const string Export =
            "ponches.export";

        public const string SyncView =
            "ponches.sync.view";

        public const string SyncRun =
            "ponches.sync.run";

        public const string SettingsView =
            "ponches.settings.view";

        public const string SettingsManage =
            "ponches.settings.manage";

        public const string UsersView =
            "ponches.users.view";

        public const string AdvancedReportsView =
            "ponches.advanced-reports.view";

        public const string FiorellaUse =
            "ponches.fiorella.use";
    }
}
