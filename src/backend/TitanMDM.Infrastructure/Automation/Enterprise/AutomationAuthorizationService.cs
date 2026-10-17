using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise;

/*
 * AUT-1.8 — Implementación de la autorización del motor.
 *
 * Resuelve permisos efectivos por base de datos (roles activos
 * del usuario dentro de la organización) y delega el alcance a
 * IScopeAccessService. Falla cerrado ante actor desconocido.
 */
internal sealed class AutomationAuthorizationService
    : IAutomationAuthorizationService
{
    private readonly TitanMdmDbContext _db;
    private readonly IScopeAccessService _scopeAccess;

    public AutomationAuthorizationService(
        TitanMdmDbContext db,
        IScopeAccessService scopeAccess)
    {
        _db = db;
        _scopeAccess = scopeAccess;
    }

    public async Task<bool> HasPermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        var granting = AutomationPermissions
            .GrantingCodes(permissionCode.Trim())
            .ToArray();

        var hasPermission = await (
            from userRole
                in _db.UserRoles.AsNoTracking()

            join role
                in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id

            join rolePermission
                in _db.RolePermissions.AsNoTracking()
                on role.Id equals rolePermission.RoleId

            join permission
                in _db.Permissions.AsNoTracking()
                on rolePermission.PermissionId
                equals permission.Id

            join user
                in _db.Users.AsNoTracking()
                on userRole.UserId equals user.Id

            where
                userRole.UserId == userId
                && user.OrganizationId == organizationId
                && role.OrganizationId == organizationId
                && user.IsActive
                && role.IsActive
                && permission.IsActive
                && granting.Contains(permission.Code)

            select permission.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        return hasPermission;
    }

    public async Task EnsurePermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var allowed = await HasPermissionAsync(
                organizationId,
                userId,
                permissionCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            throw new AutomationAuthorizationException(
                $"El actor no posee el permiso " +
                $"'{permissionCode}' para esta operación " +
                "de automatización.");
        }
    }

    public Task<AuthorizationScopeSnapshot> GetScopeAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            userId == Guid.Empty)
        {
            return Task.FromResult(
                AuthorizationScopeSnapshot.Empty());
        }

        return _scopeAccess.GetScopeSnapshotAsync(
            organizationId,
            userId,
            cancellationToken);
    }

    public async Task EnsureScopeCoversSitesAsync(
        Guid organizationId,
        Guid userId,
        IReadOnlyCollection<Guid> siteIds,
        CancellationToken cancellationToken = default)
    {
        var requested = (siteIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requested.Length > 0)
        {
            var validSites = await _db.Sites
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.IsActive &&
                        requested.Contains(x.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            if (validSites != requested.Length)
            {
                throw new AutomationAuthorizationException(
                    "Uno o más sitios de la regla no existen, " +
                    "están inactivos o pertenecen a otra " +
                    "organización.");
            }
        }

        var scope = await GetScopeAsync(
                organizationId,
                userId,
                cancellationToken)
            .ConfigureAwait(false);

        // Alcance de organización cubre cualquier sitio.
        if (scope.OrganizationWide)
            return;

        if (requested.Length == 0)
        {
            // Sin sitios explícitos el autorizador interpreta
            // "organización completa", que este actor no tiene.
            throw new AutomationAuthorizationException(
                "El actor no tiene alcance de organización para " +
                "definir una regla sin sitios explícitos.");
        }

        var accessible = scope.SiteIds.ToHashSet();
        var forbidden = requested
            .Where(site => !accessible.Contains(site))
            .ToArray();

        if (forbidden.Length > 0)
        {
            throw new AutomationAuthorizationException(
                "El alcance del actor no cubre todos los sitios " +
                "solicitados por la regla.");
        }
    }

    public Task<bool> CanAccessDeviceAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            userId == Guid.Empty ||
            deviceId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _scopeAccess.CanAccessDeviceAsync(
            organizationId,
            userId,
            deviceId,
            cancellationToken);
    }

    public Task<bool> CanAccessTicketAsync(
        Guid organizationId,
        Guid userId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            userId == Guid.Empty ||
            ticketId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _scopeAccess.CanAccessTicketAsync(
            organizationId,
            userId,
            ticketId,
            cancellationToken);
    }
}
