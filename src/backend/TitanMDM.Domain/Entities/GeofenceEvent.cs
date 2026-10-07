namespace TitanMDM.Domain.Entities;

public sealed class GeofenceEvent
{
    private GeofenceEvent()
    {
    }

    public GeofenceEvent(
        Guid organizationId,
        Guid geofenceId,
        Guid deviceId,
        string eventType,
        double latitude,
        double longitude,
        double distanceMeters,
        DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException(
                "EventType is required.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        GeofenceId = geofenceId;
        DeviceId = deviceId;
        EventType = eventType.Trim();
        Latitude = latitude;
        Longitude = longitude;
        DistanceMeters = distanceMeters;
        OccurredAtUtc = occurredAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid GeofenceId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string EventType { get; private set; } =
        string.Empty;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public double DistanceMeters { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}