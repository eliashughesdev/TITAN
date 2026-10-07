namespace TitanMDM.Application.Enrollment;

public interface IEnrollmentService
{
    Task<CreatedEnrollmentTokenDto>
        CreateTokenAsync(
            Guid organizationId,
            Guid createdByUserId,
            CreateEnrollmentTokenRequest request,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EnrollmentTokenDto>>
        GetTokensAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<bool> RevokeTokenAsync(
        Guid organizationId,
        Guid enrollmentTokenId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTokenValidationResult>
        ValidateTokenAsync(
            string token,
            string platform,
            CancellationToken cancellationToken = default);
}