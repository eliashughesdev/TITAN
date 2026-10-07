using Microsoft.Extensions.Options;
using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class AgentConfigurationBootstrapper
{
    private readonly AgentRuntimeSettingsStore
        _settingsStore;

    private readonly AgentOptions
        _options;

    private readonly ILogger<
        AgentConfigurationBootstrapper>
        _logger;

    public AgentConfigurationBootstrapper(
        AgentRuntimeSettingsStore
            settingsStore,
        IOptions<AgentOptions>
            options,
        ILogger<
            AgentConfigurationBootstrapper>
            logger)
    {
        _settingsStore =
            settingsStore;

        _options =
            options.Value;

        _logger =
            logger;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken =
            default)
    {
        await _settingsStore
            .EnsureCreatedAsync(
                _options,
                cancellationToken);

        var runtimeSettings =
            await _settingsStore
                .LoadAsync(
                    cancellationToken);

        if (runtimeSettings is null)
        {
            throw new InvalidOperationException(
                "No fue posible cargar la configuración runtime de TitanMDM.");
        }

        _options.Apply(
            runtimeSettings);

        ValidateFinalOptions(
            _options);

        _logger.LogInformation(
            "Configuración TitanMDM cargada. Archivo={SettingsPath}, ServerUrl={ServerUrl}, Heartbeat={HeartbeatSeconds}s, Polling={PollingSeconds}s",
            _settingsStore
                .GetSettingsFilePath(),
            _options.ServerUrl,
            _options
                .HeartbeatIntervalSeconds,
            _options
                .CommandPollingIntervalSeconds);

        if (string.IsNullOrWhiteSpace(
                _options.EnrollmentToken))
        {
            _logger.LogInformation(
                "No existe EnrollmentToken runtime. Si el dispositivo ya posee identidad, esto es correcto.");
        }
        else
        {
            _logger.LogInformation(
                "EnrollmentToken runtime detectado para inscripción inicial.");
        }
    }

    private static void ValidateFinalOptions(
        AgentOptions options)
    {
        if (string.IsNullOrWhiteSpace(
                options.ServerUrl))
        {
            throw new InvalidOperationException(
                "TitanMDM ServerUrl no puede estar vacío.");
        }

        if (!Uri.TryCreate(
                options.ServerUrl,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                $"TitanMDM ServerUrl inválido: {options.ServerUrl}");
        }

        if (uri.Scheme !=
                Uri.UriSchemeHttp
            &&
            uri.Scheme !=
                Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "TitanMDM ServerUrl debe utilizar HTTP o HTTPS.");
        }

        options.ServerUrl =
            options.ServerUrl
                .Trim()
                .TrimEnd('/');
    }
}