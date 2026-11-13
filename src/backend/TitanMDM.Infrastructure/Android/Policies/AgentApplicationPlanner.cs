using System.Collections.Concurrent;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Android.Apk;
using TitanMDM.Infrastructure.Applications;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android.Policies;

/// <summary>
/// Aplicaciones de la política que el TitanMDM Agent instala por sí mismo
/// (equipos sin Google Play administrado): cada packageName obligatorio de
/// la política se resuelve contra los APK subidos al repositorio de
/// software de la organización.
/// </summary>
public static class AgentApplicationPlanner
{
    public const string AgentPackageName = "com.titanmdm.agent";

    /// <summary>installType que exigen la app instalada en el equipo.</summary>
    private static readonly HashSet<string> RequiredInstallTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "FORCE_INSTALLED",
            "REQUIRED_FOR_INSTALL",
            "REQUIRED_FOR_SETUP",
            "PREINSTALLED",
            "KIOSK",
        };

    /// <summary>
    /// AVAILABLE: sin Play Store administrado el agente también la instala
    /// si hay APK en el repositorio, pero su ausencia no incumple la política.
    /// </summary>
    private const string AvailableInstallType = "AVAILABLE";

    // El archivo de un paquete no cambia: el manifiesto se lee una vez.
    private static readonly ConcurrentDictionary<Guid, ApkManifestInfo?> ManifestCache = new();

    public sealed record PolicyApplication(string PackageName, string InstallType)
    {
        public bool Required => RequiredInstallTypes.Contains(InstallType);
    }

    public static IReadOnlyList<PolicyApplication> ReadRequiredApplications(string compiledPolicyJson)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(compiledPolicyJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        if (root is not JsonObject policy || policy["applications"] is not JsonArray applications)
            return [];

        return applications
            .OfType<JsonObject>()
            .Select(app => new PolicyApplication(
                ReadString(app, "packageName") ?? string.Empty,
                ReadString(app, "installType") ?? "FORCE_INSTALLED"))
            .Where(app =>
                app.PackageName.Length > 0 &&
                !string.Equals(app.PackageName, AgentPackageName, StringComparison.Ordinal) &&
                // Play Store: app del sistema, el agente no la instala desde el repositorio.
                !string.Equals(app.PackageName, AndroidPolicyCompiler.PlayStorePackage, StringComparison.OrdinalIgnoreCase) &&
                (RequiredInstallTypes.Contains(app.InstallType) ||
                 string.Equals(app.InstallType, AvailableInstallType, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(app => app.PackageName, StringComparer.Ordinal)
            .Select(group => group.FirstOrDefault(app => app.Required) ?? group.First())
            .ToList();
    }

    /// <summary>
    /// Lista para el payload APPLY_LAUNCHER_POLICY. Las apps sin APK en el
    /// repositorio se envían con available = false para que el agente las
    /// reporte en lugar de ignorarlas.
    /// </summary>
    public static async Task<JsonArray> BuildAsync(
        TitanMdmDbContext dbContext,
        Guid organizationId,
        string compiledPolicyJson,
        CancellationToken cancellationToken,
        Func<SoftwarePackage, ApkManifestInfo?>? readManifest = null)
    {
        readManifest ??= ReadManifest;

        var required = ReadRequiredApplications(compiledPolicyJson);
        var result = new JsonArray();
        if (required.Count == 0)
            return result;

        var packages = await dbContext.SoftwarePackages
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.IsActive &&
                x.PackageType == SoftwarePackageStorage.ApkPackageType)
            .ToListAsync(cancellationToken);

        var apks = packages
            .Select(package => (Package: package, Manifest: readManifest(package)))
            .Where(x => x.Manifest is not null)
            .ToList();

        foreach (var app in required)
        {
            var best = apks
                .Where(x => string.Equals(x.Manifest!.PackageName, app.PackageName, StringComparison.Ordinal))
                .OrderByDescending(x => x.Manifest!.VersionCode)
                .ThenByDescending(x => x.Package.CreatedAtUtc)
                .FirstOrDefault();

            if (best.Package is null)
            {
                result.Add(new JsonObject
                {
                    ["packageName"] = app.PackageName,
                    ["installType"] = app.InstallType,
                    ["required"] = app.Required,
                    ["available"] = false,
                });
                continue;
            }

            result.Add(new JsonObject
            {
                ["packageName"] = app.PackageName,
                ["installType"] = app.InstallType,
                ["required"] = app.Required,
                ["available"] = true,
                ["packageId"] = best.Package.Id.ToString("D"),
                ["downloadUrl"] = ManagedAppDownload.Url(app.PackageName),
                ["sha256"] = best.Package.Sha256,
                ["sizeBytes"] = best.Package.SizeBytes,
                ["versionCode"] = best.Manifest!.VersionCode,
                ["versionName"] = best.Manifest.VersionName,
                ["fileName"] = best.Package.OriginalFileName,
            });
        }

        return result;
    }

    private static ApkManifestInfo? ReadManifest(SoftwarePackage package)
        => ManifestCache.GetOrAdd(package.Id, _ =>
        {
            try
            {
                var path = SoftwarePackageStorage.GetFullPath(package);
                return File.Exists(path) ? ApkInspector.ReadManifest(path) : null;
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or InvalidOperationException)
            {
                return null;
            }
        });

    private static string? ReadString(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim()
            : null;
}
