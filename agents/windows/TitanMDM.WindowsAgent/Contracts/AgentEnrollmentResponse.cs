namespace TitanMDM.WindowsAgent.Contracts;

public sealed record AgentEnrollmentResponse(
    Guid DeviceId,
    Guid OrganizationId,
    string DeviceName,
    string Platform,
    string Status,
    string ComplianceStatus,
    bool IsManaged,
    DateTime EnrolledAtUtc,
    string DeviceSecret);