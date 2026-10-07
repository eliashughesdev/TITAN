namespace TitanMDM.Application.Policies;

public interface IPolicyService
{
    Task<IReadOnlyCollection<PolicyDto>> GetAllAsync(
        Guid organizationId,
        string? platform,
        string? status,
        CancellationToken cancellationToken = default);

    Task<PolicyDetailsDto?> GetByIdAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task<PolicyDetailsDto> CreateAsync(
        Guid organizationId,
        Guid userId,
        CreatePolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<PolicyDetailsDto> UpdateAsync(
        Guid organizationId,
        Guid userId,
        Guid policyId,
        UpdatePolicyRequest request,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task DisableAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PolicyAssignmentDto>>
        AssignAsync(
            Guid organizationId,
            Guid userId,
            Guid policyId,
            AssignPolicyRequest request,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PolicyAssignmentDto>>
        GetAssignmentsAsync(
            Guid organizationId,
            Guid policyId,
            CancellationToken cancellationToken = default);
}