namespace TitanMDM.Application.Location;

public interface IDeviceLocationService
{
    Task ProcessLocationAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task<DeviceLocationDto?>
        GetLatestAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DeviceLocationDto>>
        GetHistoryAsync(
            Guid organizationId,
            Guid deviceId,
            int limit,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FleetLocationDto>>
        GetFleetLocationsAsync(
            Guid organizationId,
            FleetLocationFilters filters,
            CancellationToken cancellationToken = default);

    Task<LocationReportingPolicyDto?>
        GetReportingPolicyAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<LocationReportingPolicyDto>
        UpdateReportingPolicyAsync(
            Guid organizationId,
            UpdateReportingPolicyRequest request,
            CancellationToken cancellationToken = default);

    Task PurgeOldLocationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GeofenceDto>>
        GetGeofencesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<GeofenceDto>
        CreateGeofenceAsync(
            Guid organizationId,
            CreateGeofenceRequest request,
            CancellationToken cancellationToken = default);

    Task DeleteGeofenceAsync(
        Guid organizationId,
        Guid geofenceId,
        CancellationToken cancellationToken = default);

    Task AssignDevicesAsync(
        Guid organizationId,
        Guid geofenceId,
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default);
}

public sealed record DeviceLocationDto(
    Guid Id,
    Guid DeviceId,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    double? AltitudeMeters,
    double? SpeedMetersPerSecond,
    string Source,
    DateTime CapturedAtUtc,
    DateTime ReceivedAtUtc);

public sealed record FleetLocationDto(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    string? Manufacturer,
    string? Model,
    string? Department,
    Guid? SiteId,
    string? SiteName,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTime? CapturedAtUtc,
    DateTime? ReceivedAtUtc,
    string Status,
    string LocationState,
    double? SignalAgeMinutes,
    int? BatteryLevel,
    DateTime? LastSeenAtUtc);

public sealed record FleetLocationFilters(
    string? Search,
    string? Manufacturer,
    string? Department,
    Guid? DeviceGroupId,
    Guid? SiteId,
    string? Status,
    string? LocationState,
    int? MaxSignalAgeMinutes);

public sealed record LocationReportingPolicyDto(
    Guid Id,
    bool IsEnabled,
    int ReportingIntervalMinutes,
    int RetentionDays);

public sealed record UpdateReportingPolicyRequest(
    bool IsEnabled,
    int ReportingIntervalMinutes,
    int RetentionDays);

public sealed record GeofenceDto(
    Guid Id,
    string Name,
    string? Description,
    double Latitude,
    double Longitude,
    double RadiusMeters,
    bool AlertOnEnter,
    bool AlertOnExit,
    bool IsEnabled,
    int AssignedDevices,
    DateTime CreatedAtUtc);

public sealed record CreateGeofenceRequest(
    string Name,
    string? Description,
    double Latitude,
    double Longitude,
    double RadiusMeters,
    bool AlertOnEnter,
    bool AlertOnExit);

public sealed record AssignGeofenceDevicesRequest(
    IReadOnlyCollection<Guid> DeviceIds);
