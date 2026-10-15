namespace TitanMDM.Application.AndroidEnterprise;

public interface IAndroidDeviceSyncService
{
    Task<AndroidDeviceSyncResultDto> SynchronizeAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<AndroidDeviceInventorySummaryDto> GetSummaryAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default);

    Task<AndroidDeviceInventoryPageDto> GetInventoryPageAsync(
        Guid organizationId,
        string? search,
        int page,
        int pageSize,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default);
}

public sealed record AndroidDeviceSyncResultDto(
    int ReceivedFromGoogle,
    int Created,
    int Updated,
    int MarkedMissing,
    int Failed,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    IReadOnlyCollection<string> Errors);

public sealed record AndroidDeviceInventorySummaryDto(
    int Total,
    int Managed,
    int MissingInGoogle,
    int FullyManaged,
    int Dedicated,
    int WorkProfile,
    int Compliant,
    int NonCompliant,
    DateTime? LastSynchronizationUtc);

public sealed record AndroidInventoryItemDto(
    Guid DeviceId,
    string DeviceName,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string Status,
    string ComplianceStatus,
    bool IsManaged,
    DateTime? LastSeenAtUtc,
    string? ManagementMode,
    string? Ownership,
    string? State,
    string? AppliedPolicyName,
    long? AppliedPolicyVersion,
    string? AppliedPolicyState,
    DateTime? LastPolicySyncTimeUtc,
    DateTime LastSynchronizedAtUtc,
    bool IsDeletedInGoogle);

public sealed record AndroidDeviceInventoryPageDto(
    IReadOnlyCollection<AndroidInventoryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);