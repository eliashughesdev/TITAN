namespace TitanMDM.Application.Policies;

public sealed record PolicyDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string Platform,
    string Status,
    int CurrentVersion,
    int AssignedDevices,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? ArchivedAtUtc);