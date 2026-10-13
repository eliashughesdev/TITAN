namespace TitanMDM.Application.Sites;

public interface ISiteAssignmentService
{
    Task<IReadOnlyCollection<SiteAssignmentUserDto>> GetUsersAsync(
        Guid organizationId,
        string? search = null,
        bool unassignedOnly = false,
        CancellationToken cancellationToken = default);

    Task AssignUserAsync(
        Guid organizationId,
        Guid userId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default);

    Task AssignUsersAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
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

public sealed record SiteAssignmentUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? JobTitle,
    bool IsActive,
    Guid? SiteId,
    string? SiteName,
    Guid? SiteLocationId,
    string? SiteLocationName,
    bool IsTechnician,
    IReadOnlyCollection<string> Groups);
