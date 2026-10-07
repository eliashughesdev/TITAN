namespace TitanMDM.Application.Enrollment;

public sealed record CreateEnrollmentTokenRequest(
    string Platform,
    int ExpirationMinutes,
    int MaxUses);