using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Interop;

namespace TitanMDM.WindowsAgent.Services;

public sealed class RemoteDesktopHostLauncher
{
    private static readonly TimeSpan StartupProbeDelay =
        TimeSpan.FromMilliseconds(1500);

    private readonly ILogger<RemoteDesktopHostLauncher>
        _logger;

    private readonly ActiveSessionProcessLauncher
        _activeSessionLauncher;

    private readonly object
        _syncRoot =
            new();

    private int?
        _hostProcessId;

    private int?
        _windowsSessionId;

    private Guid?
        _remoteSessionId;

    private string?
        _lastExecutablePath;

    private string?
        _lastExecutableSha256;

    private string?
        _lastLaunchMode;

    private DateTime?
        _lastStartedAtUtc;

    public RemoteDesktopHostLauncher(
        ActiveSessionProcessLauncher activeSessionLauncher,
        ILogger<RemoteDesktopHostLauncher> logger)
    {
        _activeSessionLauncher =
            activeSessionLauncher;

        _logger =
            logger;
    }

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                CleanupExitedProcessUnsafe();

                return
                    IsProcessRunningUnsafe();
            }
        }
    }

    public int? RunningWindowsSessionId
    {
        get
        {
            lock (_syncRoot)
            {
                CleanupExitedProcessUnsafe();

                return
                    _windowsSessionId;
            }
        }
    }

    public int?
        ActiveConsoleSessionId =>
            _activeSessionLauncher
                .GetActiveConsoleSessionId();

    public RemoteHostStatus
        GetStatus()
    {
        lock (_syncRoot)
        {
            CleanupExitedProcessUnsafe();

            return new RemoteHostStatus(
                IsRunning:
                    IsProcessRunningUnsafe(),

                ProcessId:
                    _hostProcessId,

                WindowsSessionId:
                    _windowsSessionId,

                RemoteSessionId:
                    _remoteSessionId,

                ExecutablePath:
                    _lastExecutablePath,

                ExecutableSha256:
                    _lastExecutableSha256,

                LaunchMode:
                    _lastLaunchMode,

                StartedAtUtc:
                    _lastStartedAtUtc);
        }
    }

    public async Task StartAsync(
        RemoteDesktopStartRequest request,
        CancellationToken cancellationToken =
            default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        string executablePath;

        ProcessLaunchResult launchResult;

        lock (_syncRoot)
        {
            CleanupExitedProcessUnsafe();

            if (IsProcessRunningUnsafe())
            {
                if (
                    _remoteSessionId ==
                    request.SessionId)
                {
                    _logger.LogInformation(
                        "RemoteHost ya está activo para RemoteSession={SessionId}. PID={Pid}.",
                        request.SessionId,
                        _hostProcessId);

                    return;
                }

                throw new InvalidOperationException(
                    "Ya existe una sesión RemoteHost activa. " +
                    $"RemoteSession={_remoteSessionId}, " +
                    $"PID={_hostProcessId}.");
            }

            executablePath =
                ResolveRemoteHostPath();

            ValidateRemoteHostPath(
                executablePath);

            var sha256 =
                ComputeSha256(
                    executablePath);

            _logger.LogInformation(
                "RemoteHost validado. Path={Path}, SHA256={Sha256}.",
                executablePath,
                sha256);

            var payload =
                JsonSerializer.Serialize(
                    request);

            var encodedPayload =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        payload));

            var arguments =
                $"--session \"{encodedPayload}\"";

            var workingDirectory =
                Path.GetDirectoryName(
                    executablePath)
                ??
                AppContext.BaseDirectory;

            _logger.LogInformation(
                "Iniciando TitanMDM RemoteHost. RemoteSession={SessionId}.",
                request.SessionId);

            launchResult =
                _activeSessionLauncher
                    .Launch(
                        executablePath,
                        arguments,
                        workingDirectory);

            _hostProcessId =
                launchResult.ProcessId;

            _windowsSessionId =
                launchResult.WindowsSessionId;

            _remoteSessionId =
                request.SessionId;

            _lastExecutablePath =
                executablePath;

            _lastExecutableSha256 =
                sha256;

            _lastLaunchMode =
                launchResult.LaunchMode;

            _lastStartedAtUtc =
                DateTime.UtcNow;
        }

        /*
         * ========================================================
         * STARTUP PROBE
         * ========================================================
         *
         * CreateProcess puede devolver correctamente aunque
         * Defender / ASR termine el proceso inmediatamente.
         *
         * Esperamos brevemente y comprobamos que RemoteHost
         * siga vivo.
         * ========================================================
         */

        await Task.Delay(
            StartupProbeDelay,
            cancellationToken);

        Process? process =
            null;

        try
        {
            process =
                Process.GetProcessById(
                    launchResult.ProcessId);

            if (process.HasExited)
            {
                throw new RemoteHostLaunchException(
                    "REMOTE_HOST_EXITED_IMMEDIATELY",
                    "TitanMDM RemoteHost terminó inmediatamente después de iniciarse. " +
                    "Revise Microsoft Defender, ASR, AppLocker, WDAC o el visor de eventos.");
            }
        }
        catch (ArgumentException)
        {
            Reset();

            throw new RemoteHostLaunchException(
                "REMOTE_HOST_BLOCKED_OR_TERMINATED",
                "TitanMDM RemoteHost no permaneció en ejecución. " +
                "El proceso pudo ser bloqueado por Microsoft Defender/ASR " +
                "o terminado durante el inicio.");
        }
        finally
        {
            process?.Dispose();
        }

        _logger.LogInformation(
            "TitanMDM RemoteHost estable después del startup probe. " +
            "RemoteSession={RemoteSessionId}, PID={Pid}, WindowsSession={WindowsSessionId}, LaunchMode={LaunchMode}.",
            request.SessionId,
            launchResult.ProcessId,
            launchResult.WindowsSessionId,
            launchResult.LaunchMode);
    }

    public async Task StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken =
            default)
    {
        int? processId;

        lock (_syncRoot)
        {
            if (
                _remoteSessionId !=
                sessionId)
            {
                return;
            }

            processId =
                _hostProcessId;
        }

        if (!processId.HasValue)
        {
            Reset();

            return;
        }

        try
        {
            Process? process;

            try
            {
                process =
                    Process.GetProcessById(
                        processId.Value);
            }
            catch (ArgumentException)
            {
                process =
                    null;
            }

            if (process is null)
            {
                Reset();

                return;
            }

            using (process)
            {
                if (process.HasExited)
                {
                    Reset();

                    return;
                }

                _logger.LogInformation(
                    "Deteniendo TitanMDM RemoteHost. SessionId={SessionId}, PID={Pid}.",
                    sessionId,
                    processId.Value);

                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                    // RemoteHost puede no disponer de ventana principal.
                }

                using var timeout =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        5));

                try
                {
                    await process
                        .WaitForExitAsync(
                            timeout.Token);
                }
                catch (OperationCanceledException)
                    when (
                        !cancellationToken
                            .IsCancellationRequested)
                {
                    if (!process.HasExited)
                    {
                        _logger.LogWarning(
                            "RemoteHost no finalizó normalmente. Terminando PID={Pid}.",
                            processId.Value);

                        process.Kill(
                            entireProcessTree:
                                true);

                        await process
                            .WaitForExitAsync(
                                cancellationToken);
                    }
                }
            }
        }
        finally
        {
            Reset();
        }
    }

    private string ResolveRemoteHostPath()
    {
        var agentDirectory =
            AppContext.BaseDirectory;

        var productionSibling =
            Path.GetFullPath(
                Path.Combine(
                    agentDirectory,
                    "..",
                    "RemoteHost",
                    "TitanMDM.RemoteHost.exe"));

        var sameDirectory =
            Path.Combine(
                agentDirectory,
                "TitanMDM.RemoteHost.exe");

        var developmentDebug =
            Path.GetFullPath(
                Path.Combine(
                    agentDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "TitanMDM.RemoteHost",
                    "bin",
                    "Debug",
                    "net10.0-windows",
                    "TitanMDM.RemoteHost.exe"));

        var developmentRelease =
            Path.GetFullPath(
                Path.Combine(
                    agentDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "TitanMDM.RemoteHost",
                    "bin",
                    "Release",
                    "net10.0-windows",
                    "TitanMDM.RemoteHost.exe"));

        var candidates =
            new[]
            {
                productionSibling,
                sameDirectory,
                developmentDebug,
                developmentRelease
            };

        var executablePath =
            candidates.FirstOrDefault(
                File.Exists);

        if (executablePath is not null)
        {
            return executablePath;
        }

        throw new FileNotFoundException(
            "TitanMDM no encontró TitanMDM.RemoteHost.exe. " +
            "El componente RemoteHost debe instalarse junto al Windows Agent.",
            productionSibling);
    }

    private static void ValidateRemoteHostPath(
        string executablePath)
    {
        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new RemoteHostLaunchException(
                "REMOTE_HOST_PATH_EMPTY",
                "La ruta de RemoteHost está vacía.");
        }

        var fullPath =
            Path.GetFullPath(
                executablePath);

        if (!File.Exists(
                fullPath))
        {
            throw new RemoteHostLaunchException(
                "REMOTE_HOST_NOT_FOUND",
                $"RemoteHost no existe: {fullPath}");
        }

        if (
            !string.Equals(
                Path.GetFileName(
                    fullPath),
                "TitanMDM.RemoteHost.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RemoteHostLaunchException(
                "REMOTE_HOST_INVALID_EXECUTABLE",
                "El ejecutable solicitado no corresponde a TitanMDM.RemoteHost.exe.");
        }

        var length =
            new FileInfo(
                fullPath)
                .Length;

        if (length <= 0)
        {
            throw new RemoteHostLaunchException(
                "REMOTE_HOST_EMPTY_FILE",
                "TitanMDM.RemoteHost.exe está vacío o corrupto.");
        }
    }

    private static string ComputeSha256(
        string executablePath)
    {
        using var stream =
            File.OpenRead(
                executablePath);

        var hash =
            SHA256.HashData(
                stream);

        return Convert.ToHexString(
            hash);
    }

    private bool IsProcessRunningUnsafe()
    {
        if (!_hostProcessId.HasValue)
        {
            return false;
        }

        try
        {
            using var process =
                Process.GetProcessById(
                    _hostProcessId.Value);

            return
                !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupExitedProcessUnsafe()
    {
        if (!_hostProcessId.HasValue)
        {
            return;
        }

        if (IsProcessRunningUnsafe())
        {
            return;
        }

        _hostProcessId =
            null;

        _windowsSessionId =
            null;

        _remoteSessionId =
            null;
    }

    private void Reset()
    {
        lock (_syncRoot)
        {
            _hostProcessId =
                null;

            _windowsSessionId =
                null;

            _remoteSessionId =
                null;
        }
    }
}

public sealed record RemoteHostStatus(
    bool IsRunning,
    int? ProcessId,
    int? WindowsSessionId,
    Guid? RemoteSessionId,
    string? ExecutablePath,
    string? ExecutableSha256,
    string? LaunchMode,
    DateTime? StartedAtUtc);

public sealed class RemoteHostLaunchException
    : Exception
{
    public RemoteHostLaunchException(
        string code,
        string message)
        : base(message)
    {
        Code =
            code;
    }

    public string Code
    {
        get;
    }
}