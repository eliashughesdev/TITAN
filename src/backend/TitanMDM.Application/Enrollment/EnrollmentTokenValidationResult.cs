namespace TitanMDM.Application.Enrollment;

public sealed record EnrollmentTokenValidationResult(
    bool IsValid,
    Guid? EnrollmentTokenId,
    Guid? OrganizationId,
    string? Platform,
    string? ErrorCode,
    string? Message);