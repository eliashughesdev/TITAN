using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Sites;

using TitanMDM.Domain.Entities;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Sites;

public sealed class SiteService
    : ISiteService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public SiteService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<IReadOnlyCollection<SiteDto>>
        GetSitesAsync(
            Guid organizationId,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext
                .Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId);

        if (!includeInactive)
        {
            query =
                query.Where(
                    x =>
                        x.IsActive);
        }

        return await query
            .OrderBy(
                x =>
                    x.Name)
            .Select(
                x =>
                    MapSite(x))
            .ToArrayAsync(
                cancellationToken);
    }

    public async Task<SiteDto?>
        GetSiteAsync(
            Guid organizationId,
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .Sites
            .AsNoTracking()
            .Where(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Id ==
                        siteId)
            .Select(
                x =>
                    MapSite(x))
            .SingleOrDefaultAsync(
                cancellationToken);
    }

    public async Task<SiteDto>
        CreateSiteAsync(
            Guid organizationId,
            CreateSiteRequest request,
            CancellationToken cancellationToken = default)
    {
        var code =
            request.Code
                .Trim()
                .ToUpperInvariant();

        var exists =
            await _dbContext
                .Sites
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Code ==
                            code,
                    cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "Ya existe una localidad con ese código.");
        }

        var site =
            new Site(
                organizationId,
                request.Code,
                request.Name);

        site.Update(
            request.Name,
            request.Description,
            request.Address,
            request.City,
            request.Province,
            request.Country,
            request.TimeZoneId);

        _dbContext
            .Sites
            .Add(
                site);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return MapSite(
            site);
    }

    public async Task<SiteDto>
        UpdateSiteAsync(
            Guid organizationId,
            Guid siteId,
            UpdateSiteRequest request,
            CancellationToken cancellationToken = default)
    {
        var site =
            await GetSiteEntityAsync(
                organizationId,
                siteId,
                cancellationToken);

        var code =
            request.Code
                .Trim()
                .ToUpperInvariant();

        var duplicate =
            await _dbContext
                .Sites
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id !=
                            siteId
                        &&
                        x.Code ==
                            code,
                    cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "Ya existe otra localidad con ese código.");
        }

        if (
            !string.Equals(
                site.Code,
                code,
                StringComparison.OrdinalIgnoreCase))
        {
            site.RenameCode(
                code);
        }

        site.Update(
            request.Name,
            request.Description,
            request.Address,
            request.City,
            request.Province,
            request.Country,
            request.TimeZoneId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return MapSite(
            site);
    }

    public async Task SetSiteStatusAsync(
        Guid organizationId,
        Guid siteId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var site =
            await GetSiteEntityAsync(
                organizationId,
                siteId,
                cancellationToken);

        if (active)
        {
            site.Activate();
        }
        else
        {
            site.Deactivate();
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<SiteLocationDto>>
        GetLocationsAsync(
            Guid organizationId,
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        await EnsureSiteExistsAsync(
            organizationId,
            siteId,
            cancellationToken);

        return await _dbContext
            .SiteLocations
            .AsNoTracking()
            .Where(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.SiteId ==
                        siteId)
            .OrderBy(
                x =>
                    x.Name)
            .Select(
                x =>
                    MapLocation(x))
            .ToArrayAsync(
                cancellationToken);
    }

    public async Task<SiteLocationDto>
        CreateLocationAsync(
            Guid organizationId,
            Guid siteId,
            CreateSiteLocationRequest request,
            CancellationToken cancellationToken = default)
    {
        await EnsureSiteExistsAsync(
            organizationId,
            siteId,
            cancellationToken);

        var name =
            request.Name.Trim();

        var exists =
            await _dbContext
                .SiteLocations
                .AnyAsync(
                    x =>
                        x.SiteId ==
                            siteId
                        &&
                        x.Name ==
                            name,
                    cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "Ya existe una ubicación con ese nombre en la localidad.");
        }

        var location =
            new SiteLocation(
                organizationId,
                siteId,
                name);

        location.Update(
            name,
            request.Description);

        _dbContext
            .SiteLocations
            .Add(
                location);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return MapLocation(
            location);
    }

    public async Task<SiteLocationDto>
        UpdateLocationAsync(
            Guid organizationId,
            Guid siteId,
            Guid locationId,
            UpdateSiteLocationRequest request,
            CancellationToken cancellationToken = default)
    {
        var location =
            await _dbContext
                .SiteLocations
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        x.Id ==
                            locationId,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "La ubicación no existe.");

        var name =
            request.Name.Trim();

        var duplicate =
            await _dbContext
                .SiteLocations
                .AnyAsync(
                    x =>
                        x.SiteId ==
                            siteId
                        &&
                        x.Id !=
                            locationId
                        &&
                        x.Name ==
                            name,
                    cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "Ya existe otra ubicación con ese nombre.");
        }

        location.Update(
            name,
            request.Description);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return MapLocation(
            location);
    }

    public async Task SetLocationStatusAsync(
        Guid organizationId,
        Guid siteId,
        Guid locationId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var location =
            await _dbContext
                .SiteLocations
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        x.Id ==
                            locationId,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "La ubicación no existe.");

        if (active)
        {
            location.Activate();
        }
        else
        {
            location.Deactivate();
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    private async Task<Site>
        GetSiteEntityAsync(
            Guid organizationId,
            Guid siteId,
            CancellationToken cancellationToken)
    {
        return await _dbContext
            .Sites
            .SingleOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Id ==
                        siteId,
                cancellationToken)
        ??
        throw new InvalidOperationException(
            "La localidad no existe.");
    }

    private async Task EnsureSiteExistsAsync(
        Guid organizationId,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var exists =
            await _dbContext
                .Sites
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            siteId,
                    cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                "La localidad no existe.");
        }
    }

    private static SiteDto MapSite(
        Site site)
    {
        return new SiteDto(
            site.Id,
            site.Code,
            site.Name,
            site.Description,
            site.Address,
            site.City,
            site.Province,
            site.Country,
            site.TimeZoneId,
            site.IsActive,
            site.CreatedAtUtc,
            site.UpdatedAtUtc);
    }

    private static SiteLocationDto MapLocation(
        SiteLocation location)
    {
        return new SiteLocationDto(
            location.Id,
            location.SiteId,
            location.Name,
            location.Description,
            location.IsActive,
            location.CreatedAtUtc,
            location.UpdatedAtUtc);
    }
}