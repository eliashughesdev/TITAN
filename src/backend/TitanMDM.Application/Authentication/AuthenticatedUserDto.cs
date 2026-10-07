namespace TitanMDM.Application.Authentication;

public sealed record AuthenticatedUserDto(
    Guid Id,
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string Email,
    string[] Roles,
    string[] Permissions);