using System.Security.Claims;

namespace TitanMDM.Api.Security;

/*
 * ============================================================
 * TITANMDM HELPDESK — AUTORIZACIÓN DE ROUTING (FUENTE ÚNICA)
 * ============================================================
 *
 * Los controladores de routing (operaciones, cola y
 * diagnósticos) compartían comprobaciones manuales con
 * conjuntos de permisos ligeramente distintos, lo que hacía
 * que un mismo usuario pudiera ver una pantalla y no otra.
 *
 * Esta clase centraliza los conjuntos de permisos aceptados.
 * Los conjuntos son la UNIÓN de los permisos que ya aceptaba
 * cada controlador, por lo que ningún permiso existente se
 * elimina ni se debilita accidentalmente.
 *
 * Cualquier ajuste de permisos de routing debe hacerse aquí.
 */

public static class HelpdeskRoutingAuthorization
{
    /// <summary>
    /// Permisos que autorizan la lectura de información de
    /// routing (cola, salud del motor y diagnósticos).
    /// </summary>
    public static readonly string[]
        ViewPermissions =
        [
            "helpdesk.routing.view",
            "helpdesk.routing.manage",
            "helpdesk.automation.view",
            "helpdesk.automation.manage",
            "helpdesk.technicians.view",
            "helpdesk.technicians.manage",
            "helpdesk.groups.view",
            "helpdesk.groups.manage",
            "helpdesk.admin.access",

            // Compatibilidad operativa / legacy
            "helpdesk.ticket.details.view",
            "tickets.view",
            "helpdesk.view",
            "helpdesk.manage",
            "settings.manage"
        ];

    /// <summary>
    /// Permisos que autorizan mutaciones de routing
    /// (reprocesar, asignar y reasignar).
    /// </summary>
    public static readonly string[]
        ManagePermissions =
        [
            "helpdesk.routing.manage",
            "helpdesk.automation.manage",
            "helpdesk.technicians.manage",
            "helpdesk.groups.manage",
            "helpdesk.admin.access",

            // Compatibilidad operativa / legacy
            "helpdesk.ticket.assign",
            "tickets.assign",
            "helpdesk.manage",
            "settings.manage"
        ];

    public static bool CanView(
        ClaimsPrincipal user)
    {
        return HasAnyPermission(
            user,
            ViewPermissions);
    }

    public static bool CanManage(
        ClaimsPrincipal user)
    {
        return HasAnyPermission(
            user,
            ManagePermissions);
    }

    public static bool HasAnyPermission(
        ClaimsPrincipal user,
        params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(permissions);

        return user.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                permissions.Any(
                    permission =>
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }
}
