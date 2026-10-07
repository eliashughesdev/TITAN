namespace TitanMDM.Domain.Entities;

public sealed class DeviceApplication
{
    private DeviceApplication()
    {
    }

    public DeviceApplication(
        Guid organizationId,
        Guid deviceId,
        string packageName,
        string applicationName,
        string? versionName,
        long versionCode,
        bool isSystemApp,
        bool isEnabled,
        DateTime? firstInstallTimeUtc,
        DateTime? lastUpdateTimeUtc,
        string? installerPackageName)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException(
                "PackageName is required.",
                nameof(packageName));
        }

        Id = Guid.NewGuid();

        OrganizationId = organizationId;
        DeviceId = deviceId;

        PackageName = packageName.Trim();

        ApplicationName =
            Normalize(applicationName)
            ?? PackageName;

        VersionName =
            Normalize(versionName);

        VersionCode =
            versionCode;

        IsSystemApp =
            isSystemApp;

        IsEnabled =
            isEnabled;

        FirstInstallTimeUtc =
            NormalizeUtc(firstInstallTimeUtc);

        LastUpdateTimeUtc =
            NormalizeUtc(lastUpdateTimeUtc);

        InstallerPackageName =
            Normalize(installerPackageName);

        IsPresent = true;

        FirstSeenAtUtc =
            DateTime.UtcNow;

        LastSeenAtUtc =
            DateTime.UtcNow;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string PackageName { get; private set; } =
        string.Empty;

    public string ApplicationName { get; private set; } =
        string.Empty;

    public string? VersionName { get; private set; }

    public long VersionCode { get; private set; }

    public bool IsSystemApp { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool IsPresent { get; private set; }

    public DateTime? FirstInstallTimeUtc { get; private set; }

    public DateTime? LastUpdateTimeUtc { get; private set; }

    public string? InstallerPackageName { get; private set; }

    public DateTime FirstSeenAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Synchronize(
        string applicationName,
        string? versionName,
        long versionCode,
        bool isSystemApp,
        bool isEnabled,
        DateTime? firstInstallTimeUtc,
        DateTime? lastUpdateTimeUtc,
        string? installerPackageName)
    {
        ApplicationName =
            Normalize(applicationName)
            ?? PackageName;

        VersionName =
            Normalize(versionName);

        VersionCode =
            versionCode;

        IsSystemApp =
            isSystemApp;

        IsEnabled =
            isEnabled;

        FirstInstallTimeUtc =
            NormalizeUtc(firstInstallTimeUtc);

        LastUpdateTimeUtc =
            NormalizeUtc(lastUpdateTimeUtc);

        InstallerPackageName =
            Normalize(installerPackageName);

        IsPresent = true;

        LastSeenAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkMissing()
    {
        IsPresent = false;
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
        {
            return null;
        }

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