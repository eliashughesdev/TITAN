namespace TitanMDM.Application.Enrollment;

public sealed record ValidateEnrollmentTokenRequest(
    string Token,
    string Platform);