namespace TitanMDM.Application.Android.Policies;

public interface IAndroidPolicyAssignmentService
{
    Task<AndroidPolicyAssignmentDto> AssignAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<AndroidPolicyAssignmentDto?> GetAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}