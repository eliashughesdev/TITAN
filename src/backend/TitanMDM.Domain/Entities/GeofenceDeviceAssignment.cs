namespace TitanMDM.Domain.Entities;

public sealed class GeofenceDeviceAssignment
{
    private GeofenceDeviceAssignment()
    {
    }

    public GeofenceDeviceAssignment(
        Guid organizationId,
        Guid geofenceId,
        Guid deviceId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (geofenceId == Guid.Empty)
            throw new ArgumentException(
                "GeofenceId is required.");

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        GeofenceId = geofenceId;
        DeviceId = deviceId;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid GeofenceId { get; private set; }
    public Guid DeviceId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
}