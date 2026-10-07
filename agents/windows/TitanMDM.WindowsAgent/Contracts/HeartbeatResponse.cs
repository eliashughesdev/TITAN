namespace TitanMDM.WindowsAgent.Contracts;

public sealed record HeartbeatResponse(
    Guid DeviceId,
    string Status,
    string ComplianceStatus,
    DateTime ServerTimeUtc,
    DateTime LastSeenAtUtc);