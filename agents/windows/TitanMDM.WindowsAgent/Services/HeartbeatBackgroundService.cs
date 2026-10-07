using Microsoft.Extensions.Options;

using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class HeartbeatBackgroundService
    : BackgroundService
{
    private const int MinimumHeartbeatSeconds =
        15;

    private const int MaximumBackoffSeconds =
        300;

    private const int MaximumBackoffExponent =
        6;

    private readonly ILogger<
        HeartbeatBackgroundService>
        _logger;

    private readonly TitanMdmApiClient
        _apiClient;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly AgentLifecycleCoordinator
        _lifecycle;

    private readonly AgentOptions
        _options;

    public HeartbeatBackgroundService(
        ILogger<HeartbeatBackgroundService> logger,
        TitanMdmApiClient apiClient,
        DeviceIdentityStore identityStore,
        AgentLifecycleCoordinator lifecycle,
        IOptions<AgentOptions> options)
    {
        _logger =
            logger;

        _apiClient =
            apiClient;

        _identityStore =
            identityStore;

        _lifecycle =
            lifecycle;

        _options =
            options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TitanMDM Heartbeat Service iniciado.");

        var consecutiveFailures =
            0;

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            var heartbeatSucceeded =
                false;

            try
            {
                var identity =
                    await _identityStore
                        .LoadAsync(
                            stoppingToken);

                if (
                    identity is null)
                {
                    consecutiveFailures =
                        0;

                    if (
                        _lifecycle.State is
                            not AgentLifecycleState.Enrolling
                        and
                            not AgentLifecycleState.TokenExpired
                        and
                            not AgentLifecycleState.TokenInvalid
                        and
                            not AgentLifecycleState.RecoveryRequired)
                    {
                        _lifecycle
                            .MarkEnrollmentRequired(
                                "Heartbeat esperando identidad TitanMDM.");
                    }

                    _logger.LogDebug(
                        "Heartbeat omitido: dispositivo todavía no inscrito.");
                }
                else
                {
                    var heartbeat =
                        await _apiClient
                            .SendHeartbeatAsync(
                                stoppingToken);

                    heartbeatSucceeded =
                        true;

                    consecutiveFailures =
                        0;

                    _lifecycle
                        .MarkHealthy();

                    _logger.LogDebug(
                        "Heartbeat correcto. DeviceId: {DeviceId}, Estado: {Status}, LastSeen: {LastSeenAtUtc}",
                        heartbeat.DeviceId,
                        heartbeat.Status,
                        heartbeat.LastSeenAtUtc);
                }
            }
            catch (
                OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
            catch (
                HttpRequestException ex)
            {
                consecutiveFailures++;

                _lifecycle
                    .MarkServerUnavailable(
                        ex.Message);

                _logger.LogWarning(
                    ex,
                    "No fue posible enviar heartbeat a TitanMDM. Fallos consecutivos: {FailureCount}.",
                    consecutiveFailures);
            }
            catch (
                TaskCanceledException ex)
                when (
                    !stoppingToken
                        .IsCancellationRequested)
            {
                consecutiveFailures++;

                _lifecycle
                    .MarkServerUnavailable(
                        "Timeout enviando heartbeat.");

                _logger.LogWarning(
                    ex,
                    "Timeout enviando heartbeat a TitanMDM. Fallos consecutivos: {FailureCount}.",
                    consecutiveFailures);
            }
            catch (
                Exception ex)
            {
                consecutiveFailures++;

                _logger.LogError(
                    ex,
                    "Error inesperado en TitanMDM Heartbeat Service. Fallos consecutivos: {FailureCount}.",
                    consecutiveFailures);
            }

            var delay =
                CalculateDelay(
                    heartbeatSucceeded,
                    consecutiveFailures);

            try
            {
                await Task.Delay(
                    delay,
                    stoppingToken);
            }
            catch (
                OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "TitanMDM Heartbeat Service detenido.");
    }

    private TimeSpan CalculateDelay(
        bool heartbeatSucceeded,
        int consecutiveFailures)
    {
        var configuredIntervalSeconds =
            Math.Clamp(
                _options
                    .HeartbeatIntervalSeconds,
                MinimumHeartbeatSeconds,
                3600);

        if (
            heartbeatSucceeded
            ||
            consecutiveFailures <=
                0)
        {
            return TimeSpan.FromSeconds(
                configuredIntervalSeconds);
        }

        var exponent =
            Math.Min(
                consecutiveFailures - 1,
                MaximumBackoffExponent);

        var backoffSeconds =
            Math.Min(
                MaximumBackoffSeconds,
                5 * (1 << exponent));

        var retrySeconds =
            Math.Max(
                MinimumHeartbeatSeconds,
                backoffSeconds);

        /*
         * Jitter evita que cientos de agentes reconecten
         * exactamente al mismo tiempo después de una
         * caída de red/servidor.
         */

        var jitterMilliseconds =
            Random.Shared.Next(
                250,
                2500);

        return
            TimeSpan.FromSeconds(
                retrySeconds)
            +
            TimeSpan.FromMilliseconds(
                jitterMilliseconds);
    }
}