namespace TitanMDM.Application.Enrollment.DeviceRegistration;

public sealed record RegisterDeviceRequest(
    string EnrollmentToken,
    string DeviceName,
    string Platform,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? AgentVersion,
    string? IpAddress,
    string? MacAddress);