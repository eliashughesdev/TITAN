namespace TitanMDM.Application.AndroidEnterprise;

public interface IAndroidDeviceSyncService
{
    Task<AndroidDeviceSyncResultDto> SynchronizeAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<AndroidDeviceInventorySummaryDto> GetSummaryAsync(
        Guid organizationId,
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