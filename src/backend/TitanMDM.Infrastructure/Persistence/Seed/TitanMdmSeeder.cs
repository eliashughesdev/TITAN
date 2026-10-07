using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

namespace TitanMDM.Infrastructure.Persistence.Seed;

public sealed class TitanMdmSeeder
{
    private const string DefaultOrganizationCode =
        "TITANMDM";

    private const string DefaultOrganizationName =
        "TitanMDM";

    private const string SuperAdminRoleName =
        "SuperAdmin";

    private const string BootstrapAdminEmail =
        "superadmin@titanmdm.local";

    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IPasswordHasher<User>
        _passwordHasher;

    public TitanMdmSeeder(
        TitanMdmDbContext dbContext,
        IPasswordHasher<User> passwordHasher)
    {
        _dbContext =
            dbContext
            ??
            throw new ArgumentNullException(
                nameof(dbContext));

        _passwordHasher =
            passwordHasher
            ??
            throw new ArgumentNullException(
                nameof(passwordHasher));
    }

    // ============================================================
    // PERMISSION DEFINITION
    // ============================================================

    private sealed record Definition(
        string Code,
        string Name,
        string Module,
        string Description);

    private static readonly Definition[]
        Definitions =
        [
            // =====================================================
            // DASHBOARD / WORKSPACES
            // =====================================================

            new(
                "dashboard.view",
                "Ver dashboard",
                "Dashboard",
                "Permite visualizar el dashboard principal."),

            new(
                "dashboard.global.view",
                "Ver dashboard general",
                "Dashboard",
                "Permite visualizar métricas combinadas de todas las plataformas."),

            new(
                "workspace.windows.view",
                "Acceder a Windows Management",
                "Workspaces",
                "Permite acceder al espacio de trabajo Windows."),

            new(
                "workspace.android.view",
                "Acceder a Android Management",
                "Workspaces",
                "Permite acceder al espacio de trabajo Android."),

            new(
                "workspace.administration.view",
                "Acceder a Administración",
                "Workspaces",
                "Permite acceder al espacio de administración de TitanMDM."),

            new(
                "workspace.helpdesk.view",
                "Acceder a Mesa de Ayuda",
                "Workspaces",
                "Permite acceder al espacio de trabajo de Mesa de Ayuda."),

            // =====================================================
            // DEVICES
            // =====================================================

            new(
                "devices.view",
                "Ver dispositivos",
                "Devices",
                "Permite consultar los dispositivos administrados."),

            new(
                "devices.create",
                "Registrar dispositivos",
                "Devices",
                "Permite registrar y preparar nuevos dispositivos."),

            new(
                "devices.update",
                "Modificar dispositivos",
                "Devices",
                "Permite modificar información administrativa de dispositivos."),

            new(
                "devices.delete",
                "Eliminar dispositivos",
                "Devices",
                "Permite retirar registros de dispositivos."),

            new(
                "devices.commands",
                "Ejecutar comandos",
                "Devices",
                "Permite enviar comandos remotos a dispositivos."),

            // =====================================================
            // ENROLLMENT
            // =====================================================

            new(
                "enrollment.view",
                "Ver enrolamiento",
                "Enrollment",
                "Permite consultar métodos y procesos de enrolamiento."),

            new(
                "enrollment.manage",
                "Administrar enrolamiento",
                "Enrollment",
                "Permite crear y administrar procesos de enrolamiento."),

            // =====================================================
            // POLICIES
            // =====================================================

            new(
                "policies.view",
                "Ver políticas",
                "Policies",
                "Permite consultar políticas de administración."),

            new(
                "policies.manage",
                "Administrar políticas",
                "Policies",
                "Permite crear, modificar, asignar y retirar políticas."),

            // =====================================================
            // APPLICATIONS
            // =====================================================

            new(
                "apps.view",
                "Ver aplicaciones",
                "Applications",
                "Permite consultar el catálogo de aplicaciones."),

            new(
                "apps.manage",
                "Administrar aplicaciones",
                "Applications",
                "Permite cargar, distribuir, actualizar y retirar aplicaciones."),

            // =====================================================
            // COMPLIANCE
            // =====================================================

            new(
                "compliance.view",
                "Ver cumplimiento",
                "Compliance",
                "Permite consultar el estado de cumplimiento."),

            new(
                "compliance.manage",
                "Administrar cumplimiento",
                "Compliance",
                "Permite configurar reglas y acciones de cumplimiento."),

            // =====================================================
            // SECURITY
            // =====================================================

            new(
                "security.view",
                "Ver seguridad",
                "Security",
                "Permite consultar eventos y estado de seguridad."),

            new(
                "security.manage",
                "Administrar seguridad",
                "Security",
                "Permite administrar controles y acciones de seguridad."),

            // =====================================================
            // KIOSK
            // =====================================================

            new(
                "kiosk.view",
                "Ver modo kiosco",
                "Kiosk",
                "Permite consultar configuraciones de modo kiosco."),

            new(
                "kiosk.manage",
                "Administrar modo kiosco",
                "Kiosk",
                "Permite crear y aplicar configuraciones de modo kiosco."),

            // =====================================================
            // GEOFENCING
            // =====================================================

            new(
                "geofencing.view",
                "Ver geocercas",
                "Geofencing",
                "Permite consultar geocercas configuradas."),

            new(
                "geofencing.manage",
                "Administrar geocercas",
                "Geofencing",
                "Permite crear, modificar y asignar geocercas."),

            // =====================================================
            // REMOTE SUPPORT
            // =====================================================

            new(
                "remote.view",
                "Ver soporte remoto",
                "RemoteSupport",
                "Permite consultar sesiones de soporte remoto."),

            new(
                "remote.manage",
                "Administrar soporte remoto",
                "RemoteSupport",
                "Permite iniciar y administrar sesiones remotas."),

            // =====================================================
            // REPORTS
            // =====================================================

            new(
                "reports.view",
                "Ver reportes",
                "Reports",
                "Permite consultar reportes."),

            new(
                "reports.export",
                "Exportar reportes",
                "Reports",
                "Permite exportar información y reportes."),

            // =====================================================
            // USERS
            // =====================================================

            new(
                "users.view",
                "Ver usuarios",
                "Users",
                "Permite consultar usuarios administrativos."),

            new(
                "users.manage",
                "Administrar usuarios",
                "Users",
                "Permite crear, modificar, activar y desactivar usuarios."),

            // =====================================================
            // ROLES
            // =====================================================

            new(
                "roles.view",
                "Ver roles",
                "Roles",
                "Permite consultar roles y permisos."),

            new(
                "roles.manage",
                "Administrar roles",
                "Roles",
                "Permite crear roles y asignar permisos."),

            // =====================================================
            // AUDIT
            // =====================================================

            new(
                "audit.view",
                "Ver auditoría",
                "Audit",
                "Permite consultar el registro de auditoría."),

            // =====================================================
            // SETTINGS
            // =====================================================

            new(
                "settings.view",
                "Ver configuración",
                "Settings",
                "Permite consultar la configuración de TitanMDM."),

            new(
                "settings.manage",
                "Administrar configuración",
                "Settings",
                "Permite modificar la configuración global de TitanMDM."),

              // =====================================================
            // HELPDESK - WORKSPACE / ACCESS
            // =====================================================

            new(
                "helpdesk.portal.access",
                "Acceder al portal de Mesa de Ayuda",
                "Helpdesk",
                "Permite acceder al portal de autoservicio para colaboradores."),

            new(
                "helpdesk.agent.access",
                "Acceder a consola TIC",
                "Helpdesk",
                "Permite acceder a la consola operativa de técnicos de Mesa de Ayuda."),

            new(
                "helpdesk.admin.access",
                "Acceder a administración Helpdesk",
                "Helpdesk",
                "Permite acceder a configuración avanzada de Mesa de Ayuda."),

            // =====================================================
            // HELPDESK - REQUESTER / COLLABORATOR
            // =====================================================

            new(
                "helpdesk.request.create",
                "Crear solicitudes propias",
                "Helpdesk",
                "Permite crear tickets desde el portal de autoservicio."),

            new(
                "helpdesk.request.own.view",
                "Ver solicitudes propias",
                "Helpdesk",
                "Permite consultar únicamente los tickets creados por el usuario."),

            new(
                "helpdesk.request.own.comment",
                "Responder solicitudes propias",
                "Helpdesk",
                "Permite responder tickets propios mientras permanezcan activos."),

            new(
                "helpdesk.request.own.reopen",
                "Reabrir solicitudes propias",
                "Helpdesk",
                "Permite reabrir tickets propios dentro del período autorizado."),

            new(
                "helpdesk.request.own.confirm",
                "Confirmar resolución propia",
                "Helpdesk",
                "Permite confirmar una resolución y cerrar un ticket propio."),

            // =====================================================
            // HELPDESK - AGENT CONSOLE
            // =====================================================

            new(
                "helpdesk.inbox.my-work",
                "Ver Mi trabajo",
                "Helpdesk",
                "Permite visualizar los tickets asignados al técnico actual."),

            new(
                "helpdesk.inbox.unassigned",
                "Ver tickets sin asignar",
                "Helpdesk",
                "Permite visualizar tickets pendientes de asignación."),

            new(
                "helpdesk.inbox.all",
                "Ver todos los tickets",
                "Helpdesk",
                "Permite consultar los tickets dentro del alcance autorizado."),

            new(
                "helpdesk.inbox.escalated",
                "Ver tickets escalados",
                "Helpdesk",
                "Permite consultar tickets escalados por SLA o reglas operativas."),

            new(
                "helpdesk.kanban.view",
                "Ver Kanban de Helpdesk",
                "Helpdesk",
                "Permite utilizar la vista Kanban de operación."),

            new(
                "helpdesk.ticket.details.view",
                "Ver detalle de tickets",
                "Helpdesk",
                "Permite abrir el detalle operativo de un ticket."),

            new(
                "helpdesk.ticket.comment",
                "Responder tickets",
                "Helpdesk",
                "Permite publicar respuestas visibles para el solicitante."),

            new(
                "helpdesk.ticket.internal-note",
                "Crear notas internas",
                "Helpdesk",
                "Permite registrar notas visibles únicamente para personal TIC."),

            new(
                "helpdesk.ticket.take",
                "Tomar tickets",
                "Helpdesk",
                "Permite asignarse personalmente un ticket."),

            new(
                "helpdesk.ticket.assign",
                "Asignar tickets",
                "Helpdesk",
                "Permite asignar tickets a otros técnicos autorizados."),

            new(
                "helpdesk.ticket.transfer",
                "Transferir tickets",
                "Helpdesk",
                "Permite transferir un ticket entre grupos o técnicos."),

            new(
                "helpdesk.ticket.transition",
                "Cambiar estado del ticket",
                "Helpdesk",
                "Permite ejecutar transiciones permitidas en el ciclo de vida."),

            new(
                "helpdesk.ticket.resolve",
                "Resolver tickets",
                "Helpdesk",
                "Permite marcar un ticket como resuelto."),

            new(
                "helpdesk.ticket.close",
                "Cerrar tickets",
                "Helpdesk",
                "Permite cerrar definitivamente tickets."),

            new(
                "helpdesk.ticket.reopen",
                "Reabrir tickets",
                "Helpdesk",
                "Permite reabrir tickets resueltos o cerrados."),

            // =====================================================
            // HELPDESK - SLA
            // =====================================================

            new(
                "helpdesk.sla.view",
                "Ver seguimiento SLA",
                "Helpdesk",
                "Permite consultar vencimientos, riesgos y seguimiento SLA."),

            new(
                "helpdesk.sla.manage",
                "Administrar SLA",
                "Helpdesk",
                "Permite crear y modificar reglas y acuerdos de servicio."),

            // =====================================================
            // HELPDESK - KPI / ANALYTICS
            // =====================================================

            new(
                "helpdesk.kpi.view",
                "Ver KPI Helpdesk",
                "Helpdesk",
                "Permite visualizar indicadores operativos de Mesa de Ayuda."),

            new(
                "helpdesk.analytics.view",
                "Ver gráficos Helpdesk",
                "Helpdesk",
                "Permite visualizar gráficos y tendencias operativas."),

            new(
                "helpdesk.reports.view",
                "Ver reportes Helpdesk",
                "Helpdesk",
                "Permite consultar reportes de Mesa de Ayuda."),

            new(
                "helpdesk.reports.export",
                "Exportar reportes Helpdesk",
                "Helpdesk",
                "Permite exportar datos y reportes de Mesa de Ayuda."),

            // =====================================================
            // HELPDESK - SITES / COVERAGE
            // =====================================================

            new(
                "helpdesk.sites.view",
                "Ver localidades en Helpdesk",
                "Helpdesk",
                "Permite utilizar Sites y localidades dentro de Mesa de Ayuda."),

            new(
                "helpdesk.sites.manage",
                "Administrar localidades en Helpdesk",
                "Helpdesk",
                "Permite administrar la integración de Sites con Mesa de Ayuda."),

            new(
                "helpdesk.groups.view",
                "Ver grupos de trabajo",
                "Helpdesk",
                "Permite consultar grupos y cobertura operativa."),

            new(
                "helpdesk.groups.manage",
                "Administrar grupos de trabajo",
                "Helpdesk",
                "Permite crear y modificar grupos de trabajo."),

            new(
                "helpdesk.technicians.view",
                "Ver técnicos",
                "Helpdesk",
                "Permite consultar técnicos, disponibilidad y capacidad."),

            new(
                "helpdesk.technicians.manage",
                "Administrar técnicos",
                "Helpdesk",
                "Permite configurar técnicos, cobertura y disponibilidad."),

            new(
                "helpdesk.schedules.view",
                "Ver turnos Helpdesk",
                "Helpdesk",
                "Permite consultar turnos y horarios operativos."),

            new(
                "helpdesk.schedules.manage",
                "Administrar turnos Helpdesk",
                "Helpdesk",
                "Permite configurar turnos y horarios de técnicos."),

            // =====================================================
            // HELPDESK - SERVICE CATALOG
            // =====================================================

            new(
                "helpdesk.categories.view",
                "Ver categorías",
                "Helpdesk",
                "Permite consultar categorías y especialidades."),

            new(
                "helpdesk.categories.manage",
                "Administrar categorías",
                "Helpdesk",
                "Permite crear y modificar categorías y especialidades."),

            new(
                "helpdesk.templates.view",
                "Ver plantillas",
                "Helpdesk",
                "Permite utilizar plantillas disponibles."),

            new(
                "helpdesk.templates.manage",
                "Administrar plantillas",
                "Helpdesk",
                "Permite crear, editar y desactivar plantillas."),

            // =====================================================
            // HELPDESK - WORKFLOW / AUTOMATION
            // =====================================================

            new(
                "helpdesk.workflows.view",
                "Ver workflows",
                "Helpdesk",
                "Permite consultar ciclos de vida y transiciones."),

            new(
                "helpdesk.workflows.manage",
                "Administrar workflows",
                "Helpdesk",
                "Permite crear y modificar ciclos de vida."),

            new(
                "helpdesk.automation.view",
                "Ver automatizaciones",
                "Helpdesk",
                "Permite consultar reglas y eventos de automatización."),

            new(
                "helpdesk.automation.manage",
                "Administrar automatizaciones",
                "Helpdesk",
                "Permite crear y modificar reglas automáticas."),

            // =====================================================
            // HELPDESK - MAIL
            // =====================================================

            new(
                "helpdesk.mail.view",
                "Ver configuración de correo",
                "Helpdesk",
                "Permite consultar el estado de integración de correo."),

            new(
                "helpdesk.mail.manage",
                "Administrar correo Helpdesk",
                "Helpdesk",
                "Permite configurar entrada y salida de correo."),

            // =====================================================
            // HELPDESK - AI / TITAN
            // =====================================================

            new(
                "helpdesk.ai.use",
                "Usar Titan en Helpdesk",
                "Helpdesk",
                "Permite utilizar funciones asistidas por IA."),

            new(
                "helpdesk.ai.view",
                "Ver configuración de IA",
                "Helpdesk",
                "Permite consultar modelos y estado del proveedor de IA."),

            new(
                "helpdesk.ai.manage",
                "Administrar IA Helpdesk",
                "Helpdesk",
                "Permite modificar configuración y automatizaciones asistidas por IA."),

            // =====================================================
            // HELPDESK - LEGACY COMPATIBILITY
            // =====================================================

            new(
                "helpdesk.view",
                "Ver mesa de ayuda",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "helpdesk.manage",
                "Administrar mesa de ayuda",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "tickets.view",
                "Ver tickets",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "tickets.create",
                "Crear tickets",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "tickets.assign",
                "Asignar tickets",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "tickets.comment",
                "Comentar tickets",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            new(
                "tickets.close",
                "Cerrar tickets",
                "Helpdesk",
                "Permiso heredado de compatibilidad temporal."),

            // =====================================================
            // PONCHES
            // =====================================================

            new(
                "workspace.ponches.view",
                "Acceder a Ponches",
                "Ponches",
                "Acceder a Ponches."),

            new(
                "ponches.manage",
                "Administrar todo Ponches",
                "Ponches",
                "Administrar todo Ponches."),

            new(
                "ponches.dashboard.view",
                "Ver dashboard",
                "Ponches",
                "Ver dashboard."),

            new(
                "ponches.records.view",
                "Ver ponches",
                "Ponches",
                "Ver ponches."),

            new(
                "ponches.history.view",
                "Ver historial SQL",
                "Ponches",
                "Ver historial SQL."),

            new(
                "ponches.remote.create",
                "Registrar ponche remoto",
                "Ponches",
                "Registrar ponche remoto."),

            new(
                "ponches.devices.view",
                "Ver relojes",
                "Ponches",
                "Ver relojes."),

            new(
                "ponches.devices.manage",
                "Administrar relojes",
                "Ponches",
                "Administrar relojes."),

            new(
                "ponches.employees.view",
                "Ver empleados",
                "Ponches",
                "Ver empleados."),

            new(
                "ponches.collaborators.view",
                "Ver colaboradores",
                "Ponches",
                "Ver colaboradores."),

            new(
                "ponches.collaborators.manage",
                "Editar colaboradores",
                "Ponches",
                "Editar colaboradores."),

            new(
                "ponches.collaborators.sync",
                "Sincronizar colaboradores y biometría",
                "Ponches",
                "Sincronizar colaboradores y biometría."),

            new(
                "ponches.schedules.view",
                "Ver horarios",
                "Ponches",
                "Ver horarios."),

            new(
                "ponches.schedules.manage",
                "Administrar horarios",
                "Ponches",
                "Administrar horarios."),

            new(
                "ponches.inventory.view",
                "Ver inventario biométrico",
                "Ponches",
                "Ver inventario biométrico."),

            new(
                "ponches.inventory.manage",
                "Administrar inventario biométrico",
                "Ponches",
                "Administrar inventario biométrico."),

            new(
                "ponches.bulk.execute",
                "Ejecutar operaciones masivas",
                "Ponches",
                "Ejecutar operaciones masivas."),

            new(
                "ponches.reports.view",
                "Ver reportes de asistencia",
                "Ponches",
                "Ver reportes de asistencia."),

            new(
                "ponches.export",
                "Exportar Excel y PDF",
                "Ponches",
                "Exportar Excel y PDF."),

            new(
                "ponches.sync.view",
                "Ver historial de sincronización",
                "Ponches",
                "Ver historial de sincronización."),

            new(
                "ponches.sync.run",
                "Ejecutar sincronización",
                "Ponches",
                "Ejecutar sincronización."),

            new(
                "ponches.settings.view",
                "Ver configuración de Ponches",
                "Ponches",
                "Ver configuración de Ponches."),

            new(
                "ponches.settings.manage",
                "Editar configuración de Ponches",
                "Ponches",
                "Editar configuración de Ponches."),

            new(
                "ponches.users.view",
                "Acceder a usuarios desde Ponches",
                "Ponches",
                "Acceder a usuarios desde Ponches."),

            new(
                "ponches.advanced-reports.view",
                "Ver reportes avanzados",
                "Ponches",
                "Ver reportes avanzados."),

            new(
                "ponches.fiorella.use",
                "Usar Fiorella",
                "Ponches",
                "Usar Fiorella."),

            // =====================================================
            // SITES / MULTI-SITE
            // =====================================================

            new(
                "sites.view",
                "Ver localidades",
                "Sites",
                "Permite consultar localidades y ubicaciones."),

            new(
                "sites.manage",
                "Administrar localidades",
                "Sites",
                "Permite crear, modificar y administrar localidades y ubicaciones.")
        ];

    // ============================================================
    // SEED ENTRY POINT
    // ============================================================

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        /*
         * DatabaseBootstrapper maneja migrations.
         *
         * El Seeder únicamente garantiza datos mínimos,
         * permisos, break-glass y reconciliación de scopes.
         */

        var organization =
            await EnsureOrganizationAsync(
                cancellationToken);

        var permissions =
            await EnsurePermissionsAsync(
                cancellationToken);

        var superAdminRole =
            await EnsureSuperAdminRoleAsync(
                organization,
                cancellationToken);

        await EnsureSuperAdminPermissionsAsync(
            permissions,
            cancellationToken);

        var bootstrapAdministrator =
            await EnsureBootstrapAdministratorAsync(
                organization,
                cancellationToken);

        await EnsureSuperAdminRoleAssignmentAsync(
            bootstrapAdministrator,
            superAdminRole,
            cancellationToken);

        /*
         * IMPORTANTE
         *
         * Ya no otorgamos Organization Scope solamente al
         * usuario bootstrap.
         *
         * Reconciliamos TODOS los SuperAdmin activos.
         */
        await EnsureSystemSuperAdminScopesAsync(
            cancellationToken);
    }

