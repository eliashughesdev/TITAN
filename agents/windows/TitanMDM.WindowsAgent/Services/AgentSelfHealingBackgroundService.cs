namespace TitanMDM.WindowsAgent.Services;

public sealed class
    AgentSelfHealingBackgroundService
    : BackgroundService
{
    private readonly
        AgentSelfHealingService
        _selfHealing;

    private readonly ILogger<
        AgentSelfHealingBackgroundService>
        _logger;

    public AgentSelfHealingBackgroundService(
        AgentSelfHealingService selfHealing,
        ILogger<
            AgentSelfHealingBackgroundService>
            logger)
    {
        _selfHealing =
            selfHealing;

        _logger =
            logger;
    }

    protected override async Task
        ExecuteAsync(
            CancellationToken stoppingToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(20),
            stoppingToken);

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            try
            {
                await _selfHealing
                    .RepairAsync(
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
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "TitanMDM Self-Healing falló.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(10),
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
    }
}