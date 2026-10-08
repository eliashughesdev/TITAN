using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class Device
{
    private Device()
    {
    }

    public Device(
        Guid organizationId,
        string deviceName,
        DevicePlatform platform,
        string serialNumber)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException(
                "Device name is required.",
                nameof(deviceName));

        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException(
                "Serial number is required.",
                nameof(serialNumber));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceName = deviceName.Trim();
        Platform = platform;
        SerialNumber = serialNumber.Trim();

        Status = DeviceStatus.Pending;
        ComplianceStatus = ComplianceStatus.Unknown;

        IsManaged = false;
        IsDeleted = false;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? SiteId
    {
        get;
        private set;
    }

    public Guid? SiteLocationId
    {
        get;
        private set;
    }

    public string DeviceName { get; private set; } =
        string.Empty;

    public DevicePlatform Platform { get; private set; }

    public DeviceStatus Status { get; private set; }

    public ComplianceStatus ComplianceStatus { get; private set; }

    public string SerialNumber { get; private set; } =
        string.Empty;

    public string? Imei { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? Model { get; private set; }

    public string? OperatingSystem { get; private set; }

    public string? OperatingSystemVersion { get; private set; }

    public string? AgentVersion { get; private set; }

    public string? IpAddress { get; private set; }

    public string? MacAddress { get; private set; }

    public string? AssignedUser { get; private set; }

    public string? Department { get; private set; }

    public int? BatteryLevel { get; private set; }

    public bool IsManaged { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? EnrolledAtUtc { get; private set; }

    public DateTime? LastSeenAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void CompleteEnrollment()
    {
        IsManaged = true;
        Status = DeviceStatus.Online;

        EnrolledAtUtc ??= DateTime.UtcNow;

        LastSeenAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RegisterHeartbeat(
        string? ipAddress,
        int? batteryLevel)
    {
        IpAddress = Normalize(ipAddress);

        if (batteryLevel.HasValue)
        {
            BatteryLevel = Math.Clamp(
                batteryLevel.Value,
                0,
                100);
        }

        LastSeenAtUtc = DateTime.UtcNow;
        Status = DeviceStatus.Online;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateInventory(
        string? manufacturer,
        string? model,
        string? operatingSystem,
        string? operatingSystemVersion,
        string? agentVersion,
        string? imei,
        string? macAddress)
    {
        Manufacturer = Normalize(manufacturer);
        Model = Normalize(model);
        OperatingSystem = Normalize(operatingSystem);
        OperatingSystemVersion =
            Normalize(operatingSystemVersion);

        AgentVersion = Normalize(agentVersion);
        Imei = Normalize(imei);
        MacAddress = Normalize(macAddress);

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SynchronizeAndroidEnterprise(
        string? deviceName,
        string? manufacturer,
        string? model,
        string? operatingSystemVersion,
        string? androidDevicePolicyVersion,
        string? imei,
        string? macAddress,
        DateTime? enrollmentTimeUtc,
        DateTime? lastStatusReportTimeUtc,
        bool isManaged)
    {
        if (Platform != DevicePlatform.Android)
        {
            throw new InvalidOperationException(
                "Android Enterprise synchronization " +
                "can only update Android devices.");
        }

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            DeviceName = deviceName.Trim();
        }

        Manufacturer = Normalize(manufacturer);
        Model = Normalize(model);

        OperatingSystem = "Android";

        OperatingSystemVersion =
            Normalize(operatingSystemVersion);

        AgentVersion =
            Normalize(androidDevicePolicyVersion);

        Imei = Normalize(imei);
        MacAddress = Normalize(macAddress);

        IsManaged = isManaged;
        IsDeleted = false;

        if (enrollmentTimeUtc.HasValue)
        {
            EnrolledAtUtc =
                NormalizeUtc(enrollmentTimeUtc);
        }
        else if (isManaged)
        {
            EnrolledAtUtc ??= DateTime.UtcNow;
        }

        if (lastStatusReportTimeUtc.HasValue)
        {
            LastSeenAtUtc =
                NormalizeUtc(lastStatusReportTimeUtc);
        }

        if (isManaged)
        {
            Status = DeviceStatus.Online;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkOffline()
    {
        if (
            Status == DeviceStatus.Wiped ||
            Status == DeviceStatus.Retired ||
            Status == DeviceStatus.Quarantined)
        {
            return;
        }

        Status =
            DeviceStatus.Offline;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkAndroidMissing()
    {
        if (Platform != DevicePlatform.Android)
            return;

        IsManaged = false;

        if (Status != DeviceStatus.Wiped &&
            Status != DeviceStatus.Retired)
        {
            Status = DeviceStatus.Offline;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AssignUser(
        string? assignedUser,
        string? department)
    {
        AssignedUser = Normalize(assignedUser);
        Department = Normalize(department);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetCompliance(
        ComplianceStatus complianceStatus)
    {
        ComplianceStatus = complianceStatus;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Quarantine()
    {
        Status = DeviceStatus.Quarantined;
        ComplianceStatus =
            ComplianceStatus.Quarantined;

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Retire()
    {
        Status = DeviceStatus.Retired;
        IsManaged = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime? NormalizeUtc(
        DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc =>
                value.Value,

            DateTimeKind.Local =>
                value.Value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value.Value,
                    DateTimeKind.Utc)
        };
    }

    public void AssignSite(
    Guid? siteId,
    Guid? siteLocationId = null)
    {
        if (
            !siteId.HasValue
            &&
            siteLocationId.HasValue)
        {
            throw new InvalidOperationException(
                "No se puede asignar una ubicación sin una localidad.");
        }

        SiteId =
            siteId;

        SiteLocationId =
            siteLocationId;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
};
