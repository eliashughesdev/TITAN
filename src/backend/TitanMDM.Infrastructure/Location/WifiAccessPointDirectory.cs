using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace TitanMDM.Infrastructure.Location;

public sealed class WifiAccessPointDirectory
{
    private readonly IReadOnlyDictionary<string, WifiAccessPointFix> _accessPoints;

    public WifiAccessPointDirectory(IConfiguration configuration)
    {
        var accessPoints =
            new Dictionary<string, WifiAccessPointFix>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var section in configuration
                     .GetSection("Inventory:WifiAccessPoints")
                     .GetChildren())
        {
            var bssid = NormalizeBssid(section["Bssid"]);

            if (bssid is null ||
                !double.TryParse(
                    section["Latitude"],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude) ||
                !double.TryParse(
                    section["Longitude"],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude))
            {
                continue;
            }

            accessPoints[bssid] = new WifiAccessPointFix(
                latitude,
                longitude,
                section["Label"]);
        }

        _accessPoints = accessPoints;
    }

    public bool TryResolve(
        string? bssid,
        [NotNullWhen(true)] out WifiAccessPointFix? fix)
    {
        fix = null;

        var normalized = NormalizeBssid(bssid);

        return normalized is not null &&
               _accessPoints.TryGetValue(normalized, out fix);
    }

    private static string? NormalizeBssid(string? bssid)
    {
        if (string.IsNullOrWhiteSpace(bssid))
        {
            return null;
        }

        var normalized = bssid
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized.Length == 12
            ? normalized
            : null;
    }
}

public sealed record WifiAccessPointFix(
    double Latitude,
    double Longitude,
    string? Label);
