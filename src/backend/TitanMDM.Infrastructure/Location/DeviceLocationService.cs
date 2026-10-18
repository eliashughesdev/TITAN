using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Location;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Location;

public sealed class DeviceLocationService
    : IDeviceLocationService
{
    /// <summary>
    /// Umbral de "posicion antigua". Un dispositivo con
    /// una captura mas reciente que esto se considera Fresh.
    /// </summary>
    public const int StaleSignalThresholdMinutes = 60;

    private readonly TitanMdmDbContext _db;

    private readonly IAutomationEventDispatcher
        _automation;

    private readonly ILocationUpdateNotifier?
        _notifier;

    public DeviceLocationService(
        TitanMdmDbContext db,
        IAutomationEventDispatcher automation,
        ILocationUpdateNotifier? notifier = null)
    {
        _db = db;
        _automation = automation;
        _notifier = notifier;
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

        /*
         * Reporte de estado sin coordenadas
         * (permiso denegado / sin fix).
         * No inventamos posiciones: solo registramos
         * el estado de captura.
         */
        if (result.Latitude is null ||
            result.Longitude is null)
        {
            var captureState =
                ParseCaptureState(
                    result.Status);

            await UpsertLocationStateAsync(
                device,
                captureState,
                DateTime.UtcNow,
                result.Source,
                cancellationToken);

            await _db.SaveChangesAsync(
                cancellationToken);

            await NotifyAsync(
                device,
                captureState.ToString(),
                null,
                cancellationToken);

            return;
        }

        var latitude =
            result.Latitude.Value;

        var longitude =
            result.Longitude.Value;

        ValidateCoordinates(
            latitude,
            longitude);

        var capturedAt =
            DateTime.TryParse(
                result.CapturedAtUtc,
                out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;

        /*
         * Fuentes proactivas respetan la politica de la
         * organizacion. Las solicitudes on-demand del
         * tecnico se procesan siempre.
         */
        if (IsProactiveSource(result.Source))
        {
            var policy =
                await _db.LocationReportingPolicies
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                device.OrganizationId,
                        cancellationToken);

            if (policy is null || !policy.IsEnabled)
            {
                return;
            }
        }

        var location =
            new DeviceLocation(
                device.OrganizationId,
                device.Id,
                latitude,
                longitude,
                result.AccuracyMeters,
                result.AltitudeMeters,
                result.SpeedMetersPerSecond,
                result.Source ??
                    "AndroidAgent",
                capturedAt);

        _db.DeviceLocations.Add(
            location);

        await UpsertLocationStateAsync(
            device,
            DeviceCaptureState.Reported,
            DateTime.UtcNow,
            result.Source,
            cancellationToken);

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
                    latitude,
                    longitude,
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
                            latitude,
                            longitude,
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
                    latitude,
                    longitude,
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

        await _db.SaveChangesAsync(
            cancellationToken);

        await NotifyAsync(
            device,
            DeviceCaptureState.Reported.ToString(),
            capturedAt,
            cancellationToken);

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
        IReadOnlyCollection<FleetLocationDto>>
        GetFleetLocationsAsync(
            Guid organizationId,
            FleetLocationFilters filters,
            CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var statusFilter =
            ParseDeviceStatus(
                filters.Status);

        var query =
            from d in _db.Devices

            let latestLocation =
                _db.DeviceLocations
                    .Where(x =>
                        x.DeviceId ==
                            d.Id &&
                        x.OrganizationId ==
                            organizationId)
                    .OrderByDescending(
                        x => x.CapturedAtUtc)
                    .FirstOrDefault()

            let locationState =
                _db.DeviceLocationStates
                    .Where(x =>
                        x.DeviceId ==
                            d.Id &&
                        x.OrganizationId ==
                            organizationId)
                    .OrderByDescending(
                        x => x.UpdatedAtUtc)
                    .FirstOrDefault()

            let siteName =
                _db.SiteLocations
                    .Where(x =>
                        x.Id ==
                            d.SiteLocationId)
                    .Select(x => x.Name)
                    .FirstOrDefault()

            where
                d.OrganizationId ==
                    organizationId &&
                !d.IsDeleted

            select new
            {
                Device = d,
                LatestLocation = latestLocation,
                LocationState = locationState,
                SiteName = siteName
            };

        if (!string.IsNullOrWhiteSpace(
                filters.Search))
        {
            var search =
                filters.Search.ToLower();

            query =
                query.Where(x =>
                    x.Device.DeviceName
                        .ToLower()
                        .Contains(search) ||
                    (x.Device.Manufacturer ?? "")
                        .ToLower()
                        .Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(
                filters.Manufacturer))
        {
            query =
                query.Where(x =>
                    x.Device.Manufacturer ==
                        filters.Manufacturer);
        }

        if (!string.IsNullOrWhiteSpace(
                filters.Department))
        {
            query =
                query.Where(x =>
                    x.Device.Department ==
                        filters.Department);
        }

        if (filters.SiteId.HasValue)
        {
            query =
                query.Where(x =>
                    x.Device.SiteLocationId ==
                        filters.SiteId);
        }

        if (filters.DeviceGroupId.HasValue)
        {
            query =
                query.Where(x =>
                    _db.DeviceGroupMembers
                        .Any(m =>
                            m.DeviceId ==
                                x.Device.Id &&
                            m.GroupId ==
                                filters.DeviceGroupId));
        }

        if (statusFilter.HasValue)
        {
            query =
                query.Where(x =>
                    x.Device.Status ==
                        statusFilter);
        }

        var rows =
            await query.ToListAsync(
                cancellationToken);

        var result =
            rows.Select(x =>
                    BuildFleetLocation(
                        x.Device,
                        x.LatestLocation,
                        x.LocationState,
                        x.SiteName,
                        now))
                .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(
                filters.LocationState))
        {
            result =
                result.Where(x =>
                    string.Equals(
                        x.LocationState,
                        filters.LocationState,
                        StringComparison
                            .OrdinalIgnoreCase));
        }

        if (filters.MaxSignalAgeMinutes.HasValue)
        {
            var maxAge =
                filters.MaxSignalAgeMinutes
                    .Value;

            result =
                result.Where(x =>
                    x.SignalAgeMinutes is null
                        ? false
                        : x.SignalAgeMinutes
                            .Value <= maxAge);
        }

        return result
            .OrderBy(x => x.DeviceName)
            .ToList();
    }

    public async Task<LocationReportingPolicyDto?>
        GetReportingPolicyAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        return await _db.LocationReportingPolicies
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId)
            .Select(x =>
                new LocationReportingPolicyDto(
                    x.Id,
                    x.IsEnabled,
                    x.ReportingIntervalMinutes,
                    x.RetentionDays))
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public async Task<LocationReportingPolicyDto>
        UpdateReportingPolicyAsync(
            Guid organizationId,
            UpdateReportingPolicyRequest request,
            CancellationToken cancellationToken = default)
    {
        var policy =
            await _db.LocationReportingPolicies
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (policy is null)
        {
            policy =
                new LocationReportingPolicy(
                    organizationId,
                    request.IsEnabled,
                    request.ReportingIntervalMinutes,
                    request.RetentionDays);

            _db.LocationReportingPolicies.Add(
                policy);
        }
        else
        {
            policy.Update(
                request.IsEnabled,
                request.ReportingIntervalMinutes,
                request.RetentionDays);
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        return new LocationReportingPolicyDto(
            policy.Id,
            policy.IsEnabled,
            policy.ReportingIntervalMinutes,
            policy.RetentionDays);
    }

    public async Task PurgeOldLocationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var policy =
            await _db.LocationReportingPolicies
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (policy is null || !policy.IsEnabled)
            return;

        var cutoff =
            DateTime.UtcNow.AddDays(
                -policy.RetentionDays);

        var oldLocations =
            _db.DeviceLocations.Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.CapturedAtUtc <
                    cutoff);

        _db.DeviceLocations.RemoveRange(
            oldLocations);

        await _db.SaveChangesAsync(
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

        _db.Geofences.Add(
            geofence);

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

        _db.Geofences.Remove(
            geofence);

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

    // ============================================================
    // HELPERS
    // ============================================================

    private static FleetLocationDto
        BuildFleetLocation(
            Device device,
            DeviceLocation? latestLocation,
            DeviceLocationState? locationState,
            string? siteName,
            DateTime now)
    {
        var signalAgeMinutes =
            latestLocation is null
                ? (double?)null
                : (now -
                    latestLocation.CapturedAtUtc)
                .TotalMinutes;

        var locationStateText =
            ResolveLocationState(
                device,
                latestLocation,
                locationState,
                signalAgeMinutes);

        return new FleetLocationDto(
            device.Id,
            device.DeviceName,
            device.Manufacturer,
            device.Model,
            device.Department,
            device.SiteLocationId,
            siteName,
            latestLocation?.Latitude,
            latestLocation?.Longitude,
            latestLocation?.AccuracyMeters,
            latestLocation?.CapturedAtUtc,
            latestLocation?.ReceivedAtUtc,
            device.Status.ToString(),
            locationStateText,
            signalAgeMinutes,
            device.BatteryLevel,
            device.LastSeenAtUtc);
    }

    public static string ResolveLocationState(
        Device device,
        DeviceLocation? latestLocation,
        DeviceLocationState? locationState,
        double? signalAgeMinutes)
    {
        if (
            locationState?.CaptureState ==
                DeviceCaptureState
                    .PermissionDenied)
        {
            return "PermissionDenied";
        }

        if (latestLocation is null)
        {
            return "NoLocation";
        }

        if (
            device.Status ==
                DeviceStatus.Offline ||
            device.Status ==
                DeviceStatus.Retired)
        {
            return "Offline";
        }

        if (
            signalAgeMinutes.HasValue &&
            signalAgeMinutes.Value >
                StaleSignalThresholdMinutes)
        {
            return "Stale";
        }

        return "Fresh";
    }

    private async Task UpsertLocationStateAsync(
        Device device,
        DeviceCaptureState captureState,
        DateTime updatedAtUtc,
        string? source,
        CancellationToken cancellationToken)
    {
        var existing =
            await _db.DeviceLocationStates
                .SingleOrDefaultAsync(
                    x =>
                        x.DeviceId ==
                            device.Id &&
                        x.OrganizationId ==
                            device.OrganizationId,
                    cancellationToken);

        if (existing is null)
        {
            _db.DeviceLocationStates.Add(
                new DeviceLocationState(
                    device.OrganizationId,
                    device.Id,
                    captureState,
                    updatedAtUtc,
                    source));

            return;
        }

        existing.Update(
            captureState,
            updatedAtUtc,
            source);
    }

    private async Task NotifyAsync(
        Device device,
        string locationState,
        DateTime? capturedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_notifier is null)
            return;

        try
        {
            await _notifier
                .NotifyLocationUpdatedAsync(
                    device.OrganizationId,
                    device.Id,
                    locationState,
                    capturedAtUtc,
                    cancellationToken);
        }
        catch
        {
            /*
             * La notificacion en vivo es best-effort:
             * nunca debe impedir persistir la posicion.
             */
        }
    }

    private static bool IsProactiveSource(
        string? source)
    {
        return !string.IsNullOrWhiteSpace(source) &&
            source.Contains(
                "Proactive",
                StringComparison
                    .OrdinalIgnoreCase);
    }

    private static DeviceCaptureState
        ParseCaptureState(
            string? status)
    {
        if (string.Equals(
                status,
                "PERMISSION_DENIED",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return DeviceCaptureState
                .PermissionDenied;
        }

        if (string.Equals(
                status,
                "UNAVAILABLE",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return DeviceCaptureState
                .Unavailable;
        }

        if (string.Equals(
                status,
                "DISABLED",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return DeviceCaptureState
                .DisabledByPolicy;
        }

        return DeviceCaptureState.Unknown;
    }

    private static DeviceStatus?
        ParseDeviceStatus(
            string? status)
    {
        return Enum.TryParse<DeviceStatus>(
            status,
            ignoreCase: true,
            out var parsed)
                ? parsed
                : null;
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
        double? Latitude,
        double? Longitude,
        double? AccuracyMeters,
        double? AltitudeMeters,
        double? SpeedMetersPerSecond,
        string? Source,
        string? CapturedAtUtc,
        string? Status);
}
