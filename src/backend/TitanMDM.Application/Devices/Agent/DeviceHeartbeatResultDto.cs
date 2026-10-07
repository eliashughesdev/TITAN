namespace TitanMDM.Application.Devices.Agent;

public sealed record DeviceHeartbeatResultDto(
    Guid DeviceId,
    string Status,
    string ComplianceStatus,
    DateTime ServerTimeUtc,
    DateTime? LastSeenAtUtc);