namespace TitanMDM.Domain.Entities;

public sealed class DeviceLocation
{
    private DeviceLocation()
    {
    }

    public DeviceLocation(
        Guid organizationId,
        Guid deviceId,
        double latitude,
        double longitude,
        double? accuracyMeters,
        double? altitudeMeters,
        double? speedMetersPerSecond,
        string source,
        DateTime capturedAtUtc)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.");

        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(
                nameof(latitude));

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(
                nameof(longitude));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        AltitudeMeters = altitudeMeters;
        SpeedMetersPerSecond =
            speedMetersPerSecond;

        Source =
            string.IsNullOrWhiteSpace(source)
                ? "AndroidAgent"
                : source.Trim();

        CapturedAtUtc =
            capturedAtUtc == default
                ? DateTime.UtcNow
                : capturedAtUtc;

        ReceivedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DeviceId { get; private set; }

    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    public double? AccuracyMeters { get; private set; }
    public double? AltitudeMeters { get; private set; }
    public double? SpeedMetersPerSecond { get; private set; }

    public string Source { get; private set; } =
        string.Empty;

    public DateTime CapturedAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
}