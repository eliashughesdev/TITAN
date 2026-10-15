namespace TitanMDM.Application.AndroidEnterprise;

public sealed record AndroidEnterpriseStatusDto(
    bool IsConfigured,
    bool CanAuthenticate,
    bool HasPublicCallback,
    string GoogleProjectId,
    string? EnterpriseName,
    string? EnterpriseDisplayName,
    string Status,
    DateTime? ConnectedAtUtc,
    string? LastError);

public sealed record AndroidSignupResponse(
    string SignupUrl,
    DateTime ExpiresAtUtc);

public sealed record CreateAndroidEnrollmentRequest(
    string Mode,
    int ExpirationMinutes = 60,
    Guid? PolicyId = null);

public sealed record AndroidEnrollmentDto(
    Guid Id,
    string Mode,
    string GoogleEnrollmentTokenName,
    Guid? PolicyId,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    bool IsRevoked,
    bool IsExpired);

public sealed record CreatedAndroidEnrollmentDto(
    Guid Id,
    string Mode,
    string GoogleEnrollmentTokenName,
    string EnrollmentToken,
    string QrCode,
    Guid? PolicyId,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc);

public sealed record AndroidAuditEventDto(
    Guid Id,
    string Action,
    string TargetType,
    string? TargetId,
    string Result,
    string ActorName,
    string? ActorEmail,
    string? CorrelationId,
    string? IpAddress,
    DateTime CreatedAtUtc);

public sealed record AndroidAuditPageDto(
    IReadOnlyCollection<AndroidAuditEventDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record CreateAndroidWebTokenRequest(
    string? IframeOrigin = null,
    string? ReturnUrl = null,
    string? Target = null);

public sealed record AndroidWebTokenDto(
    string Token,
    string Url,
    DateTime? ExpirationTimeUtc,
    DateTime CreatedAtUtc);
