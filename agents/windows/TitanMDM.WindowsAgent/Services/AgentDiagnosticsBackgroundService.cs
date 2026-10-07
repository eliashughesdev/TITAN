namespace TitanMDM.WindowsAgent.Services;

public sealed class
    AgentDiagnosticsBackgroundService
    : BackgroundService
{
    private readonly
        AgentDiagnosticsService
        _diagnostics;

    private readonly ILogger<
        AgentDiagnosticsBackgroundService>
        _logger;

    public AgentDiagnosticsBackgroundService(
        AgentDiagnosticsService diagnostics,
        ILogger<
            AgentDiagnosticsBackgroundService>
            logger)
    {
        _diagnostics =
            diagnostics;

        _logger =
            logger;
    }

    protected override async Task
        ExecuteAsync(
            CancellationToken stoppingToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(15),
            stoppingToken);

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            try
            {
                var snapshot =
                    await _diagnostics
                        .CaptureAsync(
                            stoppingToken);

                if (
                    !snapshot
                        .ServerReachable)
                {
                    _logger.LogWarning(
                        "Diagnóstico TitanMDM: servidor no disponible. {Error}",
                        snapshot.ServerError);
                }

                if (
                    !snapshot
                        .IdentityReadable)
                {
                    _logger.LogWarning(
                        "Diagnóstico TitanMDM: identidad ausente o ilegible.");
                }

                if (
                    !snapshot
                        .RemoteHostExists)
                {
                    _logger.LogWarning(
                        "Diagnóstico TitanMDM: RemoteHost ausente.");
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
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error realizando diagnóstico TitanMDM.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(5),
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