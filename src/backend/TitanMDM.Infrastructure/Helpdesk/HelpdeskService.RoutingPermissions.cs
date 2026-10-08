using Microsoft.EntityFrameworkCore;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    private static readonly string[]
        TechnicianRoutingPermissions =
        [
            "helpdesk.agent.access",
            "helpdesk.ticket.details.view",
            "helpdesk.ticket.comment",
            "helpdesk.ticket.take",
            "helpdesk.ticket.assign",
            "helpdesk.ticket.transition",
            "helpdesk.ticket.resolve",
            "helpdesk.ticket.close",

            // Legacy compatibility
            "tickets.comment",
            "tickets.assign",
            "tickets.close",
            "helpdesk.view",
            "helpdesk.manage"
        ];

    private IQueryable<Guid>
        EligibleTechnicians(
            Guid organizationId)
    {
        return (
            from userRole
                in _db.UserRoles
                    .AsNoTracking()

            join role
                in _db.Roles
                    .AsNoTracking()
                on userRole.RoleId
                equals role.Id

            join rolePermission
                in _db.RolePermissions
                    .AsNoTracking()
                on role.Id
                equals rolePermission.RoleId

            join permission
                in _db.Permissions
                    .AsNoTracking()
                on rolePermission.PermissionId
                equals permission.Id

            join user
                in _db.Users
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
                TechnicianRoutingPermissions
                    .Contains(
                        permission.Code)

            select user.Id
        )
        .Distinct();
    }
}