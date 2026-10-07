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