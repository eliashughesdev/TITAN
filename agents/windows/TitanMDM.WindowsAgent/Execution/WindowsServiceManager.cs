using System.Diagnostics;
using System.Text.Json;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class WindowsServiceManager
{
    private readonly ILogger<WindowsServiceManager> _logger;

    public WindowsServiceManager(
        ILogger<WindowsServiceManager> logger)
    {
        _logger = logger;
    }

    public Task<string> StartAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            serviceName,
            "start",
            cancellationToken);
    }

    public Task<string> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            serviceName,
            "stop",
            cancellationToken);
    }

    public Task<string> RestartAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            serviceName,
            "restart",
            cancellationToken);
    }

    private async Task<string> ExecuteAsync(
        string serviceName,
        string action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException(
                "ServiceName es obligatorio.",
                nameof(serviceName));
        }

        var normalizedName =
            serviceName.Trim();

        ValidateServiceName(normalizedName);

        var result =
            action switch
            {
                "start" =>
                    await RunScAsync(
                        $"start \"{normalizedName}\"",
                        cancellationToken),

                "stop" =>
                    await RunScAsync(
                        $"stop \"{normalizedName}\"",
                        cancellationToken),

                "restart" =>
                    await RestartInternalAsync(
                        normalizedName,
                        cancellationToken),

                _ =>
                    throw new InvalidOperationException(
                        $"Acción de servicio no soportada: {action}")
            };

        if (!result.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError);
        }

        _logger.LogInformation(
            "Servicio {ServiceName}: acción {Action} ejecutada.",
            normalizedName,
            action);

        return JsonSerializer.Serialize(
            new
            {
                action =
                    $"SERVICE_{action.ToUpperInvariant()}",

                serviceName =
                    normalizedName,

                success =
                    true,

                output =
                    result.StandardOutput,

                executedAtUtc =
                    DateTime.UtcNow
            });
    }

    private async Task<ScResult> RestartInternalAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var stop =
            await RunScAsync(
                $"stop \"{serviceName}\"",
                cancellationToken);

        // Un servicio ya detenido no debe impedir
        // intentar el arranque posterior.
        await Task.Delay(
            TimeSpan.FromSeconds(2),
            cancellationToken);

        var start =
            await RunScAsync(
                $"start \"{serviceName}\"",
                cancellationToken);

        return new ScResult(
            start.Success,
            start.ExitCode,
            $"{stop.StandardOutput}\n{start.StandardOutput}".Trim(),
            $"{stop.StandardError}\n{start.StandardError}".Trim());
    }

    private static void ValidateServiceName(
        string serviceName)
    {
        if (serviceName.Length > 256)
        {
            throw new ArgumentException(
                "El nombre del servicio excede el tamaño permitido.");
        }

        var invalid =
            new[]
            {
                '\r',
                '\n',
                '&',
                '|',
                '>',
                '<',
                '^'
            };

        if (serviceName.IndexOfAny(invalid) >= 0)
        {
            throw new ArgumentException(
                "El nombre del servicio contiene caracteres no permitidos.");
        }
    }

    private static async Task<ScResult> RunScAsync(
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    "sc.exe",

                Arguments =
                    arguments,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true
            };

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        process.Start();

        var stdoutTask =
            process.StandardOutput
                .ReadToEndAsync(
                    cancellationToken);

        var stderrTask =
            process.StandardError
                .ReadToEndAsync(
                    cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        return new ScResult(
            process.ExitCode == 0,
            process.ExitCode,
            (await stdoutTask).Trim(),
            (await stderrTask).Trim());
    }

    private sealed record ScResult(
        bool Success,
        int ExitCode,
        string StandardOutput,
        string StandardError);
}