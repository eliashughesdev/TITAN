namespace TitanMDM.Domain.Entities;

public sealed class AndroidDevice
{
    private AndroidDevice()
    {
    }

    public AndroidDevice(
        Guid organizationId,
        Guid deviceId,
        string googleDeviceName)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));

        if (string.IsNullOrWhiteSpace(googleDeviceName))
            throw new ArgumentException(
                "GoogleDeviceName is required.",
                nameof(googleDeviceName));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        GoogleDeviceName = googleDeviceName.Trim();

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        LastSynchronizedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid DeviceId { get; private set; }

    // Android Management API:
    // enterprises/{enterpriseId}/devices/{deviceId}
    public string GoogleDeviceName { get; private set; } =
        string.Empty;

    public string? GoogleDeviceId { get; private set; }

    public string? ManagementMode { get; private set; }

    public string? Ownership { get; private set; }

    public string? State { get; private set; }

    public string? AppliedPolicyName { get; private set; }

    public long? AppliedPolicyVersion { get; private set; }

    public string? AppliedPolicyState { get; private set; }

    public string? EnrollmentTokenName { get; private set; }

    public string? UserName { get; private set; }

    public string? Brand { get; private set; }

    public string? Hardware { get; private set; }

    public string? DeviceBasebandVersion { get; private set; }

    public string? BootloaderVersion { get; private set; }

    public string? SecurityPatchLevel { get; private set; }

    public int? ApiLevel { get; private set; }

    public string? BuildNumber { get; private set; }

    public string? KernelVersion { get; private set; }

    public string? AndroidDevicePolicyVersion { get; private set; }

    public string? AndroidDevicePolicyVersionCode { get; private set; }

    public string? EncryptionStatus { get; private set; }

    public string? SecurityPosture { get; private set; }

    public DateTime? EnrollmentTimeUtc { get; private set; }

    public DateTime? LastStatusReportTimeUtc { get; private set; }

    public DateTime? LastPolicySyncTimeUtc { get; private set; }

    public DateTime LastSynchronizedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsDeletedInGoogle { get; private set; }

    public DateTime? DeletedInGoogleAtUtc { get; private set; }

    public void Synchronize(
        string? googleDeviceId,
        string? managementMode,
        string? ownership,
        string? state,
        string? appliedPolicyName,
        long? appliedPolicyVersion,
        string? appliedPolicyState,
        string? enrollmentTokenName,
        string? userName,
        string? brand,
        string? hardware,
        string? deviceBasebandVersion,
        string? bootloaderVersion,
        string? securityPatchLevel,
        int? apiLevel,
        string? buildNumber,
        string? kernelVersion,
        string? androidDevicePolicyVersion,
        string? androidDevicePolicyVersionCode,
        string? encryptionStatus,
        string? securityPosture,
        DateTime? enrollmentTimeUtc,
        DateTime? lastStatusReportTimeUtc,
        DateTime? lastPolicySyncTimeUtc)
    {
        GoogleDeviceId = Normalize(googleDeviceId);
        ManagementMode = Normalize(managementMode);
        Ownership = Normalize(ownership);
        State = Normalize(state);

        AppliedPolicyName =
            Normalize(appliedPolicyName);

        AppliedPolicyVersion =
            appliedPolicyVersion;

        AppliedPolicyState =
            Normalize(appliedPolicyState);

        EnrollmentTokenName =
            Normalize(enrollmentTokenName);

        UserName = Normalize(userName);

        Brand = Normalize(brand);
        Hardware = Normalize(hardware);

        DeviceBasebandVersion =
            Normalize(deviceBasebandVersion);

        BootloaderVersion =
            Normalize(bootloaderVersion);

        SecurityPatchLevel =
            Normalize(securityPatchLevel);

        ApiLevel = apiLevel;

        BuildNumber =
            Normalize(buildNumber);

        KernelVersion =
            Normalize(kernelVersion);

        AndroidDevicePolicyVersion =
            Normalize(androidDevicePolicyVersion);

        AndroidDevicePolicyVersionCode =
            Normalize(androidDevicePolicyVersionCode);

        EncryptionStatus =
            Normalize(encryptionStatus);

        SecurityPosture =
            Normalize(securityPosture);

        EnrollmentTimeUtc =
            NormalizeUtc(enrollmentTimeUtc);

        LastStatusReportTimeUtc =
            NormalizeUtc(lastStatusReportTimeUtc);

        LastPolicySyncTimeUtc =
            NormalizeUtc(lastPolicySyncTimeUtc);

        IsDeletedInGoogle = false;
        DeletedInGoogleAtUtc = null;

        LastSynchronizedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkMissingInGoogle()
    {
        if (!IsDeletedInGoogle)
        {
            IsDeletedInGoogle = true;
            DeletedInGoogleAtUtc = DateTime.UtcNow;
        }

        LastSynchronizedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RestoreFromGoogle()
    {
        IsDeletedInGoogle = false;
        DeletedInGoogleAtUtc = null;
        LastSynchronizedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime? NormalizeUtc(
        DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc =>
                value.Value,

            DateTimeKind.Local =>
                value.Value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value.Value,
                    DateTimeKind.Utc)
        };
    }
}