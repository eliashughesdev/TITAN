using System.Text.Json;
using TitanMDM.WindowsAgent.Configuration;

namespace TitanMDM.WindowsAgent.Storage;

public sealed class AgentRuntimeSettingsStore
{
    private readonly string _directoryPath;
    private readonly string _settingsFilePath;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                WriteIndented =
                    true
            };

    public AgentRuntimeSettingsStore()
    {
        _directoryPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .CommonApplicationData),

                "TitanMDM");

        _settingsFilePath =
            Path.Combine(
                _directoryPath,
                "agentsettings.json");
    }

    public bool Exists()
    {
        return File.Exists(
            _settingsFilePath);
    }

    public async Task<AgentRuntimeSettings?>
        LoadAsync(
            CancellationToken cancellationToken =
                default)
    {
        if (!File.Exists(
                _settingsFilePath))
        {
            return null;
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    _settingsFilePath,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    json))
            {
                return null;
            }

            return JsonSerializer
                .Deserialize<AgentRuntimeSettings>(
                    json,
                    JsonOptions);
        }
        catch (Exception ex)
            when (
                ex is IOException
                ||
                ex is UnauthorizedAccessException
                ||
                ex is JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        AgentRuntimeSettings settings,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        NormalizeAndValidate(
            settings);

        Directory.CreateDirectory(
            _directoryPath);

        var json =
            JsonSerializer.Serialize(
                settings,
                JsonOptions);

        var temporaryPath =
            _settingsFilePath +
            ".tmp";

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            cancellationToken);

        File.Move(
            temporaryPath,
            _settingsFilePath,
            true);
    }

    public async Task ClearEnrollmentTokenAsync(
        CancellationToken cancellationToken =
            default)
    {
        var settings =
            await LoadAsync(
                cancellationToken);

        if (settings is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                settings.EnrollmentToken))
        {
            return;
        }

        settings.EnrollmentToken =
            null;

        await SaveAsync(
            settings,
            cancellationToken);
    }

    public async Task EnsureCreatedAsync(
        AgentOptions fallback,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentNullException.ThrowIfNull(
            fallback);

        if (Exists())
        {
            return;
        }

        var settings =
            new AgentRuntimeSettings
            {
                ServerUrl =
                    fallback.ServerUrl,

                EnrollmentToken =
                    fallback.EnrollmentToken,

                HeartbeatIntervalSeconds =
                    fallback
                        .HeartbeatIntervalSeconds,

                CommandPollingIntervalSeconds =
                    fallback
                        .CommandPollingIntervalSeconds,

                RequestTimeoutSeconds =
                    fallback
                        .RequestTimeoutSeconds
            };

        await SaveAsync(
            settings,
            cancellationToken);
    }

    public string GetSettingsFilePath()
    {
        return _settingsFilePath;
    }

    public string GetDirectoryPath()
    {
        return _directoryPath;
    }

    private static void NormalizeAndValidate(
        AgentRuntimeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(
                settings.ServerUrl))
        {
            throw new InvalidOperationException(
                "ServerUrl es obligatorio.");
        }

        settings.ServerUrl =
            settings.ServerUrl
                .Trim()
                .TrimEnd('/');

        if (!Uri.TryCreate(
                settings.ServerUrl,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                "ServerUrl no contiene una URL válida.");
        }

        if (uri.Scheme !=
                Uri.UriSchemeHttp
            &&
            uri.Scheme !=
                Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "ServerUrl debe usar HTTP o HTTPS.");
        }

        settings.HeartbeatIntervalSeconds =
            Math.Clamp(
                settings
                    .HeartbeatIntervalSeconds,
                15,
                3600);

        settings.CommandPollingIntervalSeconds =
            Math.Clamp(
                settings
                    .CommandPollingIntervalSeconds,
                5,
                300);

        settings.RequestTimeoutSeconds =
            Math.Clamp(
                settings
                    .RequestTimeoutSeconds,
                10,
                300);
    }
}