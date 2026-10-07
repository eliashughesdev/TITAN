using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class WindowsSoftwareManager
{
    private readonly ILogger<WindowsSoftwareManager>
        _logger;

    public WindowsSoftwareManager(
        ILogger<WindowsSoftwareManager> logger)
    {
        _logger =
            logger;
    }

    /*
     * ============================================================
     * INSTALL
     * ============================================================
     */

    public async Task<string> InstallAsync(
        WindowsSoftwareInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (string.IsNullOrWhiteSpace(
                request.PackagePath))
        {
            throw new InvalidOperationException(
                "PackagePath es obligatorio.");
        }

        var packagePath =
            Path.GetFullPath(
                request.PackagePath);

        if (!File.Exists(
                packagePath))
        {
            throw new FileNotFoundException(
                "El paquete de software no existe.",
                packagePath);
        }

        var hash =
            await CalculateSha256Async(
                packagePath,
                cancellationToken);

        if (
            string.IsNullOrWhiteSpace(
                request.ExpectedSha256)
            ||
            !string.Equals(
                hash,
                request.ExpectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El paquete no coincide con el SHA-256 autorizado.");
        }

        var extension =
            Path.GetExtension(
                    packagePath)
                .ToLowerInvariant();

        ProcessStartInfo info =
            extension switch
            {
                ".msi" =>
                    CreateMsiInstallStartInfo(
                        packagePath),

                ".exe" =>
                    CreateExeInstallStartInfo(
                        packagePath,
                        request.Arguments),

                ".msix" or ".appx" =>
                    CreateAppxInstallStartInfo(
                        packagePath),

                _ =>
                    throw new InvalidOperationException(
                        $"Formato de paquete no permitido: {extension}")
            };

        var result =
            await ExecuteProcessAsync(
                info,
                request.TimeoutSeconds,
                cancellationToken);

        var successfulExitCodes =
            extension == ".msi"
                ? new[]
                {
                    0,
                    1641,
                    3010
                }
                : new[]
                {
                    0
                };

        var success =
            successfulExitCodes.Contains(
                result.ExitCode);

        _logger.LogInformation(
            "Instalación de {Package}. ExitCode={ExitCode}.",
            Path.GetFileName(
                packagePath),
            result.ExitCode);

        return JsonSerializer.Serialize(
            new
            {
                action =
                    "SOFTWARE_INSTALL",

                success,

                package =
                    Path.GetFileName(
                        packagePath),

                sha256 =
                    hash,

                exitCode =
                    result.ExitCode,

                rebootRequired =
                    result.ExitCode
                    is 1641 or 3010,

                standardOutput =
                    Truncate(
                        result.StandardOutput,
                        32_000),

                standardError =
                    Truncate(
                        result.StandardError,
                        32_000),

                startedAtUtc =
                    result.StartedAtUtc,

                completedAtUtc =
                    result.CompletedAtUtc
            });
    }

    /*
     * ============================================================
     * UNINSTALL
     * ============================================================
     *
     * TitanMDM V1 soporta dos métodos:
     *
     * 1. MSI ProductCode
     *      {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}
     *
     * 2. Ejecutable de desinstalación local.
     *
     * No ejecutamos strings arbitrarios mediante cmd.exe.
     * ============================================================
     */

    public async Task<string> UninstallAsync(
        WindowsSoftwareUninstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var hasProductCode =
            !string.IsNullOrWhiteSpace(
                request.ProductCode);

        var hasExecutable =
            !string.IsNullOrWhiteSpace(
                request.UninstallExecutable);

        if (
            !hasProductCode
            &&
            !hasExecutable)
        {
            throw new InvalidOperationException(
                "Debe especificar ProductCode o UninstallExecutable.");
        }

        if (
            hasProductCode
            &&
            hasExecutable)
        {
            throw new InvalidOperationException(
                "Use ProductCode o UninstallExecutable, no ambos.");
        }

        ProcessStartInfo info;

        string method;

        string target;

        if (hasProductCode)
        {
            var productCode =
                NormalizeProductCode(
                    request.ProductCode!);

            method =
                "MSI";

            target =
                productCode;

            info =
                new ProcessStartInfo
                {
                    FileName =
                        "msiexec.exe",

                    Arguments =
                        $"/x {productCode} /qn /norestart",

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true,

                    UseShellExecute =
                        false,

                    CreateNoWindow =
                        true
                };
        }
        else
        {
            var executable =
                Path.GetFullPath(
                    request.UninstallExecutable!);

            if (!File.Exists(
                    executable))
            {
                throw new FileNotFoundException(
                    "El ejecutable de desinstalación no existe.",
                    executable);
            }

            var extension =
                Path.GetExtension(
                        executable)
                    .ToLowerInvariant();

            if (
                extension != ".exe"
                &&
                extension != ".msi")
            {
                throw new InvalidOperationException(
                    "El desinstalador debe ser .exe o .msi.");
            }

            method =
                extension == ".msi"
                    ? "MSI_FILE"
                    : "EXECUTABLE";

            target =
                executable;

            if (extension == ".msi")
            {
                info =
                    new ProcessStartInfo
                    {
                        FileName =
                            "msiexec.exe",

                        Arguments =
                            $"/x \"{executable}\" /qn /norestart",

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        UseShellExecute =
                            false,

                        CreateNoWindow =
                            true
                    };
            }
            else
            {
                info =
                    new ProcessStartInfo
                    {
                        FileName =
                            executable,

                        Arguments =
                            request.Arguments
                            ??
                            string.Empty,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        UseShellExecute =
                            false,

                        CreateNoWindow =
                            true
                    };
            }
        }

        var result =
            await ExecuteProcessAsync(
                info,
                request.TimeoutSeconds,
                cancellationToken);

        var success =
            result.ExitCode
            is 0
            or 1605
            or 1641
            or 3010;

        _logger.LogInformation(
            "Desinstalación de software. Method={Method} Target={Target} ExitCode={ExitCode}.",
            method,
            target,
            result.ExitCode);

        return JsonSerializer.Serialize(
            new
            {
                action =
                    "SOFTWARE_UNINSTALL",

                success,

                method,

                target,

                exitCode =
                    result.ExitCode,

                alreadyAbsent =
                    result.ExitCode ==
                    1605,

                rebootRequired =
                    result.ExitCode
                    is 1641 or 3010,

                standardOutput =
                    Truncate(
                        result.StandardOutput,
                        32_000),

                standardError =
                    Truncate(
                        result.StandardError,
                        32_000),

                startedAtUtc =
                    result.StartedAtUtc,

                completedAtUtc =
                    result.CompletedAtUtc
            });
    }

    /*
     * ============================================================
     * INSTALL PROCESS BUILDERS
     * ============================================================
     */

    private static ProcessStartInfo
        CreateMsiInstallStartInfo(
            string packagePath)
    {
        return new ProcessStartInfo
        {
            FileName =
                "msiexec.exe",

            Arguments =
                $"/i \"{packagePath}\" /qn /norestart",

            RedirectStandardOutput =
                true,

            RedirectStandardError =
                true,

            UseShellExecute =
                false,

            CreateNoWindow =
                true
        };
    }

    private static ProcessStartInfo
        CreateExeInstallStartInfo(
            string packagePath,
            string? arguments)
    {
        return new ProcessStartInfo
        {
            FileName =
                packagePath,

            Arguments =
                arguments
                ??
                string.Empty,

            RedirectStandardOutput =
                true,

            RedirectStandardError =
                true,

            UseShellExecute =
                false,

            CreateNoWindow =
                true
        };
    }

    private static ProcessStartInfo
        CreateAppxInstallStartInfo(
            string packagePath)
    {
        var escaped =
            packagePath.Replace(
                "'",
                "''",
                StringComparison.Ordinal);

        var script =
            $"Add-AppxPackage -Path '{escaped}'";

        var encoded =
            Convert.ToBase64String(
                Encoding.Unicode
                    .GetBytes(
                        script));

        return new ProcessStartInfo
        {
            FileName =
                "powershell.exe",

            Arguments =
                "-NoLogo -NoProfile -NonInteractive " +
                $"-EncodedCommand {encoded}",

            RedirectStandardOutput =
                true,

            RedirectStandardError =
                true,

            UseShellExecute =
                false,

            CreateNoWindow =
                true
        };
    }

    /*
     * ============================================================
     * PROCESS EXECUTION
     * ============================================================
     */

    private static async Task<
        SoftwareProcessResult>
        ExecuteProcessAsync(
            ProcessStartInfo info,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        var timeout =
            Math.Clamp(
                timeoutSeconds,
                30,
                7200);

        using var timeoutCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(
                timeout));

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
                process
                    .StandardOutput
                    .ReadToEndAsync(
                        timeoutCts.Token);

            var stderrTask =
                process
                    .StandardError
                    .ReadToEndAsync(
                        timeoutCts.Token);

            await process
                .WaitForExitAsync(
                    timeoutCts.Token);

            var stdout =
                await stdoutTask;

            var stderr =
                await stderrTask;

            return new SoftwareProcessResult(
                process.ExitCode,
                stdout,
                stderr,
                startedAt,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(
                process);

            throw new TimeoutException(
                "La operación de software excedió el tiempo máximo permitido.");
        }
    }

    /*
     * ============================================================
     * MSI PRODUCT CODE
     * ============================================================
     */

    private static string
        NormalizeProductCode(
            string productCode)
    {
        var value =
            productCode.Trim();

        if (
            !Guid.TryParse(
                value,
                out var guid))
        {
            throw new InvalidOperationException(
                "ProductCode no contiene un GUID MSI válido.");
        }

        return
            "{"
            +
            guid.ToString()
                .ToUpperInvariant()
            +
            "}";
    }

    /*
     * ============================================================
     * SHA-256
     * ============================================================
     */

    private static async Task<string>
        CalculateSha256Async(
            string path,
            CancellationToken cancellationToken)
    {
        await using var stream =
            File.OpenRead(
                path);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert
            .ToHexString(
                hash);
    }

    /*
     * ============================================================
     * HELPERS
     * ============================================================
     */

    private static string Truncate(
        string value,
        int maximumLength)
    {
        if (
            string.IsNullOrEmpty(
                value))
        {
            return string.Empty;
        }

        return value.Length <=
            maximumLength
            ? value.Trim()
            : value[..maximumLength]
                .Trim();
    }

    private static void TryKill(
        Process process)
    {
        try
        {
            if (
                !process.HasExited)
            {
                process.Kill(
                    entireProcessTree:
                        true);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private sealed record
        SoftwareProcessResult(
            int ExitCode,
            string StandardOutput,
            string StandardError,
            DateTime StartedAtUtc,
            DateTime CompletedAtUtc);
}

/*
 * ================================================================
 * CONTRACTS
 * ================================================================
 */

public sealed record WindowsSoftwareInstallRequest(
    string PackagePath,
    string ExpectedSha256,
    string? Arguments,
    int TimeoutSeconds);

public sealed record WindowsSoftwareUninstallRequest(
    string? ProductCode,
    string? UninstallExecutable,
    string? Arguments,
    int TimeoutSeconds);