using System.Text.RegularExpressions;

namespace TitanMDM.Infrastructure.Applications;

/// <summary>Bajada directa del APK firmado cuando no hay contenedor GMS.</summary>
public static partial class ManagedAppDownload
{
    public const string PathPrefix = "/api/apps/download/";

    public static string Url(string packageName) => PathPrefix + packageName;

    public static bool IsValidPackageName(string? packageName) =>
        !string.IsNullOrWhiteSpace(packageName) &&
        PackageNamePattern().IsMatch(packageName);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$")]
    private static partial Regex PackageNamePattern();
}
