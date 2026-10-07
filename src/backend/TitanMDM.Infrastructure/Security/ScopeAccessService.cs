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
            dbContext;
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
            organizationId == Guid.Empty
            ||
            userId == Guid.Empty)
        {
            return Task.FromResult(
                false);
        }

        return _dbContext
            .UserScopeGrants
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.UserId ==
                        userId
                    &&
                    x.ScopeType ==
                        AuthorizationScopeType.Organization
                    &&
                    x.ScopeId ==
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
            organizationId == Guid.Empty
            ||
            userId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var organizationAccess =
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (organizationAccess)
        {
            return await _dbContext
                .Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .Select(
                    x =>
                        x.Id)
                .ToArrayAsync(
                    cancellationToken);
        }

        return await (
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
            organizationId == Guid.Empty
            ||
            userId == Guid.Empty
            ||
            siteId == Guid.Empty)
        {
            return false;
        }

        var siteExists =
            await _dbContext
                .Sites
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            siteId
                        &&
                        x.IsActive,
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
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.UserId ==
                        userId
                    &&
                    x.ScopeType ==
                        AuthorizationScopeType.Site
                    &&
                    x.ScopeId ==
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
        if (deviceId == Guid.Empty)
        {
            return false;
        }

        var resource =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            deviceId
                        &&
                        !x.IsDeleted)
                .Select(
                    x =>
                        new
                        {
                            x.SiteId
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (resource is null)
        {
            return false;
        }

        var organizationScope =
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (organizationScope)
        {
            return true;
        }

        var sites =
            await GetAccessibleSiteIdsAsync(
                organizationId,
                userId,
                cancellationToken);

        return ResourceScopeRules
            .CanAccessResource(
                false,
                sites,
                resource.SiteId);
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
        if (ticketId == Guid.Empty)
        {
            return false;
        }

        var resource =
            await _dbContext
                .HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId)
                .Select(
                    x =>
                        new
                        {
                            x.SiteId
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (resource is null)
        {
            return false;
        }

        var organizationScope =
            await HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (organizationScope)
        {
            return true;
        }

        var sites =
            await GetAccessibleSiteIdsAsync(
                organizationId,
                userId,
                cancellationToken);

        return ResourceScopeRules
            .CanAccessResource(
                false,
                sites,
                resource.SiteId);
    }
}