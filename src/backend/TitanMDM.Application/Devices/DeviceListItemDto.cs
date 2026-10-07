namespace TitanMDM.Application.Devices;

public sealed record DeviceListItemDto(
    Guid Id,
    string DeviceName,
    string Platform,
    string Status,
    string ComplianceStatus,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? AssignedUser,
    string? Department,
    string? IpAddress,
    int? BatteryLevel,
    bool IsManaged,
    DateTime? EnrolledAtUtc,
    DateTime? LastSeenAtUtc);