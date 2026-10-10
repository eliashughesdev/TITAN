
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class DeviceCommandNotificationService : BackgroundService
{
    private readonly DeviceIdentityStore _identityStore;
    private readonly CommandWakeSignal _wakeSignal;
    private readonly AgentOptions _options;
    private readonly ILogger<DeviceCommandNotificationService> _logger;

    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];

    public DeviceCommandNotificationService(
        DeviceIdentityStore identityStore,
        CommandWakeSignal wakeSignal,
        IOptions<AgentOptions> options,
        ILogger<DeviceCommandNotificationService> logger)
    {
        _identityStore = identityStore;
        _wakeSignal = wakeSignal;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WIN-C: servicio de notificaciones de comandos iniciado.");

        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            HubConnection? connection = null;

            try
            {
                var identity = await _identityStore.LoadAsync(
                    stoppingToken);

                if (identity is null)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);

                    continue;
                }

                var disconnected =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                var hubUrl =
                    $"{_options.ServerUrl.TrimEnd('/')}/hubs/device-commands";

                connection = new HubConnectionBuilder()
                    .WithUrl(
                        hubUrl,
                        connectionOptions =>
                        {
                            connectionOptions.Headers[
                                "X-Titan-Device-Id"] =
                                identity.DeviceId.ToString();

                            connectionOptions.Headers[
                                "X-Titan-Device-Secret"] =
                                identity.DeviceSecret;
                        })
                    .WithAutomaticReconnect(ReconnectDelays)
                    .Build();

                connection.On<Guid>(
                    "CommandAvailable",
                    commandId =>
                    {
                        _logger.LogDebug(
                            "WIN-C: comando anunciado: {CommandId}",
                            commandId);

                        // El Worker consulta al backend:
                        // el evento no transporta una orden ejecutable.
                        _wakeSignal.Pulse();
                    });

                connection.Reconnecting += exception =>
                {
                    _logger.LogWarning(
                        exception,
                        "WIN-C: canal SignalR reconectando.");

                    return Task.CompletedTask;
                };

                connection.Reconnected += connectionId =>
                {
                    _logger.LogInformation(
                        "WIN-C: SignalR reconectado. ConnectionId={ConnectionId}",
                        connectionId);

                    // Recuperar órdenes que pudieran haberse
                    // anunciado durante la desconexión.
                    _wakeSignal.Pulse();

                    return Task.CompletedTask;
                };

                connection.Closed += exception =>
                {
                    if (exception is not null)
                    {
                        _logger.LogWarning(
                            exception,
                            "WIN-C: canal SignalR cerrado.");
                    }

                    disconnected.TrySetResult(true);

                    return Task.CompletedTask;
                };

                await connection.StartAsync(stoppingToken);

                failures = 0;

                _logger.LogInformation(
                    "WIN-C: notificaciones conectadas. DeviceId={DeviceId}",
                    identity.DeviceId);

                // Consulta inicial para recuperar comandos pendientes.
                _wakeSignal.Pulse();

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                failures++;

                _logger.LogWarning(
                    exception,
                    "WIN-C: conexión de notificaciones fallida. Intento={Attempt}",
                    failures);
            }
            finally
            {
                if (connection is not null)
                {
                    try
                    {
                        await connection.DisposeAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "WIN-C: error liberando la conexión SignalR.");
                    }
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Incluso sin SignalR, el Worker conserva
            // su mecanismo de sondeo HTTP.
            var exponent = Math.Min(failures, 5);

            var seconds = Math.Min(
                30,
                2 * (1 << exponent));

            var jitter = Random.Shared.Next(250, 1250);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(seconds) +
                    TimeSpan.FromMilliseconds(jitter),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "WIN-C: servicio de notificaciones detenido.");
    }
}
