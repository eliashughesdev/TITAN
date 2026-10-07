namespace TitanMDM.Application.Enrollment;

public sealed record CreatedEnrollmentTokenDto(
    Guid Id,
    string Token,
    string Platform,
    string Status,
    int MaxUses,
    int UsedCount,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc);