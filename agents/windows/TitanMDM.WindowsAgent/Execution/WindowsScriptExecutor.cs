using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class WindowsScriptExecutor
{
    private const int MaximumScriptBytes =
        1024 * 1024;

    private readonly ILogger<WindowsScriptExecutor>
        _logger;

    public WindowsScriptExecutor(
        ILogger<WindowsScriptExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        WindowsScriptExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(
                request.ScriptPath))
        {
            throw new ArgumentException(
                "ScriptPath es obligatorio.");
        }

        var fullPath =
            Path.GetFullPath(
                request.ScriptPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "El script autorizado no existe.",
                fullPath);
        }

        var fileInfo =
            new FileInfo(fullPath);

        if (fileInfo.Length > MaximumScriptBytes)
        {
            throw new InvalidOperationException(
                "El script excede el tamaño máximo permitido.");
        }

        var actualHash =
            await CalculateSha256Async(
                fullPath,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(
                request.ExpectedSha256)
            ||
            !string.Equals(
                actualHash,
                request.ExpectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La integridad SHA-256 del script no coincide con la autorizada.");
        }

        var extension =
            Path.GetExtension(fullPath)
                .ToLowerInvariant();

        var executable =
            extension switch
            {
                ".ps1" =>
                    "powershell.exe",

                ".cmd" or ".bat" =>
                    "cmd.exe",

                _ =>
                    throw new InvalidOperationException(
                        $"Tipo de script no permitido: {extension}")
            };

        var arguments =
            extension == ".ps1"
                ? $"-NoLogo -NoProfile -NonInteractive -File \"{fullPath}\""
                : $"/d /s /c \"\"{fullPath}\"\"";

        var timeoutSeconds =
            Math.Clamp(
                request.TimeoutSeconds,
                5,
                3600);

        using var timeoutCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(
                timeoutSeconds));

        var info =
            new ProcessStartInfo
            {
                FileName =
                    executable,

                Arguments =
                    arguments,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        fullPath)
                    ?? Environment.CurrentDirectory
            };

        using var process =
            new Process
            {
                StartInfo =
                    info
            };

        var startedAt =
            DateTime.UtcNow;

        try
        {
            process.Start();

            var stdoutTask =
                process.StandardOutput
                    .ReadToEndAsync(
                        timeoutCts.Token);

            var stderrTask =
                process.StandardError
                    .ReadToEndAsync(
                        timeoutCts.Token);

            await process.WaitForExitAsync(
                timeoutCts.Token);

            var stdout =
                await stdoutTask;

            var stderr =
                await stderrTask;

            var result =
                new
                {
                    action =
                        "SCRIPT_EXECUTE",

                    success =
                        process.ExitCode == 0,

                    exitCode =
                        process.ExitCode,

                    scriptName =
                        Path.GetFileName(
                            fullPath),

                    sha256 =
                        actualHash,

                    standardOutput =
                        Truncate(stdout, 64_000),

                    standardError =
                        Truncate(stderr, 64_000),

                    startedAtUtc =
                        startedAt,

                    completedAtUtc =
                        DateTime.UtcNow
                };

            _logger.LogInformation(
                "Script autorizado {ScriptName} ejecutado. ExitCode={ExitCode}.",
                Path.GetFileName(fullPath),
                process.ExitCode);

            return JsonSerializer.Serialize(
                result);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);

            throw new TimeoutException(
                $"El script superó el timeout de {timeoutSeconds} segundos.");
        }
    }

    private static async Task<string>
        CalculateSha256Async(
            string path,
            CancellationToken cancellationToken)
    {
        await using var stream =
            File.OpenRead(path);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert
            .ToHexString(hash);
    }

    private static string Truncate(
        string value,
        int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value.Trim();
        }

        return value[..maximumLength]
            .Trim();
    }

    private static void TryKill(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
            // El proceso puede haber finalizado
            // entre ambas comprobaciones.
        }
    }
}

public sealed record WindowsScriptExecutionRequest(
    string ScriptPath,
    string ExpectedSha256,
    int TimeoutSeconds);