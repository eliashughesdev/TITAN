namespace TitanMDM.Application.Applications;

public interface IApplicationInventoryService
{
    Task ProcessInventoryAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DeviceApplicationDto>>
        GetDeviceApplicationsAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ApplicationSummaryDto>>
        GetApplicationsAsync(
            Guid organizationId,
            string? search,
            bool? systemApp,
            CancellationToken cancellationToken = default);
}

public sealed record DeviceApplicationDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string PackageName,
    string ApplicationName,
    string? VersionName,
    long VersionCode,
    bool IsSystemApp,
    bool IsEnabled,
    bool IsPresent,
    string? InstallerPackageName,
    DateTime? FirstInstallTimeUtc,
    DateTime? LastUpdateTimeUtc,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc);

public sealed record ApplicationSummaryDto(
    string PackageName,
    string ApplicationName,
    string? VersionName,
    long VersionCode,
    bool IsSystemApp,
    int DeviceCount,
    int EnabledCount,
    DateTime LastSeenAtUtc);