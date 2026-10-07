using System.Diagnostics;
using System.Text.Json;

using TitanMDM.WindowsAgent.Interop;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class WindowsActionExecutor
{
    private readonly ILogger<
        WindowsActionExecutor> _logger;

    private readonly ActiveSessionProcessLauncher
        _activeSessionLauncher;

    public WindowsActionExecutor(
        ActiveSessionProcessLauncher activeSessionLauncher,
        ILogger<WindowsActionExecutor> logger)
    {
        _activeSessionLauncher =
            activeSessionLauncher;

        _logger =
            logger;
    }

    // ============================================================
    // LOCK DEVICE
    // ============================================================

    public Task<string>
        LockDeviceAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        /*
         * IMPORTANTE
         * ==========
         *
         * TitanMDM.WindowsAgent.exe corre como Windows Service,
         * normalmente dentro de Session 0.
         *
         * Ejecutar:
         *
         * rundll32 user32.dll,LockWorkStation
         *
         * directamente desde el servicio puede bloquear Session 0
         * y NO la sesión interactiva del usuario.
         *
         * ActiveSessionProcessLauncher crea el proceso dentro de la
         * sesión activa del usuario.
         */

        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            windowsDirectory =
                @"C:\Windows";
        }

        var rundll32Path =
            Path.Combine(
                windowsDirectory,
                "System32",
                "rundll32.exe");

        if (!File.Exists(
                rundll32Path))
        {
            throw new FileNotFoundException(
                "TitanMDM no encontró rundll32.exe.",
                rundll32Path);
        }

        var activeSessionId =
            _activeSessionLauncher
                .GetActiveConsoleSessionId();

        if (!activeSessionId.HasValue)
        {
            throw new InvalidOperationException(
                "No existe una sesión interactiva activa para bloquear.");
        }

        _logger.LogInformation(
            "TitanMDM bloqueará la sesión interactiva Windows {WindowsSessionId}.",
            activeSessionId.Value);

        var launchResult =
            _activeSessionLauncher
                .Launch(
                    rundll32Path,
                    "user32.dll,LockWorkStation",
                    Path.GetDirectoryName(
                        rundll32Path)
                    ??
                    windowsDirectory);

        _logger.LogWarning(
            "LOCK_DEVICE ejecutado. PID={ProcessId}, WindowsSession={WindowsSessionId}, LaunchMode={LaunchMode}.",
            launchResult.ProcessId,
            launchResult.WindowsSessionId,
            launchResult.LaunchMode);

        return Task.FromResult(
            JsonSerializer.Serialize(
                new
                {
                    action =
                        "LOCK_DEVICE",

                    accepted =
                        true,

                    targetWindowsSessionId =
                        launchResult
                            .WindowsSessionId,

                    processId =
                        launchResult
                            .ProcessId,

                    launchMode =
                        launchResult
                            .LaunchMode,

                    executedAtUtc =
                        DateTime.UtcNow
                }));
    }

    // ============================================================
    // RESTART DEVICE
    // ============================================================

    public Task<string>
        RestartDeviceAsync(
            CancellationToken cancellationToken = default)
    {
        return ExecuteShutdownCommandAsync(
            arguments:
                "/r /t 5 /f /d p:4:1",

            action:
                "RESTART_DEVICE",

            cancellationToken:
                cancellationToken);
    }

    // ============================================================
    // SHUTDOWN DEVICE
    // ============================================================

    public Task<string>
        ShutdownDeviceAsync(
            CancellationToken cancellationToken = default)
    {
        return ExecuteShutdownCommandAsync(
            arguments:
                "/s /t 5 /f /d p:4:1",

            action:
                "SHUTDOWN_DEVICE",

            cancellationToken:
                cancellationToken);
    }

    // ============================================================
    // PROCESS TERMINATION
    // ============================================================

    public async Task<string>
        TerminateProcessAsync(
            int processId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        /*
         * PID 0 = Idle
         * PID 4 = System
         *
         * Nunca deben tocarse.
         */

        if (processId <= 4)
        {
            throw new InvalidOperationException(
                "TitanMDM no permite finalizar procesos críticos del sistema.");
        }

        Process process;

        try
        {
            process =
                Process.GetProcessById(
                    processId);
        }
        catch (
            ArgumentException)
        {
            throw new InvalidOperationException(
                $"El proceso PID {processId} ya no existe.");
        }

        using (process)
        {
            var processName =
                process.ProcessName;

            _logger.LogWarning(
                "TitanMDM solicitó finalizar proceso {ProcessName} ({ProcessId}).",
                processName,
                processId);

            process.Kill(
                entireProcessTree:
                    true);

            await process
                .WaitForExitAsync(
                    cancellationToken);

            if (!process.HasExited)
            {
                throw new InvalidOperationException(
                    $"El proceso {processName} ({processId}) no finalizó.");
            }

            _logger.LogWarning(
                "TitanMDM finalizó correctamente proceso {ProcessName} ({ProcessId}).",
                processName,
                processId);

            return JsonSerializer.Serialize(
                new
                {
                    action =
                        "PROCESS_TERMINATE",

                    processId,

                    processName,

                    success =
                        true,

                    executedAtUtc =
                        DateTime.UtcNow
                });
        }
    }

    // ============================================================
    // SHUTDOWN / RESTART EXECUTION
    // ============================================================

    private async Task<string>
        ExecuteShutdownCommandAsync(
            string arguments,
            string action,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            windowsDirectory =
                @"C:\Windows";
        }

        var shutdownPath =
            Path.Combine(
                windowsDirectory,
                "System32",
                "shutdown.exe");

        if (!File.Exists(
                shutdownPath))
        {
            throw new FileNotFoundException(
                "TitanMDM no encontró shutdown.exe.",
                shutdownPath);
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    shutdownPath,

                Arguments =
                    arguments,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        shutdownPath)
                    ??
                    windowsDirectory,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true
            };

        _logger.LogWarning(
            "TitanMDM ejecutará acción {Action}.",
            action);

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Windows rechazó la ejecución de {action}.");
        }

        var standardOutputTask =
            process.StandardOutput
                .ReadToEndAsync(
                    cancellationToken);

        var standardErrorTask =
            process.StandardError
                .ReadToEndAsync(
                    cancellationToken);

        await process
            .WaitForExitAsync(
                cancellationToken);

        var standardOutput =
            await standardOutputTask;

        var standardError =
            await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{action} falló. ExitCode={process.ExitCode}. " +
                $"{NormalizeOutput(standardError)}");
        }

        _logger.LogWarning(
            "TitanMDM aceptó {Action}. ExitCode={ExitCode}.",
            action,
            process.ExitCode);

        return JsonSerializer.Serialize(
            new
            {
                action,

                accepted =
                    true,

                exitCode =
                    process.ExitCode,

                output =
                    NormalizeOutput(
                        standardOutput),

                executedAtUtc =
                    DateTime.UtcNow
            });
    }

    private static string?
        NormalizeOutput(
            string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value
            .Trim();
    }
}