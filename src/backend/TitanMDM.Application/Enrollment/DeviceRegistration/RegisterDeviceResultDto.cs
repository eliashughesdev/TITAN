namespace TitanMDM.Application.Enrollment.DeviceRegistration;

public sealed record RegisterDeviceResultDto(
    Guid DeviceId,
    Guid OrganizationId,
    string DeviceName,
    string Platform,
    string Status,
    string ComplianceStatus,
    bool IsManaged,
    DateTime EnrolledAtUtc,
    string DeviceSecret);