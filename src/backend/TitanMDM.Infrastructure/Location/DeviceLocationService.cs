using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Location;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Application.Automation;

namespace TitanMDM.Infrastructure.Location;

public sealed class DeviceLocationService
    : IDeviceLocationService
{
private readonly TitanMdmDbContext _db;

private readonly IAutomationEventDispatcher
    _automation;

public DeviceLocationService(
    TitanMdmDbContext db,
    IAutomationEventDispatcher automation)
{
    _db = db;
    _automation = automation;
}

    public async Task ProcessLocationAsync(
    Guid deviceId,
    string resultJson,
    CancellationToken cancellationToken = default)
{
    var device =
        await _db.Devices
            .SingleOrDefaultAsync(
                x =>
                    x.Id == deviceId &&
                    !x.IsDeleted,
                cancellationToken)
        ?? throw new InvalidOperationException(
            "Device not found.");

    var result =
        JsonSerializer.Deserialize<LocationResult>(
            resultJson,
            JsonOptions)
        ?? throw new InvalidOperationException(
            "Invalid location result.");

    ValidateCoordinates(
        result.Latitude,
        result.Longitude);

    var capturedAt =
        DateTime.TryParse(
            result.CapturedAtUtc,
            out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;

    var location =
        new DeviceLocation(
            device.OrganizationId,
            device.Id,
            result.Latitude,
            result.Longitude,
            result.AccuracyMeters,
            result.AltitudeMeters,
            result.SpeedMetersPerSecond,
            result.Source ??
                "AndroidAgent",
            capturedAt);

    _db.DeviceLocations.Add(
        location);

    var geofences =
        await (
            from assignment in
                _db.GeofenceDeviceAssignments

            join geofence in
                _db.Geofences

            on assignment.GeofenceId
                equals geofence.Id

            where
                assignment.DeviceId ==
                    device.Id &&
                assignment.OrganizationId ==
                    device.OrganizationId &&
                geofence.OrganizationId ==
                    device.OrganizationId &&
                geofence.IsEnabled

            select geofence
        )
        .ToListAsync(
            cancellationToken);

    var pendingEvents =
        new List<(
            GeofenceEvent Event,
            Geofence Geofence)>();

    foreach (var geofence in geofences)
    {
        var distanceMeters =
            CalculateDistanceMeters(
                result.Latitude,
                result.Longitude,
                geofence.Latitude,
                geofence.Longitude);

        var isInside =
            distanceMeters <=
            geofence.RadiusMeters;

        var state =
            await _db.GeofenceDeviceStates
                .SingleOrDefaultAsync(
                    x =>
                        x.GeofenceId ==
                            geofence.Id &&
                        x.DeviceId ==
                            device.Id,
                    cancellationToken);

        if (state is null)
        {
            state =
                new GeofenceDeviceState(
                    device.OrganizationId,
                    geofence.Id,
                    device.Id,
                    isInside,
                    distanceMeters,
                    capturedAt);

            _db.GeofenceDeviceStates.Add(
                state);

            // Primera observación:
            // si el dispositivo ya está dentro,
            // registramos una entrada inicial.
            if (
                isInside &&
                geofence.AlertOnEnter)
            {
                var geofenceEvent =
                    new GeofenceEvent(
                        device.OrganizationId,
                        geofence.Id,
                        device.Id,
                        "Enter",
                        result.Latitude,
                        result.Longitude,
                        distanceMeters,
                        capturedAt);

                _db.GeofenceEvents.Add(
                    geofenceEvent);

                pendingEvents.Add(
                    (
                        geofenceEvent,
                        geofence
                    ));
            }

            continue;
        }

        var wasInside =
            state.IsInside;

        var changed =
            state.Update(
                isInside,
                distanceMeters,
                capturedAt);

        if (!changed)
            continue;

        var eventType =
            isInside
                ? "Enter"
                : "Exit";

        if (
            eventType == "Enter" &&
            !geofence.AlertOnEnter)
        {
            continue;
        }

        if (
            eventType == "Exit" &&
            !geofence.AlertOnExit)
        {
            continue;
        }

        // Garantiza que la transición
        // corresponda realmente al estado anterior.
        if (
            isInside == wasInside)
        {
            continue;
        }

        var transitionEvent =
            new GeofenceEvent(
                device.OrganizationId,
                geofence.Id,
                device.Id,
                eventType,
                result.Latitude,
                result.Longitude,
                distanceMeters,
                capturedAt);

        _db.GeofenceEvents.Add(
            transitionEvent);

        pendingEvents.Add(
            (
                transitionEvent,
                geofence
            ));
    }

    // Primero persistimos ubicación,
    // estados y eventos.
    await _db.SaveChangesAsync(
        cancellationToken);

    // Después publicamos los eventos
    // al Automation Engine.
    foreach (
        var pending in
        pendingEvents)
    {
        var triggerType =
            pending.Event.EventType ==
                "Enter"
                ? "GeofenceEnter"
                : "GeofenceExit";

        await _automation.DispatchAsync(
            device.OrganizationId,
            device.Id,
            triggerType,
            new
            {
                platform =
                    device.Platform.ToString(),

                deviceName =
                    device.DeviceName,

                geofenceId =
                    pending.Geofence.Id,

                geofenceName =
                    pending.Geofence.Name,

                eventType =
                    pending.Event.EventType,

                latitude =
                    pending.Event.Latitude,

                longitude =
                    pending.Event.Longitude,

                distanceMeters =
                    pending.Event.DistanceMeters,

                radiusMeters =
                    pending.Geofence
                        .RadiusMeters,

                occurredAtUtc =
                    pending.Event
                        .OccurredAtUtc
            },
            cancellationToken:
                cancellationToken);
    }
}
    public async Task<DeviceLocationDto?>
        GetLatestAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        return await _db.DeviceLocations
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.DeviceId == deviceId)
            .OrderByDescending(
                x => x.CapturedAtUtc)
            .Select(x =>
                new DeviceLocationDto(
                    x.Id,
                    x.DeviceId,
                    x.Latitude,
                    x.Longitude,
                    x.AccuracyMeters,
                    x.AltitudeMeters,
                    x.SpeedMetersPerSecond,
                    x.Source,
                    x.CapturedAtUtc,
                    x.ReceivedAtUtc))
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public async Task<
        IReadOnlyCollection<DeviceLocationDto>>
        GetHistoryAsync(
            Guid organizationId,
            Guid deviceId,
            int limit,
            CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(
            limit,
            1,
            500);

        return await _db.DeviceLocations
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.DeviceId == deviceId)
            .OrderByDescending(
                x => x.CapturedAtUtc)
            .Take(limit)
            .Select(x =>
                new DeviceLocationDto(
                    x.Id,
                    x.DeviceId,
                    x.Latitude,
                    x.Longitude,
                    x.AccuracyMeters,
                    x.AltitudeMeters,
                    x.SpeedMetersPerSecond,
                    x.Source,
                    x.CapturedAtUtc,
                    x.ReceivedAtUtc))
            .ToListAsync(
                cancellationToken);
    }

    public async Task<
        IReadOnlyCollection<GeofenceDto>>
        GetGeofencesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        return await _db.Geofences
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId)
            .OrderBy(x => x.Name)
            .Select(x =>
                new GeofenceDto(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Latitude,
                    x.Longitude,
                    x.RadiusMeters,
                    x.AlertOnEnter,
                    x.AlertOnExit,
                    x.IsEnabled,
                    _db.GeofenceDeviceAssignments
                        .Count(a =>
                            a.GeofenceId ==
                            x.Id),
                    x.CreatedAtUtc))
            .ToListAsync(
                cancellationToken);
    }

    public async Task<GeofenceDto>
        CreateGeofenceAsync(
            Guid organizationId,
            CreateGeofenceRequest request,
            CancellationToken cancellationToken = default)
    {
        var geofence =
            new Geofence(
                organizationId,
                request.Name,
                request.Description,
                request.Latitude,
                request.Longitude,
                request.RadiusMeters,
                request.AlertOnEnter,
                request.AlertOnExit);

        _db.Geofences.Add(geofence);

        await _db.SaveChangesAsync(
            cancellationToken);

        return new GeofenceDto(
            geofence.Id,
            geofence.Name,
            geofence.Description,
            geofence.Latitude,
            geofence.Longitude,
            geofence.RadiusMeters,
            geofence.AlertOnEnter,
            geofence.AlertOnExit,
            geofence.IsEnabled,
            0,
            geofence.CreatedAtUtc);
    }

    public async Task DeleteGeofenceAsync(
        Guid organizationId,
        Guid geofenceId,
        CancellationToken cancellationToken = default)
    {
        var geofence =
            await _db.Geofences
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == geofenceId &&
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Geofence not found.");

        _db.Geofences.Remove(geofence);

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    public async Task AssignDevicesAsync(
        Guid organizationId,
        Guid geofenceId,
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default)
    {
        var exists =
            await _db.Geofences.AnyAsync(
                x =>
                    x.Id == geofenceId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (!exists)
            throw new InvalidOperationException(
                "Geofence not found.");

        var valid =
            await _db.Devices
                .AsNoTracking()
                .Where(x =>
                    deviceIds.Contains(x.Id) &&
                    x.OrganizationId ==
                        organizationId &&
                    !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(
                    cancellationToken);

        var existing =
            await _db
                .GeofenceDeviceAssignments
                .AsNoTracking()
                .Where(x =>
                    x.GeofenceId ==
                    geofenceId)
                .Select(x => x.DeviceId)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var deviceId in
            valid.Except(existing))
        {
            _db.GeofenceDeviceAssignments.Add(
                new GeofenceDeviceAssignment(
                    organizationId,
                    geofenceId,
                    deviceId));
        }

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    private static void ValidateCoordinates(
    double latitude,
    double longitude)
{
    if (
        latitude < -90 ||
        latitude > 90)
    {
        throw new InvalidOperationException(
            "Latitude must be between -90 and 90.");
    }

    if (
        longitude < -180 ||
        longitude > 180)
    {
        throw new InvalidOperationException(
            "Longitude must be between -180 and 180.");
    }
}

private static double
    CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
{
    const double EarthRadiusMeters =
        6371000d;

    var latitude1Radians =
        DegreesToRadians(
            latitude1);

    var latitude2Radians =
        DegreesToRadians(
            latitude2);

    var latitudeDifference =
        DegreesToRadians(
            latitude2 -
            latitude1);

    var longitudeDifference =
        DegreesToRadians(
            longitude2 -
            longitude1);

    var a =
        Math.Sin(
            latitudeDifference / 2) *
        Math.Sin(
            latitudeDifference / 2) +

        Math.Cos(
            latitude1Radians) *

        Math.Cos(
            latitude2Radians) *

        Math.Sin(
            longitudeDifference / 2) *

        Math.Sin(
            longitudeDifference / 2);

    var c =
        2 *
        Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

    return EarthRadiusMeters * c;
}

private static double
    DegreesToRadians(
        double degrees)
{
    return degrees *
        Math.PI /
        180d;
}

    private static readonly
        JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };

    private sealed record LocationResult(
        double Latitude,
        double Longitude,
        double? AccuracyMeters,
        double? AltitudeMeters,
        double? SpeedMetersPerSecond,
        string? Source,
        string? CapturedAtUtc);
}