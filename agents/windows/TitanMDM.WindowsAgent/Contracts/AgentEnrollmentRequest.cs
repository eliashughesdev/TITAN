namespace TitanMDM.WindowsAgent.Contracts;

public sealed record AgentEnrollmentRequest(
    string EnrollmentToken,
    string DeviceName,
    string Platform,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? AgentVersion);