    // ============================================================
    // ORGANIZATION
    // ============================================================

    private async Task<Organization>
        EnsureOrganizationAsync(
            CancellationToken cancellationToken)
    {
        var organization =
            await _dbContext
                .Organizations
                .FirstOrDefaultAsync(
                    x =>
                        x.Code ==
                            DefaultOrganizationCode,
                    cancellationToken);

        if (organization is not null)
        {
            return organization;
        }

        organization =
            new Organization(
                DefaultOrganizationName,
                DefaultOrganizationCode);

        organization.UpdateInformation(
            DefaultOrganizationName,
            "Plataforma empresarial de administración y seguridad de dispositivos.",
            null,
            "Dominican Republic",
            "America/Santo_Domingo");

        _dbContext
            .Organizations
            .Add(
                organization);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return organization;
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private async Task<List<Permission>>
        EnsurePermissionsAsync(
            CancellationToken cancellationToken)
    {
        var codes =
            Definitions
                .Select(
                    x =>
                        x.Code)
                .ToArray();

        var permissions =
            await _dbContext
                .Permissions
                .Where(
                    x =>
                        codes.Contains(
                            x.Code))
                .ToListAsync(
                    cancellationToken);

        var existingCodes =
            permissions
                .Select(
                    x =>
                        x.Code)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var definition
            in Definitions)
        {
            if (
                existingCodes.Contains(
                    definition.Code))
            {
                continue;
            }

            var permission =
                new Permission(
                    definition.Code,
                    definition.Name,
                    definition.Module,
                    definition.Description);

            _dbContext
                .Permissions
                .Add(
                    permission);

            permissions.Add(
                permission);

            existingCodes.Add(
                definition.Code);
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return permissions;
    }

    // ============================================================
    // SUPERADMIN ROLE
    // ============================================================

    private async Task<Role>
        EnsureSuperAdminRoleAsync(
            Organization organization,
            CancellationToken cancellationToken)
    {
        var role =
            await _dbContext
                .Roles
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organization.Id
                        &&
                        x.Name ==
                            SuperAdminRoleName,
                    cancellationToken);

        if (role is not null)
        {
            return role;
        }

        role =
            new Role(
                organization.Id,
                SuperAdminRoleName);

        role.SetDescription(
            "Administrador principal con acceso total a TitanMDM.");

        role.MarkAsSystemRole();

        _dbContext
            .Roles
            .Add(
                role);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return role;
    }

