namespace TitanMDM.Domain.Entities;

public sealed class Geofence
{
    private Geofence()
    {
    }

    public Geofence(
        Guid organizationId,
        string name,
        string? description,
        double latitude,
        double longitude,
        double radiusMeters,
        bool alertOnEnter,
        bool alertOnExit)
    {
        Validate(
            organizationId,
            name,
            latitude,
            longitude,
            radiusMeters);

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = Normalize(description);
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        AlertOnEnter = alertOnEnter;
        AlertOnExit = alertOnExit;
        IsEnabled = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } =
        string.Empty;

    public string? Description { get; private set; }

    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double RadiusMeters { get; private set; }

    public bool AlertOnEnter { get; private set; }
    public bool AlertOnExit { get; private set; }
    public bool IsEnabled { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string? description,
        double latitude,
        double longitude,
        double radiusMeters,
        bool alertOnEnter,
        bool alertOnExit,
        bool isEnabled)
    {
        Validate(
            OrganizationId,
            name,
            latitude,
            longitude,
            radiusMeters);

        Name = name.Trim();
        Description = Normalize(description);
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        AlertOnEnter = alertOnEnter;
        AlertOnExit = alertOnExit;
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void Validate(
        Guid organizationId,
        string name,
        double latitude,
        double longitude,
        double radiusMeters)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Name is required.");

        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(
                nameof(latitude));

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(
                nameof(longitude));

        if (radiusMeters < 25 ||
            radiusMeters > 100000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters),
                "Radius must be between 25 and 100000 meters.");
        }
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}