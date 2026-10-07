namespace TitanMDM.Application.AndroidEnterprise;

public interface IAndroidEnterpriseService
{
    Task<AndroidEnterpriseStatusDto> GetStatusAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<AndroidSignupResponse> CreateSignupUrlAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task CompleteSignupAsync(
        string state,
        string enterpriseToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AndroidEnrollmentDto>>
        GetEnrollmentsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<CreatedAndroidEnrollmentDto>
        CreateEnrollmentAsync(
            Guid organizationId,
            Guid userId,
            CreateAndroidEnrollmentRequest request,
            CancellationToken cancellationToken = default);

    Task<bool> RevokeEnrollmentAsync(
        Guid organizationId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);
}