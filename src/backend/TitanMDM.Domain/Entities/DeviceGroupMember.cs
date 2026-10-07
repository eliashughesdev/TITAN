namespace TitanMDM.Domain.Entities;

public sealed class DeviceGroupMember
{
    private DeviceGroupMember()
    {
    }

    public DeviceGroupMember(
        Guid organizationId,
        Guid groupId,
        Guid deviceId,
        string source = "Manual")
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (groupId == Guid.Empty)
            throw new ArgumentException(
                "GroupId is required.");

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.");

        Id = Guid.NewGuid();

        OrganizationId =
            organizationId;

        GroupId =
            groupId;

        DeviceId =
            deviceId;

        Source =
            string.IsNullOrWhiteSpace(source)
                ? "Manual"
                : source.Trim();

        AddedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid GroupId
    {
        get;
        private set;
    }

    public Guid DeviceId
    {
        get;
        private set;
    }

    public string Source
    {
        get;
        private set;
    } = "Manual";

    public DateTime AddedAtUtc
    {
        get;
        private set;
    }
}