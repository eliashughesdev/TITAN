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