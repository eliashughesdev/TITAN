using System.Net;
using System.Net.Sockets;

namespace TitanMDM.Infrastructure.Android.Policies;

/// <summary>
/// Valida URLs de recursos visuales del Titan Launcher.
/// No acepta file:, content:, http:, credenciales ni destinos internos.
/// </summary>
public static class LauncherResourceUrlRules
{
    public const int MaxLength = 2048;

    private static readonly string[] AllowedExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    ];

    private static readonly string[] BlockedHostSuffixes =
    [
        "localhost",
        "local",
        "internal",
        "localdomain",
        "lan",
        "home",
        "home.arpa",
        "intranet"
    ];

    public static bool TryValidate(
        string? raw,
        out string? normalized,
        out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var value = raw.Trim();

        if (value.Length > MaxLength)
        {
            error =
                "La URL del recurso supera 2048 caracteres.";
            return false;
        }

        if (value.Contains('\\') ||
            value.Contains('@') ||
            value.Contains(' ') ||
            value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            error =
                "La URL del recurso no es un destino HTTPS autorizado.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(uri.IdnHost) ||
            uri.IsLoopback)
        {
            error =
                "La URL del recurso debe ser HTTPS, sin credenciales ni destinos internos.";
            return false;
        }

        if (!HasAllowedExtension(uri.AbsolutePath))
        {
            error =
                "El recurso debe ser PNG, JPEG o WebP servido por HTTPS.";
            return false;
        }

        if (!IsPublicHost(uri.IdnHost))
        {
            error =
                "La URL del recurso apunta a un destino interno o no autorizado.";
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Host = uri.IdnHost,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty
        };

        normalized = builder.Uri.GetComponents(
            UriComponents.HttpRequestUrl,
            UriFormat.UriEscaped);

        return true;
    }

    public static bool HostIsAuthorized(
        string normalizedUrl,
        IEnumerable<string>? authorizedHosts)
    {
        var hosts =
            authorizedHosts?
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Select(host => host.Trim().TrimEnd('.'))
                .Where(host => host.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            ?? [];

        if (hosts.Length == 0)
        {
            return true;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.IdnHost;
        return hosts.Any(allowed =>
            string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAllowedExtension(string absolutePath)
    {
        var path = Uri.UnescapeDataString(absolutePath);
        return AllowedExtensions.Any(extension =>
            path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPublicHost(string host)
    {
        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();

        if (normalized.Length == 0 ||
            normalized.All(char.IsDigit) ||
            BlockedHostSuffixes.Any(suffix =>
                normalized == suffix ||
                normalized.EndsWith("." + suffix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (normalized is "metadata" or "metadata.google.internal" ||
            normalized.EndsWith(".nip.io", StringComparison.Ordinal) ||
            normalized.EndsWith(".sslip.io", StringComparison.Ordinal))
        {
            return false;
        }

        if (!IPAddress.TryParse(normalized, out var address))
        {
            return Uri.CheckHostName(normalized) == UriHostNameType.Dns;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Broadcast))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !IsPrivateOrReservedIPv4(bytes);
        }

        var ipv6 = address.GetAddressBytes();
        var first = ipv6[0];
        return first != 0xFC &&
               first != 0xFD &&
               first != 0xFE &&
               first != 0xFF;
    }

    private static bool IsPrivateOrReservedIPv4(byte[] bytes)
    {
        if (bytes[0] is 0 or 10 or 127)
        {
            return true;
        }

        if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
        {
            return true;
        }

        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
        {
            return true;
        }

        if (bytes[0] == 198 && bytes[1] is 18 or 19 or 51)
        {
            return true;
        }

        if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
        {
            return true;
        }

        return bytes[0] >= 224;
    }
}
