namespace TitanMDM.Application.Audit;

public sealed record AuditEventDto(
    Guid Id,
    string EventType,
    string Module,
    string Action,
    string Status,
    Guid? ActorUserId,
    string ActorName,
    string ActorEmail,
    Guid? DeviceId,
    string DeviceName,
    string Platform,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    long? DurationMilliseconds,
    int DeliveryAttempts,
    string? ErrorCode,
    string? ErrorMessage,
    string? PayloadJson,
    string? ResultJson);

public sealed record AuditListResultDto(
    IReadOnlyCollection<AuditEventDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AuditSummaryDto(
    int TotalEvents,
    int SuccessfulEvents,
    int FailedEvents,
    int ActiveEvents,
    int EventsLast24Hours,
    int UniqueActors,
    int UniqueDevices);

public sealed record AuditFilterOptionsDto(
    IReadOnlyCollection<string> Modules,
    IReadOnlyCollection<string> Actions,
    IReadOnlyCollection<string> Statuses);

public sealed record AuditQueryDto(
    string? Search,
    string? Module,
    string? Action,
    string? Status,
    Guid? UserId,
    Guid? DeviceId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 50);