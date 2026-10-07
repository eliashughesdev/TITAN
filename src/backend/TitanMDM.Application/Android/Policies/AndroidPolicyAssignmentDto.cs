namespace TitanMDM.Application.Android.Policies;

public sealed record AndroidPolicyAssignmentDto(
    Guid AssignmentId,
    Guid PolicyId,
    Guid DeviceId,
    Guid AndroidDeviceId,
    int PolicyVersion,
    string GoogleDeviceName,
    string GooglePolicyName,
    string AssignmentStatus,
    DateTime AssignedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? AppliedAtUtc,
    string? AppliedPolicyName,
    long? AppliedPolicyVersion,
    string? AppliedPolicyState,
    DateTime? LastPolicySyncTimeUtc,
    string? ErrorMessage);