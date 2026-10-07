namespace TitanMDM.Application.Policies;

public sealed record PolicyDetailsDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string Platform,
    string Status,
    int CurrentVersion,
    string ConfigurationJson,
    int AssignedDevices,
    int AppliedDevices,
    int FailedDevices,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? ArchivedAtUtc);