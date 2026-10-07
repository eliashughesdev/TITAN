namespace TitanMDM.Application.Policies;

public sealed record PolicyAssignmentDto(
    Guid Id,
    Guid PolicyId,
    Guid DeviceId,
    string DeviceName,
    int PolicyVersion,
    string Status,
    Guid? CommandId,
    DateTime AssignedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? AppliedAtUtc,
    string? ErrorMessage);