    // ============================================================
    // SUPERADMIN PERMISSIONS
    // ============================================================

    private async Task
        EnsureSuperAdminPermissionsAsync(
            IReadOnlyCollection<Permission> permissions,
            CancellationToken cancellationToken)
    {
        /*
         * Todos los roles de sistema llamados SuperAdmin
         * reciben todos los permisos activos.
         *
         * Esto mantiene soporte para futuras organizaciones.
         */

        var adminRoleIds =
            await _dbContext
                .Roles
                .Where(
                    x =>
                        x.IsSystemRole
                        &&
                        x.IsActive
                        &&
                        x.Name ==
                            SuperAdminRoleName)
                .Select(
                    x =>
                        x.Id)
                .ToArrayAsync(
                    cancellationToken);

        if (
            adminRoleIds.Length ==
                0)
        {
            return;
        }

        foreach (
            var roleId
            in adminRoleIds)
        {
            var assignedPermissionIds =
                (
                    await _dbContext
                        .RolePermissions
                        .Where(
                            x =>
                                x.RoleId ==
                                    roleId)
                        .Select(
                            x =>
                                x.PermissionId)
                        .ToListAsync(
                            cancellationToken)
                )
                .ToHashSet();

            foreach (
                var permission
                in permissions.Where(
                    x =>
                        x.IsActive))
            {
                if (
                    assignedPermissionIds.Contains(
                        permission.Id))
                {
                    continue;
                }

                _dbContext
                    .RolePermissions
                    .Add(
                        new RolePermission(
                            roleId,
                            permission.Id));

                assignedPermissionIds.Add(
                    permission.Id);
            }
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    // ============================================================
    // BOOTSTRAP ADMINISTRATOR / BREAK-GLASS
    // ============================================================

    private async Task<User>
        EnsureBootstrapAdministratorAsync(
            Organization organization,
            CancellationToken cancellationToken)
    {
        var user =
            await _dbContext
                .Users
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organization.Id
                        &&
                        x.Email ==
                            BootstrapAdminEmail,
                    cancellationToken);

        if (user is not null)
        {
            return user;
        }

        var password =
            Environment
                .GetEnvironmentVariable(
                    "TITAN_BOOTSTRAP_PASSWORD");

        if (
            string.IsNullOrWhiteSpace(
                password)
            ||
            password.Length <
                12)
        {
            throw new InvalidOperationException(
                "Primera instalación: configura TITAN_BOOTSTRAP_PASSWORD " +
                "con al menos 12 caracteres. Las instalaciones con el " +
                "administrador existente no necesitan esta variable.");
        }

        user =
            new User(
                organization.Id,
                "Titan",
                "Administrator",
                BootstrapAdminEmail);

        user.SetPasswordHash(
            _passwordHasher
                .HashPassword(
                    user,
                    password));

        _dbContext
            .Users
            .Add(
                user);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return user;
    }

    // ============================================================
    // BOOTSTRAP SUPERADMIN ROLE ASSIGNMENT
    // ============================================================

    private async Task
        EnsureSuperAdminRoleAssignmentAsync(
            User user,
            Role role,
            CancellationToken cancellationToken)
    {
        var assigned =
            await _dbContext
                .UserRoles
                .AnyAsync(
                    x =>
                        x.UserId ==
                            user.Id
                        &&
                        x.RoleId ==
                            role.Id,
                    cancellationToken);

        if (assigned)
        {
            return;
        }

        _dbContext
            .UserRoles
            .Add(
                new UserRole(
                    user.Id,
                    role.Id,
                    null));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    // ============================================================
    // SUPERADMIN ORGANIZATION SCOPE RECONCILIATION
    // ============================================================

    private async Task
        EnsureSystemSuperAdminScopesAsync(
            CancellationToken cancellationToken)
    {
        /*
         * ========================================================
         * REGLA DE SEGURIDAD
         * ========================================================
         *
         * Todo usuario:
         *
         *   - activo;
         *   - con rol SuperAdmin activo;
         *   - con rol de sistema;
         *   - perteneciente a la misma organización del rol;
         *
         * obtiene explícitamente:
         *
         * AuthorizationScopeType.Organization
         *
         * NO hay bypass en Controllers.
         * NO hay bypass por email.
         * NO hay bypass dentro del AuthorizationHandler.
         *
         * El acceso global se representa mediante un registro
         * persistido en UserScopeGrants.
         *
         * Esto corrige usuarios corporativos como:
         *
         * esosa@cesariglesias.com.do
         *
         * que tienen SuperAdmin + permisos, pero anteriormente
         * no recibían Organization Scope.
         * ========================================================
         */

        var superAdmins =
            await (
                from userRole
                    in _dbContext
                        .UserRoles
                        .AsNoTracking()

                join user
                    in _dbContext
                        .Users
                        .AsNoTracking()

                    on userRole.UserId
                    equals user.Id

                join role
                    in _dbContext
                        .Roles
                        .AsNoTracking()

                    on userRole.RoleId
                    equals role.Id

                where
                    user.IsActive
                    &&
                    role.IsActive
                    &&
                    role.IsSystemRole
                    &&
                    role.Name ==
                        SuperAdminRoleName
                    &&
                    role.OrganizationId ==
                        user.OrganizationId

                select new
                {
                    UserId =
                        user.Id,

                    OrganizationId =
                        user.OrganizationId
                }
            )
            .Distinct()
            .ToArrayAsync(
                cancellationToken);

        if (
            superAdmins.Length ==
                0)
        {
            return;
        }

        var superAdminUserIds =
            superAdmins
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToArray();

        /*
         * Consultamos únicamente los Organization Scope existentes
         * para los SuperAdmin detectados.
         */

        var existingScopes =
            await _dbContext
                .UserScopeGrants
                .AsNoTracking()
                .Where(
                    x =>
                        superAdminUserIds.Contains(
                            x.UserId)
                        &&
                        x.ScopeType ==
                            AuthorizationScopeType
                                .Organization)
                .Select(
                    x =>
                        new
                        {
                            x.UserId,
                            x.OrganizationId,
                            x.ScopeId
                        })
                .ToArrayAsync(
                    cancellationToken);

        var existingScopeKeys =
            existingScopes
                .Select(
                    x =>
                        (
                            x.UserId,
                            x.OrganizationId,
                            x.ScopeId
                        ))
                .ToHashSet();

        var changes =
            0;

        foreach (
            var superAdmin
            in superAdmins)
        {
            /*
             * Para Organization Scope:
             *
             * OrganizationId = organización del usuario.
             * ScopeId        = misma organización.
             */

            var key =
                (
                    superAdmin.UserId,
                    superAdmin.OrganizationId,
                    superAdmin.OrganizationId
                );

            if (
                existingScopeKeys.Contains(
                    key))
            {
                continue;
            }

            _dbContext
                .UserScopeGrants
                .Add(
                    new UserScopeGrant(
                        superAdmin.OrganizationId,
                        superAdmin.UserId,
                        AuthorizationScopeType
                            .Organization,
                        superAdmin.OrganizationId,
                        null));

            existingScopeKeys.Add(
                key);

            changes++;
        }

        if (
            changes ==
                0)
        {
            return;
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }
}