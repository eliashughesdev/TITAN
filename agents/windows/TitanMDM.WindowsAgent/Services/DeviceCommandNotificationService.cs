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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            HubConnection? connection = null;

            try
            {
                var identity = await _identityStore.LoadAsync(stoppingToken);

                if (identity is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var closed = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                var hubUrl =
                    $"{_options.ServerUrl.TrimEnd('/')}/hubs/device-commands";

                connection = new HubConnectionBuilder()
                    .WithUrl(
                        hubUrl,
                        connectionOptions =>
                        {
                            connectionOptions.Headers["X-Titan-Device-Id"] =
                                identity.DeviceId.ToString();

                            connectionOptions.Headers["X-Titan-Device-Secret"] =
                                identity.DeviceSecret;
                        })
                    .WithAutomaticReconnect(
                    [
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(20),
                        TimeSpan.FromSeconds(30)
                    ])
                    .Build();

                connection.On<Guid>(
                    "CommandAvailable",
                    commandId =>
                    {
                        _logger.LogDebug(
                            "Immediate command notification received. CommandId={CommandId}.",
                            commandId);

                        _wakeSignal.Pulse();
                    });

                connection.Reconnected += _ =>
                {
                    _wakeSignal.Pulse();
                    return Task.CompletedTask;
                };

                connection.Closed += exception =>
                {
                    closed.TrySetResult();

                    if (exception is not null)
                    {
                        _logger.LogWarning(
                            exception,
                            "Device command notification channel closed.");
                    }

                    return Task.CompletedTask;
                };

                await connection.StartAsync(stoppingToken);
                failures = 0;
                _wakeSignal.Pulse();

                _logger.LogInformation(
                    "Device command notification channel connected. DeviceId={DeviceId}.",
                    identity.DeviceId);

                await closed.Task.WaitAsync(stoppingToken);
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
                    "Unable to connect the device command notification channel. Attempt={Attempt}.",
                    failures);
            }
            finally
            {
                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }

            var delaySeconds = Math.Min(
                60,
                2 * (1 << Math.Min(failures, 5)));

            var jitter = Random.Shared.Next(250, 1500);
            await Task.Delay(
                TimeSpan.FromSeconds(delaySeconds) +
                TimeSpan.FromMilliseconds(jitter),
                stoppingToken);
        }
    }
}
