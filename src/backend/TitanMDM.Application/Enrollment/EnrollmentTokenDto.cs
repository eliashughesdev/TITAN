namespace TitanMDM.Application.Enrollment;

public sealed record EnrollmentTokenDto(
    Guid Id,
    string Platform,
    string Status,
    int MaxUses,
    int UsedCount,
    DateTime ExpiresAtUtc,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc);