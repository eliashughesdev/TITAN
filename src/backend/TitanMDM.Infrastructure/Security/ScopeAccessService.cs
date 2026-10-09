using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Security;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Security;

public sealed class ScopeAccessService
    : IScopeAccessService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public ScopeAccessService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext
            ??
            throw new ArgumentNullException(
                nameof(
                    dbContext));
    }

    // ============================================================
    // ORGANIZATION
    // ============================================================

    public Task<bool>
        HasOrganizationScopeAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty)
        {
            return Task.FromResult(
                false);
        }

        return _dbContext
            .UserScopeGrants
            .AsNoTracking()
            .AnyAsync(
                grant =>
                    grant.OrganizationId ==
                        organizationId
                    &&
                    grant.UserId ==
                        userId
                    &&
                    grant.ScopeType ==
                        AuthorizationScopeType.Organization
                    &&
                    grant.ScopeId ==
                        organizationId,
                cancellationToken);
    }

    // ============================================================
    // ACCESSIBLE SITES
    // ============================================================

    public async Task<
        IReadOnlyCollection<Guid>>
        GetAccessibleSiteIdsAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var organizationWide =
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (organizationWide)
        {
            return await _dbContext
                .Sites
                .AsNoTracking()
                .Where(
                    site =>
                        site.OrganizationId ==
                            organizationId
                        &&
                        site.IsActive)
                .OrderBy(
                    site =>
                        site.Name)
                .Select(
                    site =>
                        site.Id)
                .ToArrayAsync(
                    cancellationToken);
        }

        /*
         * IMPORTANTE
         *
         * Solamente devolvemos Sites:
         *
         * - que pertenezcan a la organización;
         * - que estén activos;
         * - para los cuales exista un grant explícito.
         *
         * Department y Group NO se convierten implícitamente
         * en Site access.
         */

        var siteIds =
            await (
                from grant
                    in _dbContext
                        .UserScopeGrants
                        .AsNoTracking()

                join site
                    in _dbContext
                        .Sites
                        .AsNoTracking()

                    on grant.ScopeId
                    equals site.Id

                where
                    grant.OrganizationId ==
                        organizationId
                    &&
                    grant.UserId ==
                        userId
                    &&
                    grant.ScopeType ==
                        AuthorizationScopeType.Site
                    &&
                    site.OrganizationId ==
                        organizationId
                    &&
                    site.IsActive

                select site.Id
            )
            .Distinct()
            .ToArrayAsync(
                cancellationToken);

        return siteIds;
    }

    // ============================================================
    // SNAPSHOT
    // ============================================================

    public async Task<AuthorizationScopeSnapshot>
        GetScopeSnapshotAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty)
        {
            return AuthorizationScopeSnapshot
                .Empty();
        }

        var organizationWide =
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (organizationWide)
        {
            return AuthorizationScopeSnapshot
                .Organization();
        }

        var siteIds =
            await GetAccessibleSiteIdsAsync(
                organizationId,
                userId,
                cancellationToken);

        if (
            siteIds.Count ==
            0)
        {
            return AuthorizationScopeSnapshot
                .Empty();
        }

        return AuthorizationScopeSnapshot
            .Sites(
                siteIds);
    }

    // ============================================================
    // SITE
    // ============================================================

    public async Task<bool>
        CanAccessSiteAsync(
            Guid organizationId,
            Guid userId,
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty
            ||
            siteId ==
                Guid.Empty)
        {
            return false;
        }

        /*
         * Nunca autorizamos un Site inexistente
         * o perteneciente a otra organización.
         */

        var siteExists =
            await _dbContext
                .Sites
                .AsNoTracking()
                .AnyAsync(
                    site =>
                        site.Id ==
                            siteId
                        &&
                        site.OrganizationId ==
                            organizationId
                        &&
                        site.IsActive,
                    cancellationToken);

        if (!siteExists)
        {
            return false;
        }

        if (
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken))
        {
            return true;
        }

        return await _dbContext
            .UserScopeGrants
            .AsNoTracking()
            .AnyAsync(
                grant =>
                    grant.OrganizationId ==
                        organizationId
                    &&
                    grant.UserId ==
                        userId
                    &&
                    grant.ScopeType ==
                        AuthorizationScopeType.Site
                    &&
                    grant.ScopeId ==
                        siteId,
                cancellationToken);
    }

    // ============================================================
    // DEVICE
    // ============================================================

    public async Task<bool>
        CanAccessDeviceAsync(
            Guid organizationId,
            Guid userId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty
            ||
            deviceId ==
                Guid.Empty)
        {
            return false;
        }

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    item =>
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.Id ==
                            deviceId
                        &&
                        !item.IsDeleted)
                .Select(
                    item =>
                        new
                        {
                            item.SiteId
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (device is null)
        {
            return false;
        }

        var snapshot =
            await GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        if (
            snapshot.OrganizationWide)
        {
            return true;
        }

        /*
         * Un recurso sin Site solamente es visible
         * desde Organization scope.
         *
         * Esto evita que dispositivos sin clasificar
         * se filtren accidentalmente hacia técnicos regionales.
         */

        if (
            !device.SiteId.HasValue
            ||
            device.SiteId.Value ==
                Guid.Empty)
        {
            return false;
        }

        return snapshot
            .SiteIds
            .Contains(
                device.SiteId.Value);
    }

    // ============================================================
    // HELPDESK
    // ============================================================

    public async Task<bool>
        CanAccessTicketAsync(
            Guid organizationId,
            Guid userId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty
            ||
            ticketId ==
                Guid.Empty)
        {
            return false;
        }

        var ticket =
            await _dbContext
                .HelpdeskTickets
                .AsNoTracking()
                .Where(
                    item =>
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.Id ==
                            ticketId)
                .Select(
                    item =>
                        new
                        {
                            item.SiteId
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (ticket is null)
        {
            return false;
        }

        var snapshot =
            await GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        if (
            snapshot.OrganizationWide)
        {
            return true;
        }

        /*
         * Ticket sin Site:
         *
         * no concedemos acceso por accidente a usuarios
         * regionales solamente porque el Ticket todavía
         * no haya sido clasificado.
         *
         * Helpdesk tendrá su propia lógica operacional
         * para My Work / requester / unassigned cuando
         * entremos al bloque HD-E.
         */

        if (
            !ticket.SiteId.HasValue
            ||
            ticket.SiteId.Value ==
                Guid.Empty)
        {
            return false;
        }

        return snapshot
            .SiteIds
            .Contains(
                ticket.SiteId.Value);
    }
}