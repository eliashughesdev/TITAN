using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;

namespace TitanMDM.WindowsAgent.Storage;

[SupportedOSPlatform("windows")]
public sealed class DeviceIdentityStore
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "TitanMDM.WindowsAgent.DeviceIdentity.v1");

    private readonly string _directoryPath;
    private readonly string _identityFilePath;

    public DeviceIdentityStore()
    {
        _directoryPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "TitanMDM");

        _identityFilePath =
            Path.Combine(
                _directoryPath,
                "device.json");
    }

    public bool Exists()
    {
        return File.Exists(
            _identityFilePath);
    }

    public async Task<DeviceIdentity?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(
                _identityFilePath))
        {
            return null;
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    _identityFilePath,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var storedIdentity =
                JsonSerializer.Deserialize<StoredDeviceIdentity>(
                    json,
                    JsonOptions);

            if (storedIdentity is null ||
                storedIdentity.DeviceId == Guid.Empty ||
                string.IsNullOrWhiteSpace(
                    storedIdentity.ProtectedDeviceSecret))
            {
                return null;
            }

            var encryptedBytes =
                Convert.FromBase64String(
                    storedIdentity.ProtectedDeviceSecret);

            var secretBytes =
                ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope.LocalMachine);

            var deviceSecret =
                Encoding.UTF8.GetString(
                    secretBytes);

            if (string.IsNullOrWhiteSpace(
                    deviceSecret))
            {
                return null;
            }

            return new DeviceIdentity(
                storedIdentity.DeviceId,
                deviceSecret);
        }
        catch (
            Exception ex)
            when (
                ex is JsonException ||
                ex is FormatException ||
                ex is CryptographicException ||
                ex is IOException ||
                ex is UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        DeviceIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            identity);

        if (identity.DeviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId no puede estar vacío.",
                nameof(identity));
        }

        if (string.IsNullOrWhiteSpace(
                identity.DeviceSecret))
        {
            throw new ArgumentException(
                "DeviceSecret no puede estar vacío.",
                nameof(identity));
        }

        Directory.CreateDirectory(
            _directoryPath);

        var secretBytes =
            Encoding.UTF8.GetBytes(
                identity.DeviceSecret);

        var encryptedBytes =
            ProtectedData.Protect(
                secretBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

        var storedIdentity =
            new StoredDeviceIdentity(
                identity.DeviceId,
                Convert.ToBase64String(
                    encryptedBytes));

        var json =
            JsonSerializer.Serialize(
                storedIdentity,
                JsonOptions);

        var temporaryFilePath =
            _identityFilePath +
            ".tmp";

        await File.WriteAllTextAsync(
            temporaryFilePath,
            json,
            cancellationToken);

        File.Move(
            temporaryFilePath,
            _identityFilePath,
            true);
    }

    public Task DeleteAsync()
    {
        if (File.Exists(
                _identityFilePath))
        {
            File.Delete(
                _identityFilePath);
        }

        return Task.CompletedTask;
    }

    public string GetIdentityFilePath()
    {
        return _identityFilePath;
    }

    private static readonly JsonSerializerOptions
        JsonOptions = new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            PropertyNameCaseInsensitive =
                true,

            WriteIndented =
                true
        };

    private sealed record StoredDeviceIdentity(
        Guid DeviceId,
        string ProtectedDeviceSecret);
}

public sealed record DeviceIdentity(
    Guid DeviceId,
    string DeviceSecret);