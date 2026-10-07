namespace TitanMDM.Application.Security;

public interface ISecurityPostureService
{
    Task ProcessSecurityStatusAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task ProcessComplianceAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task<SecurityDashboardDto>
        GetDashboardAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DeviceSecurityDto>>
        GetDevicesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);
}

public sealed record SecurityDashboardDto(
    int TotalDevices,
    int EvaluatedDevices,
    int CompliantDevices,
    int NonCompliantDevices,
    int RootedDevices,
    int AdbEnabledDevices,
    int DeveloperModeDevices,
    int UnsecuredDevices,
    int CriticalRiskDevices,
    double AverageComplianceScore);

public sealed record DeviceSecurityDto(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    string Status,
    string ComplianceStatus,
    int ComplianceScore,
    string RiskLevel,
    bool DeviceSecure,
    string EncryptionStatus,
    bool AdbEnabled,
    bool DeveloperOptionsEnabled,
    bool RootDetected,
    bool EmulatorDetected,
    bool? BootloaderLocked,
    bool? SelinuxEnforced,
    bool AgentInstalled,
    string AgentVersionName,
    string? SecurityPatchLevel,
    int TotalChecks,
    int PassedChecks,
    int FailedChecks,
    string FindingsJson,
    DateTime? LastSecurityScanAtUtc,
    DateTime? LastComplianceCheckAtUtc);