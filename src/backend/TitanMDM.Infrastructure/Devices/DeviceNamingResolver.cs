using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Devices.Naming;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices;

public sealed class DeviceNamingResolver
{
    private readonly TitanMdmDbContext
        _db;

    private readonly DeviceNamingOptions
        _options;

    public DeviceNamingResolver(
        TitanMdmDbContext db,
        IOptions<DeviceNamingOptions> options)
    {
        _db =
            db;

        _options =
            options.Value;
    }

    public async Task<DeviceNamingResolution>
        ResolveAsync(
            Guid organizationId,
            string? deviceName,
            CancellationToken cancellationToken = default)
    {
        var parsed =
            DeviceNameParser.Parse(
                deviceName,
                _options);

        if (
            !parsed.Matched
            ||
            !parsed.Valid)
        {
            return new DeviceNamingResolution(
                parsed,
                null,
                null,
                false);
        }

        if (
            string.IsNullOrWhiteSpace(
                parsed.SuggestedSiteName))
        {
            return new DeviceNamingResolution(
                parsed,
                null,
                null,
                false);
        }

        var siteName =
            parsed.SuggestedSiteName
                .Trim();

        var site =
            await _db
                .Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .Where(
                    x =>
                        x.Name ==
                            siteName
                        ||
                        x.Name.Contains(
                            siteName))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Name
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (site is null)
        {
            return new DeviceNamingResolution(
                parsed,
                null,
                null,
                false);
        }

        Guid? siteLocationId =
            null;

        if (
            !string.IsNullOrWhiteSpace(
                parsed.SuggestedSiteLocationName))
        {
            var locationName =
                parsed
                    .SuggestedSiteLocationName
                    .Trim();

            siteLocationId =
                await _db
                    .SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.SiteId ==
                                site.Id
                            &&
                            x.IsActive
                            &&
                            x.Name ==
                                locationName)
                    .Select(
                        x =>
                            (Guid?)x.Id)
                    .FirstOrDefaultAsync(
                        cancellationToken);
        }

        return new DeviceNamingResolution(
            parsed,
            site.Id,
            siteLocationId,
            true);
    }
}

public sealed record DeviceNamingResolution(
    DeviceNameParseResult Parsed,
    Guid? SiteId,
    Guid? SiteLocationId,
    bool Resolved);