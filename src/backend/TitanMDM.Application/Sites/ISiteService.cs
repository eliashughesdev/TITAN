namespace TitanMDM.Application.Sites;

public interface ISiteService
{
    Task<IReadOnlyCollection<SiteDto>>
        GetSitesAsync(
            Guid organizationId,
            bool includeInactive = false,
            CancellationToken cancellationToken = default);

    Task<SiteDto?>
        GetSiteAsync(
            Guid organizationId,
            Guid siteId,
            CancellationToken cancellationToken = default);

    Task<SiteDto>
        CreateSiteAsync(
            Guid organizationId,
            CreateSiteRequest request,
            CancellationToken cancellationToken = default);

    Task<SiteDto>
        UpdateSiteAsync(
            Guid organizationId,
            Guid siteId,
            UpdateSiteRequest request,
            CancellationToken cancellationToken = default);

    Task SetSiteStatusAsync(
        Guid organizationId,
        Guid siteId,
        bool active,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SiteLocationDto>>
        GetLocationsAsync(
            Guid organizationId,
            Guid siteId,
            CancellationToken cancellationToken = default);

    Task<SiteLocationDto>
        CreateLocationAsync(
            Guid organizationId,
            Guid siteId,
            CreateSiteLocationRequest request,
            CancellationToken cancellationToken = default);

    Task<SiteLocationDto>
        UpdateLocationAsync(
            Guid organizationId,
            Guid siteId,
            Guid locationId,
            UpdateSiteLocationRequest request,
            CancellationToken cancellationToken = default);

    Task SetLocationStatusAsync(
        Guid organizationId,
        Guid siteId,
        Guid locationId,
        bool active,
        CancellationToken cancellationToken = default);
}

public sealed record SiteDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Address,
    string? City,
    string? Province,
    string? Country,
    string? TimeZoneId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SiteLocationDto(
    Guid Id,
    Guid SiteId,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateSiteRequest(
    string Code,
    string Name,
    string? Description,
    string? Address,
    string? City,
    string? Province,
    string? Country,
    string? TimeZoneId);

public sealed record UpdateSiteRequest(
    string Code,
    string Name,
    string? Description,
    string? Address,
    string? City,
    string? Province,
    string? Country,
    string? TimeZoneId);

public sealed record CreateSiteLocationRequest(
    string Name,
    string? Description);

public sealed record UpdateSiteLocationRequest(
    string Name,
    string? Description);