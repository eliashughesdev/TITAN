using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Enrollment;
using TitanMDM.Infrastructure.Android.Apk;

namespace TitanMDM.Infrastructure.Android.Provisioning;

/// <summary>
/// Calcula PROVISIONING_DEVICE_ADMIN_SIGNATURE_CHECKSUM del APK real del
/// agente: el local (deploy/app-debug.apk, el mismo que se publica en main)
/// o, si no existe, el que sirve DownloadUrl. Se recalcula cuando el
/// archivo cambia.
/// </summary>
public sealed class AndroidProvisioningInfoProvider : IAndroidProvisioningInfoProvider
{
    private const string AgentPackageName = "com.titanmdm.agent";

    private readonly AndroidProvisioningOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AndroidProvisioningInfoProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private (string Path, DateTime WriteTimeUtc, long Length, AndroidProvisioningInfoDto Info)? _cached;
    private (DateTime FetchedAtUtc, AndroidProvisioningInfoDto Info)? _remote;

    public AndroidProvisioningInfoProvider(
        IOptions<AndroidProvisioningOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AndroidProvisioningInfoProvider> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AndroidProvisioningInfoDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var downloadUrl = _options.DownloadUrl.Trim();
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("AndroidProvisioning:DownloadUrl debe ser una URL https.");

        if (!string.IsNullOrWhiteSpace(_options.SignatureChecksum))
        {
            return new AndroidProvisioningInfoDto(
                _options.ComponentName, AgentPackageName, downloadUrl,
                _options.SignatureChecksum.Trim(), null, null, null, "configuration");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var local = ResolveLocalApk();
            if (local is not null)
                return ReadLocal(local, downloadUrl);

            return await ReadRemoteAsync(downloadUrl, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private AndroidProvisioningInfoDto ReadLocal(string path, string downloadUrl)
    {
        var file = new FileInfo(path);
        if (_cached is { } cached &&
            cached.Path == path &&
            cached.WriteTimeUtc == file.LastWriteTimeUtc &&
            cached.Length == file.Length)
        {
            return cached.Info;
        }

        var info = Inspect(path, downloadUrl, "local:" + path);
        _cached = (path, file.LastWriteTimeUtc, file.Length, info);
        return info;
    }

    private async Task<AndroidProvisioningInfoDto> ReadRemoteAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        if (_remote is { } remote && DateTime.UtcNow - remote.FetchedAtUtc < TimeSpan.FromMinutes(10))
            return remote.Info;

        var temp = Path.Combine(Path.GetTempPath(), $"titan-agent-{Guid.NewGuid():N}.apk");
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(AndroidProvisioningInfoProvider));
            client.Timeout = TimeSpan.FromMinutes(2);
            await using (var response = await client.GetStreamAsync(downloadUrl, cancellationToken))
            await using (var output = File.Create(temp))
            {
                await response.CopyToAsync(output, cancellationToken);
            }

            var info = Inspect(temp, downloadUrl, "download");
            _remote = (DateTime.UtcNow, info);
            return info;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Android agent APK could not be downloaded from {Url}", downloadUrl);
            throw new InvalidOperationException(
                "No se encontró el APK del agente (deploy/app-debug.apk) ni se pudo descargar desde AndroidProvisioning:DownloadUrl.");
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { }
        }
    }

    private AndroidProvisioningInfoDto Inspect(string path, string downloadUrl, string source)
    {
        try
        {
            var manifest = ApkInspector.ReadManifest(path);
            if (!string.Equals(manifest.PackageName, AgentPackageName, StringComparison.Ordinal))
                throw new InvalidOperationException($"El APK de aprovisionamiento es {manifest.PackageName}, no {AgentPackageName}.");

            using var stream = File.OpenRead(path);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

            return new AndroidProvisioningInfoDto(
                _options.ComponentName,
                manifest.PackageName,
                downloadUrl,
                ApkInspector.ReadSignatureChecksum(path),
                sha256,
                manifest.VersionCode,
                manifest.VersionName,
                source);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException($"El APK del agente no es válido: {exception.Message}");
        }
    }

    private string? ResolveLocalApk()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApkPath))
            return File.Exists(_options.ApkPath) ? Path.GetFullPath(_options.ApkPath) : null;

        // Busca deploy/app-debug.apk subiendo desde el binario del API.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; directory is not null && depth < 10; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "app-debug.apk");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
