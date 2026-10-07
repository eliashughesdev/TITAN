namespace TitanMDM.Application.Devices;

public interface IDeviceQueryService
{
    Task<DeviceListResultDto> GetDevicesAsync(
        Guid organizationId,
        string? search,
        string? platform,
        string? status,
        string? compliance,
        bool? managed,
        string? sortBy,
        string? sortDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DeviceDetailsDto?> GetDeviceByIdAsync(
        Guid organizationId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<AndroidDeviceDetailsDto?>
        GetAndroidDeviceDetailsAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default);

    Task<DeviceOperationalSnapshotDto?>
        GetOperationalSnapshotAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default);
}