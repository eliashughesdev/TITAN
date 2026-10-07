namespace TitanMDM.Application.Devices.Agent;

public sealed record DeviceHeartbeatRequest(
    Guid DeviceId,
    string DeviceSecret,
    string? IpAddress,
    int? BatteryLevel,
    string? AgentVersion,
    string? OperatingSystemVersion);