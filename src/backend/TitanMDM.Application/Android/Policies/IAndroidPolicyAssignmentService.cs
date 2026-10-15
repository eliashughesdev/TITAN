namespace TitanMDM.Application.Android.Policies;

public interface IAndroidPolicyAssignmentService
{
    Task<AndroidPolicyAssignmentDto> AssignAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default);

    Task<AndroidPolicyAssignmentDto?> GetAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default);

    Task<AndroidPolicyAssignmentPageDto> GetPageAsync(
        Guid organizationId,
        Guid policyId,
        string? search,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);
}