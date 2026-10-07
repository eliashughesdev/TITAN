namespace TitanMDM.Application.Commands;

public sealed record DeviceCommandDto(
    Guid Id,
    Guid OrganizationId,
    Guid DeviceId,
    string CommandType,
    string PayloadJson,
    string Status,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? QueuedAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    int DeliveryAttempts);