namespace TitanMDM.Application.Sites;

public interface ISiteAssignmentService
{
    Task AssignUserAsync(
        Guid organizationId,
        Guid userId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default);

    Task AssignDeviceAsync(
        Guid organizationId,
        Guid deviceId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default);

    Task AssignTicketAsync(
        Guid organizationId,
        Guid ticketId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default);
}