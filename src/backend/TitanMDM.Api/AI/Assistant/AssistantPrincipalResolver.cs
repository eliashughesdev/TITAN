using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Assistant;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.AI.Assistant;

/// <summary>
/// Resuelve la identidad verificada del usuario a partir del
/// token y de la base de datos. La identidad nunca proviene del
/// texto enviado por el cliente ni del modelo.
/// </summary>
public sealed class AssistantPrincipalResolver
{
    private readonly TitanMdmDbContext _db;

    public AssistantPrincipalResolver(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    public async Task<AssistantPrincipal?> ResolveAsync(
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var organizationClaim =
            user.FindFirstValue("organization_id") ??
            user.FindFirstValue("organizationId");

        var userClaim =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("user_id") ??
            user.FindFirstValue("userId");

        if (!Guid.TryParse(
                organizationClaim,
                out var organizationId) ||
            !Guid.TryParse(
                userClaim,
                out var userId))
        {
            return null;
        }

        var account =
            await _db.Users
                .AsNoTracking()
                .Where(
                    item =>
                        item.OrganizationId == organizationId &&
                        item.Id == userId &&
                        item.IsActive)
                .Select(
                    item =>
                        new
                        {
                            item.FirstName,
                            item.LastName,
                            item.Email
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (account is null)
        {
            return null;
        }

        var assistantEnabled =
            await _db.HelpdeskAssistantAccess
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.OrganizationId == organizationId &&
                        item.UserId == userId &&
                        item.IsEnabled,
                    cancellationToken);

        var roles =
            await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.UserId == userId &&
                      role.OrganizationId == organizationId &&
                      role.IsActive
                select role.Name
            )
            .Distinct()
            .ToListAsync(
                cancellationToken);

        var permissions =
            await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                join rolePermission in _db.RolePermissions.AsNoTracking()
                    on role.Id equals rolePermission.RoleId
                join permission in _db.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.UserId == userId &&
                      role.OrganizationId == organizationId &&
                      role.IsActive &&
                      permission.IsActive
                select permission.Code
            )
            .Distinct()
            .ToListAsync(
                cancellationToken);

        return new AssistantPrincipal(
            organizationId,
            userId,
            $"{account.FirstName} {account.LastName}".Trim(),
            account.Email,
            assistantEnabled,
            roles,
            permissions);
    }
}
