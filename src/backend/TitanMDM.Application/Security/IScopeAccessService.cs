namespace TitanMDM.Application.Security;

public interface IScopeAccessService
{
    Task<bool> HasOrganizationScopeAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>>
        GetAccessibleSiteIdsAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default);

    Task<AuthorizationScopeSnapshot>
        GetScopeSnapshotAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default);

    Task<bool> CanAccessSiteAsync(
        Guid organizationId,
        Guid userId,
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessDeviceAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessTicketAsync(
        Guid organizationId,
        Guid userId,
        Guid ticketId,
        CancellationToken cancellationToken = default);
}

public sealed record AuthorizationScopeSnapshot(
    bool OrganizationWide,
    IReadOnlyCollection<Guid> SiteIds)
{
    public bool HasAnyScope =>
        OrganizationWide
        ||
        SiteIds.Count > 0;

    public static AuthorizationScopeSnapshot
        Organization()
    {
        return new AuthorizationScopeSnapshot(
            true,
            Array.Empty<Guid>());
    }

    public static AuthorizationScopeSnapshot
        Sites(
            IReadOnlyCollection<Guid> siteIds)
    {
        return new AuthorizationScopeSnapshot(
            false,
            siteIds
                .Where(
                    x =>
                        x != Guid.Empty)
                .Distinct()
                .ToArray());
    }

    public static AuthorizationScopeSnapshot
        Empty()
    {
        return new AuthorizationScopeSnapshot(
            false,
            Array.Empty<Guid>());
    }
}