namespace TitanMDM.Application.Android.Policies;

public sealed record AndroidPolicyPublicationDto(
    Guid Id,
    Guid PolicyId,
    Guid PolicyVersionId,
    int PolicyVersion,
    string GooglePolicyId,
    string? GooglePolicyName,
    string Status,
    string? CompiledPolicyJson,
    string? GoogleResponseJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? DeletedAtUtc);