using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android.Policies;

public sealed record LauncherAssetInfo(
    Guid Id,
    Guid OrganizationId,
    string Kind,
    string Format,
    long Size,
    string Sha256,
    DateTime CreatedAtUtc);

public sealed class LauncherAssetStore
{
    public const long MaxWallpaperBytes = 4 * 1024 * 1024;
    public const long MaxLogoBytes = 1024 * 1024;

    private readonly string _root;
    private readonly TitanMdmDbContext _dbContext;

    public LauncherAssetStore(
        IWebHostEnvironment environment,
        IOptions<AndroidManagementOptions> options,
        TitanMdmDbContext dbContext)
    {
        var configured = options.Value.LauncherAssetRoot;
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "launcher-assets")
            : configured;
        _dbContext = dbContext;
    }

    public async Task<LauncherAssetInfo> SaveAsync(
        Guid organizationId,
        string kind,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("La organización es obligatoria.");
        }

        var normalizedKind = NormalizeKind(kind);
        var limit = normalizedKind == "logo" ? MaxLogoBytes : MaxWallpaperBytes;
        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > limit)
        {
            throw new InvalidOperationException("El archivo está vacío o supera el tamaño permitido.");
        }

        var bytes = buffer.ToArray();
        var format = DetectFormat(bytes)
            ?? throw new InvalidOperationException("El archivo debe ser PNG, JPEG o WebP.");
        var id = Guid.NewGuid();
        var directory = OrganizationDirectory(organizationId);
        Directory.CreateDirectory(directory);
        var payload = PayloadPath(organizationId, id);
        var meta = MetaPath(organizationId, id);
        await File.WriteAllBytesAsync(payload, bytes, cancellationToken);
        var info = new LauncherAssetInfo(
            id,
            organizationId,
            normalizedKind,
            format,
            bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            DateTime.UtcNow);
        await File.WriteAllTextAsync(meta, JsonSerializer.Serialize(info), cancellationToken);
        return info;
    }

    public IReadOnlyList<LauncherAssetInfo> List(Guid organizationId)
    {
        var directory = OrganizationDirectory(organizationId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.meta")
            .Select(path => ReadMeta(path))
            .Where(item => item is not null && item.OrganizationId == organizationId)
            .Select(item => item!)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArray();
    }

    public LauncherAssetInfo? Find(Guid organizationId, Guid assetId)
    {
        var info = ReadMeta(MetaPath(organizationId, assetId));
        if (info is null || info.OrganizationId != organizationId || info.Id != assetId)
        {
            return null;
        }

        var payload = PayloadPath(organizationId, assetId);
        if (!File.Exists(payload))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(payload);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return hash == info.Sha256 && DetectFormat(bytes) == info.Format
            ? info
            : null;
    }

    public (LauncherAssetInfo Info, byte[] Bytes)? ReadBytes(Guid organizationId, Guid assetId)
    {
        var info = Find(organizationId, assetId);
        if (info is null)
        {
            return null;
        }

        var bytes = File.ReadAllBytes(PayloadPath(organizationId, assetId));
        return (info, bytes);
    }

    public bool Delete(Guid organizationId, Guid assetId)
    {
        var payload = PayloadPath(organizationId, assetId);
        var meta = MetaPath(organizationId, assetId);
        var existed = File.Exists(payload) || File.Exists(meta);
        if (File.Exists(payload)) File.Delete(payload);
        if (File.Exists(meta)) File.Delete(meta);
        return existed;
    }

    public async Task<int> CleanupAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var versions = await _dbContext.PolicyVersions
            .AsNoTracking()
            .Where(version => version.OrganizationId == organizationId)
            .Select(version => version.ConfigurationJson)
            .ToListAsync(cancellationToken);
        var referenced = new List<Guid>();
        foreach (var json in versions)
        {
            foreach (Match match in AssetReferencePattern.Matches(json))
            {
                if (Guid.TryParse(match.Groups[1].Value, out var id))
                {
                    referenced.Add(id);
                }
            }
        }

        return DeleteUnreferenced(organizationId, referenced);
    }

    public int DeleteUnreferenced(Guid organizationId, IEnumerable<Guid> referenced)
    {
        var keep = referenced.ToHashSet();
        var removed = 0;
        foreach (var asset in List(organizationId))
        {
            if (keep.Contains(asset.Id))
            {
                continue;
            }

            if (Delete(organizationId, asset.Id))
            {
                removed++;
            }
        }

        return removed;
    }

    public async Task<(LauncherAssetInfo Info, byte[] Bytes)?> ReadForDeviceAsync(
        Guid deviceId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var device = await _dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == deviceId && !item.IsDeleted,
                cancellationToken);
        if (device is null)
        {
            return null;
        }

        return ReadBytes(device.OrganizationId, assetId);
    }

    private static readonly Regex AssetReferencePattern = new(
        "titan-asset:([0-9a-fA-F-]{36})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? AssetToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("titan-asset:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = value["titan-asset:".Length..].Trim();
        return Guid.TryParse(raw, out var id) && id != Guid.Empty
            ? id.ToString("D")
            : null;
    }

    public static string? DetectFormat(byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return null;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "png";
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "jpg";
        }

        return bytes[0] == (byte)'R' &&
               bytes[1] == (byte)'I' &&
               bytes[2] == (byte)'F' &&
               bytes[3] == (byte)'F' &&
               bytes[8] == (byte)'W' &&
               bytes[9] == (byte)'E' &&
               bytes[10] == (byte)'B' &&
               bytes[11] == (byte)'P'
            ? "webp"
            : null;
    }

    private string OrganizationDirectory(Guid organizationId) =>
        Path.Combine(_root, organizationId.ToString("D"));

    private string PayloadPath(Guid organizationId, Guid assetId) =>
        Path.Combine(OrganizationDirectory(organizationId), assetId.ToString("D") + ".bin");

    private string MetaPath(Guid organizationId, Guid assetId) =>
        Path.Combine(OrganizationDirectory(organizationId), assetId.ToString("D") + ".meta");

    private static LauncherAssetInfo? ReadMeta(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherAssetInfo>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeKind(string kind)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "wallpaper" => "wallpaper",
            "logo" => "logo",
            _ => throw new InvalidOperationException("El tipo de recurso debe ser fondo o logotipo.")
        };
    }
}
