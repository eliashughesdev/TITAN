using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Helpdesk;

/*
 * ============================================================
 * TITANMDM HELPDESK — ELEGIBILIDAD DE TÉCNICOS (FUENTE ÚNICA)
 * ============================================================
 *
 * Centraliza las reglas que determinan qué usuarios son
 * considerados técnicos elegibles para el routing automático.
 *
 * Antes existían varias copias divergentes de la lista de
 * permisos (motor de routing, planificación de grupos y
 * controlador de staff), lo que provocaba que un usuario
 * pudiera ser elegible en un lugar y no en otro.
 *
 * Esta clase es la única referencia autorizada. Cualquier
 * cambio de reglas debe realizarse aquí.
 */

public static class HelpdeskTechnicianEligibility
{
    /// <summary>
    /// Permisos operativos que convierten a un usuario en
    /// técnico elegible para recibir asignaciones automáticas.
    /// </summary>
    public static readonly string[]
        RoutingPermissions =
        [
            "helpdesk.agent.access",
            "helpdesk.ticket.details.view",
            "helpdesk.ticket.comment",
            "helpdesk.ticket.take",
            "helpdesk.ticket.assign",
            "helpdesk.ticket.transition",
            "helpdesk.ticket.resolve",
            "helpdesk.ticket.close",

            // Compatibilidad legacy
            "tickets.comment",
            "tickets.assign",
            "tickets.close",
            "helpdesk.view",
            "helpdesk.manage"
        ];

    /// <summary>
    /// Devuelve los identificadores de los técnicos elegibles
    /// dentro de la organización indicada.
    ///
    /// La consulta respeta organización, estado activo de
    /// usuario, rol, permiso y la lista canónica de permisos.
    /// </summary>
    public static IQueryable<Guid> EligibleTechnicianIds(
        TitanMdmDbContext db,
        Guid organizationId)
    {
        ArgumentNullException.ThrowIfNull(db);

        return (
            from userRole
                in db.UserRoles
                    .AsNoTracking()

            join role
                in db.Roles
                    .AsNoTracking()
                on userRole.RoleId
                equals role.Id

            join rolePermission
                in db.RolePermissions
                    .AsNoTracking()
                on role.Id
                equals rolePermission.RoleId

            join permission
                in db.Permissions
                    .AsNoTracking()
                on rolePermission.PermissionId
                equals permission.Id

            join user
                in db.Users
                    .AsNoTracking()
                on userRole.UserId
                equals user.Id

            where
                user.OrganizationId ==
                    organizationId
                &&
                role.OrganizationId ==
                    organizationId
                &&
                user.IsActive
                &&
                role.IsActive
                &&
                permission.IsActive
                &&
                RoutingPermissions
                    .Contains(
                        permission.Code)

            select user.Id
        )
        .Distinct();
    }
}
