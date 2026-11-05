namespace TitanMDM.Application.Android.Policies;

public interface IAndroidPolicyAssignmentService
{
    /// <param name="actorUserId">
    /// Usuario que asigna. Obligatorio para equipos del TitanMDM Agent,
    /// donde la política se entrega como comando en la cola SQL.
    /// </param>
    Task<AndroidPolicyAssignmentDto> AssignAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default,
        Guid? actorUserId = null);

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