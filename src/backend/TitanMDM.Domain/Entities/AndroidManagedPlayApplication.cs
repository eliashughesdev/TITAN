using System.Text.RegularExpressions;

using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class AndroidManagedPlayApplication
{
    public const int MaxPackageNameLength = 255;
    public const int MaxDisplayTitleLength = 300;
    public const int MaxIconUrlLength = 2000;
    public const int MaxDescriptionLength = 2000;
    public const int MaxLatestVersionNameLength = 100;
    public const int MaxProductIdLength = 100;
    public const int MaxInstallationTypeLength = 50;
    public const int MaxMetadataErrorLength = 500;

    private static readonly Regex PackageNamePattern =
        new(
            "^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)+$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private AndroidManagedPlayApplication()
    {
    }

    public AndroidManagedPlayApplication(
        Guid organizationId,
        string packageName,
        AndroidManagedPlayApplicationSource source,
        Guid createdByUserId,
        string? productId = null,
        string? installationType = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "CreatedByUserId is required.");
        }

        Id = Guid.NewGuid();

        OrganizationId = organizationId;
        PackageName = NormalizePackageName(packageName);
        Source = source;
        ProductId = NormalizeOptional(productId, MaxProductIdLength);
        InstallationType = NormalizeOptional(installationType, MaxInstallationTypeLength);

        ApprovalState = AndroidManagedPlayApprovalState.Pending;

        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string PackageName { get; private set; } = string.Empty;

    public AndroidManagedPlayApplicationSource Source { get; private set; }

    public AndroidManagedPlayApprovalState ApprovalState { get; private set; }

    public string? DisplayTitle { get; private set; }

    public string? IconUrl { get; private set; }

    public string? Description { get; private set; }

    public string? LatestVersionName { get; private set; }

    public string? ProductId { get; private set; }

    public string? InstallationType { get; private set; }

    public string? MetadataError { get; private set; }

    public DateTime? MetadataFetchedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? RemovedAtUtc { get; private set; }

    public bool IsRemoved => RemovedAtUtc.HasValue;

    public bool HasOfficialMetadata => MetadataFetchedAtUtc.HasValue;

    public void ApplyOfficialMetadata(
        string? displayTitle,
        string? iconUrl,
        string? description,
        string? latestVersionName,
        DateTime fetchedAtUtc)
    {
        DisplayTitle = NormalizeOptional(displayTitle, MaxDisplayTitleLength);
        IconUrl = NormalizeOptional(iconUrl, MaxIconUrlLength);
        Description = NormalizeOptional(description, MaxDescriptionLength);
        LatestVersionName = NormalizeOptional(latestVersionName, MaxLatestVersionNameLength);

        ApprovalState = AndroidManagedPlayApprovalState.Available;
        MetadataError = null;
        MetadataFetchedAtUtc = EnsureUtc(fetchedAtUtc);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkMetadataUnavailable(
        string? error,
        DateTime fetchedAtUtc)
    {
        ApprovalState = AndroidManagedPlayApprovalState.Unavailable;
        MetadataError = NormalizeOptional(error, MaxMetadataErrorLength);
        MetadataFetchedAtUtc = EnsureUtc(fetchedAtUtc);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateInstallationType(string? installationType)
    {
        InstallationType = NormalizeOptional(installationType, MaxInstallationTypeLength);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateSource(AndroidManagedPlayApplicationSource source)
    {
        Source = source;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Remove()
    {
        RemovedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = RemovedAtUtc.Value;
    }

    public void Reactivate()
    {
        RemovedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string NormalizePackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException(
                "PackageName is required.",
                nameof(packageName));
        }

        var normalized = packageName.Trim().ToLowerInvariant();

        if (normalized.Length > MaxPackageNameLength ||
            !PackageNamePattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "PackageName no es un identificador de aplicacion Android valido.",
                nameof(packageName));
        }

        return normalized;
    }

    public static bool IsValidPackageName(string? packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return false;
        }

        var normalized = packageName.Trim();

        return normalized.Length <= MaxPackageNameLength &&
               PackageNamePattern.IsMatch(normalized);
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();
    }
}
