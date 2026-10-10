using TitanMDM.Application.Commands.Agent;

namespace TitanMDM.Api.Services;

public sealed class DeviceCommandExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceCommandExpirationService> _logger;

    public DeviceCommandExpirationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceCommandExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var commands = scope.ServiceProvider
                    .GetRequiredService<IDeviceCommandAgentService>();

                await commands.ExpireStaleCommandsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Device command expiration sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
