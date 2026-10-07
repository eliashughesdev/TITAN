namespace TitanMDM.Domain.Entities;

public sealed class DeviceSecurityPosture
{
    private DeviceSecurityPosture()
    {
    }

    public DeviceSecurityPosture(
        Guid organizationId,
        Guid deviceId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string AndroidVersion { get; private set; } =
        string.Empty;

    public int ApiLevel { get; private set; }

    public string? SecurityPatchLevel { get; private set; }

    public bool DeviceSecure { get; private set; }

    public string EncryptionStatus { get; private set; } =
        "Unknown";

    public bool AdbEnabled { get; private set; }

    public bool DeveloperOptionsEnabled { get; private set; }

    public bool RootDetected { get; private set; }

    public string RootSignalsJson { get; private set; } =
        "[]";

    public bool EmulatorDetected { get; private set; }

    public string? VerifiedBootState { get; private set; }

    public bool? BootloaderLocked { get; private set; }

    public bool? SelinuxEnforced { get; private set; }

    public bool AgentInstalled { get; private set; }

    public string AgentVersionName { get; private set; } =
        string.Empty;

    public long AgentVersionCode { get; private set; }

    public bool? UnknownSourcesAllowed { get; private set; }

    public int ComplianceScore { get; private set; }

    public string RiskLevel { get; private set; } =
        "Unknown";

    public string ComplianceStatus { get; private set; } =
        "Unknown";

    public int TotalChecks { get; private set; }

    public int PassedChecks { get; private set; }

    public int FailedChecks { get; private set; }

    public string FindingsJson { get; private set; } =
        "[]";

    public DateTime? LastSecurityScanAtUtc { get; private set; }

    public DateTime? LastComplianceCheckAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateSecurity(
        string androidVersion,
        int apiLevel,
        string? securityPatchLevel,
        bool deviceSecure,
        string encryptionStatus,
        bool adbEnabled,
        bool developerOptionsEnabled,
        bool rootDetected,
        string rootSignalsJson,
        bool emulatorDetected,
        string? verifiedBootState,
        bool? bootloaderLocked,
        bool? selinuxEnforced,
        bool agentInstalled,
        string agentVersionName,
        long agentVersionCode,
        bool? unknownSourcesAllowed)
    {
        AndroidVersion =
            androidVersion ?? string.Empty;

        ApiLevel = apiLevel;

        SecurityPatchLevel =
            Normalize(securityPatchLevel);

        DeviceSecure = deviceSecure;

        EncryptionStatus =
            string.IsNullOrWhiteSpace(
                encryptionStatus)
                ? "Unknown"
                : encryptionStatus.Trim();

        AdbEnabled = adbEnabled;

        DeveloperOptionsEnabled =
            developerOptionsEnabled;

        RootDetected =
            rootDetected;

        RootSignalsJson =
            string.IsNullOrWhiteSpace(
                rootSignalsJson)
                ? "[]"
                : rootSignalsJson;

        EmulatorDetected =
            emulatorDetected;

        VerifiedBootState =
            Normalize(
                verifiedBootState);

        BootloaderLocked =
            bootloaderLocked;

        SelinuxEnforced =
            selinuxEnforced;

        AgentInstalled =
            agentInstalled;

        AgentVersionName =
            agentVersionName ?? string.Empty;

        AgentVersionCode =
            agentVersionCode;

        UnknownSourcesAllowed =
            unknownSourcesAllowed;

        LastSecurityScanAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void UpdateCompliance(
        int score,
        string riskLevel,
        string complianceStatus,
        int totalChecks,
        int passedChecks,
        int failedChecks,
        string findingsJson)
    {
        ComplianceScore =
            Math.Clamp(
                score,
                0,
                100);

        RiskLevel =
            string.IsNullOrWhiteSpace(
                riskLevel)
                ? "Unknown"
                : riskLevel.Trim();

        ComplianceStatus =
            string.IsNullOrWhiteSpace(
                complianceStatus)
                ? "Unknown"
                : complianceStatus.Trim();

        TotalChecks =
            Math.Max(
                totalChecks,
                0);

        PassedChecks =
            Math.Max(
                passedChecks,
                0);

        FailedChecks =
            Math.Max(
                failedChecks,
                0);

        FindingsJson =
            string.IsNullOrWhiteSpace(
                findingsJson)
                ? "[]"
                : findingsJson;

        LastComplianceCheckAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();
    }
}