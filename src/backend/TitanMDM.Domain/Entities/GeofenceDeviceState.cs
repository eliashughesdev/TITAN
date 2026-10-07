namespace TitanMDM.Domain.Entities;

public sealed class GeofenceDeviceState
{
    private GeofenceDeviceState()
    {
    }

    public GeofenceDeviceState(
        Guid organizationId,
        Guid geofenceId,
        Guid deviceId,
        bool isInside,
        double distanceMeters,
        DateTime evaluatedAtUtc)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        GeofenceId = geofenceId;
        DeviceId = deviceId;
        IsInside = isInside;
        DistanceMeters = distanceMeters;
        LastEvaluatedAtUtc = evaluatedAtUtc;
        LastTransitionAtUtc = evaluatedAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid GeofenceId { get; private set; }

    public Guid DeviceId { get; private set; }

    public bool IsInside { get; private set; }

    public double DistanceMeters { get; private set; }

    public DateTime LastEvaluatedAtUtc { get; private set; }

    public DateTime LastTransitionAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool Update(
        bool isInside,
        double distanceMeters,
        DateTime evaluatedAtUtc)
    {
        var changed =
            IsInside != isInside;

        IsInside = isInside;
        DistanceMeters = distanceMeters;
        LastEvaluatedAtUtc = evaluatedAtUtc;

        if (changed)
        {
            LastTransitionAtUtc =
                evaluatedAtUtc;
        }

        UpdatedAtUtc = DateTime.UtcNow;

        return changed;
    }
}