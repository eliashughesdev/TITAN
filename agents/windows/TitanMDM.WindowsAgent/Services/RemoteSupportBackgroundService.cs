
using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class RemoteSupportBackgroundService
    : BackgroundService
{
    private const int MaxRestartAttempts = 3;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan IdentityWaitInterval =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan BackendRetryInterval =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RestartInterval =
        TimeSpan.FromSeconds(10);

    private readonly RemoteSupportApiClient _apiClient;
    private readonly RemoteSupportSessionManager _sessionManager;
    private readonly RemoteDesktopHostLauncher _hostLauncher;
    private readonly DeviceIdentityStore _identityStore;
    private readonly AgentLifecycleCoordinator _lifecycle;
    private readonly ILogger<RemoteSupportBackgroundService> _logger;

    private RemoteDesktopStartRequest? _activeLaunchRequest;

    private int _restartAttempts;
    private DateTime _nextRestartAtUtc = DateTime.MinValue;
    private bool _identityWaitingLogged;

    public RemoteSupportBackgroundService(
        RemoteSupportApiClient apiClient,
        RemoteSupportSessionManager sessionManager,
        RemoteDesktopHostLauncher hostLauncher,
        DeviceIdentityStore identityStore,
        AgentLifecycleCoordinator lifecycle,
        ILogger<RemoteSupportBackgroundService> logger)
    {
        _apiClient = apiClient;
        _sessionManager = sessionManager;
        _hostLauncher = hostLauncher;
        _identityStore = identityStore;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    // ============================================================
    // MAIN SUPERVISOR
    // ============================================================

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TitanMDM Remote Support supervisor iniciado.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // La expiración debe comprobarse incluso
                    // cuando el backend está desconectado.
                    await EnforceLocalSessionExpiryAsync(
                        stoppingToken);

                    var identity = await _identityStore.LoadAsync(
                        stoppingToken);

                    if (identity is null)
                    {
                        if (!_identityWaitingLogged)
                        {
                            _logger.LogInformation(
                                "Remote Support espera la identidad persistente del agente.");

                            _identityWaitingLogged = true;
                        }

                        await StopSessionIfIdentityUnavailableAsync(
                            stoppingToken);

                        await DelayAsync(
                            IdentityWaitInterval,
                            stoppingToken);

                        continue;
                    }

                    _identityWaitingLogged = false;

                    await PollAsync(stoppingToken);

                    await DelayAsync(
                        PollInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(
                        "Remote Support: comunicación HTTP no disponible. Error={Message}",
                        ex.Message);

                    await DelayAsync(
                        BackendRetryInterval,
                        stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Remote Support supervisor detectó una excepción recuperable.");

                    await DelayAsync(
                        BackendRetryInterval,
                        stoppingToken);
                }
            }
        }
        finally
        {
            await CleanupOnShutdownAsync();
        }
    }

    // ============================================================
    // LOCAL SESSION EXPIRY
    // ============================================================

    private async Task EnforceLocalSessionExpiryAsync(
        CancellationToken cancellationToken)
    {
        var current = _sessionManager.CurrentSession;

        if (current is null)
        {
            return;
        }

        var launchRequest = _activeLaunchRequest;

        if (launchRequest is null)
        {
            // Una sesión aceptada puede encontrarse todavía
            // esperando el bootstrap. No la terminamos aquí.
            return;
        }

        if (launchRequest.SessionId != current.SessionId)
        {
            _logger.LogError(
                "Inconsistencia de sesión remota. Current={CurrentSessionId}, Bootstrap={BootstrapSessionId}.",
                current.SessionId,
                launchRequest.SessionId);

            await StopCurrentSessionAsync(
                current.SessionId,
                cancellationToken);

            return;
        }

        if (launchRequest.ExpiresAtUtc > DateTime.UtcNow)
        {
            return;
        }

        _logger.LogInformation(
            "Expiración local RemoteSession={SessionId}. Deteniendo RemoteHost.",
            current.SessionId);

        await StopCurrentSessionAsync(
            current.SessionId,
            cancellationToken);
    }

    private async Task StopSessionIfIdentityUnavailableAsync(
        CancellationToken cancellationToken)
    {
        var current = _sessionManager.CurrentSession;

        if (current is null)
        {
            return;
        }

        _logger.LogWarning(
            "La identidad del agente no está disponible. Cerrando RemoteSession={SessionId}.",
            current.SessionId);

        await StopCurrentSessionAsync(
            current.SessionId,
            cancellationToken);
    }

    // ============================================================
    // BACKEND POLLING
    // ============================================================

    private async Task PollAsync(
        CancellationToken cancellationToken)
    {
        var pending = await _apiClient.GetPendingAsync(
            cancellationToken);

        var current = _sessionManager.CurrentSession;

        if (current is not null)
        {
            var serverSession = pending.FirstOrDefault(
                item => item.SessionId == current.SessionId);

            if (serverSession is null)
            {
                _logger.LogInformation(
                    "RemoteSession={SessionId} ya no está activa en el backend.",
                    current.SessionId);

                await StopCurrentSessionAsync(
                    current.SessionId,
                    cancellationToken);

                return;
            }

            if (serverSession.ExpiresAtUtc <= DateTime.UtcNow)
            {
                await StopCurrentSessionAsync(
                    current.SessionId,
                    cancellationToken);

                return;
            }

            await EnsureHostRunningAsync(
                current.SessionId,
                cancellationToken);

            return;
        }

        var request = pending
            .Where(item =>
                item.ExpiresAtUtc > DateTime.UtcNow)
            .OrderBy(item => item.RequestedAtUtc)
            .FirstOrDefault();

        if (request is null)
        {
            return;
        }

        await StartSessionAsync(
            request,
            cancellationToken);
    }

    // ============================================================
    // START SESSION
    // ============================================================

    private async Task StartSessionAsync(
        RemoteSupportRequest request,
        CancellationToken cancellationToken)
    {
        if (!_sessionManager.TryBegin(
                request,
                out _))
        {
            return;
        }

        try
        {
            await _apiClient.MarkConnectingAsync(
                request.SessionId,
                cancellationToken);

            var bootstrap =
                await _apiClient.CreateHostBootstrapAsync(
                    request.SessionId,
                    cancellationToken);

            var launchRequest = new RemoteDesktopStartRequest(
                SessionId: request.SessionId,
                TechnicianName: request.TechnicianDisplayName,
                Reason: request.Reason,
                AllowKeyboard: request.AllowKeyboard,
                AllowMouse: request.AllowMouse,
                AllowClipboard: request.AllowClipboard,
                AllowFileTransfer: request.AllowFileTransfer,
                ExpiresAtUtc: request.ExpiresAtUtc,
                ServerUrl: bootstrap.ServerUrl,
                AccessToken: bootstrap.AccessToken);

            if (launchRequest.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    "La sesión expiró antes de iniciar RemoteHost.");
            }

            await _hostLauncher.StartAsync(
                launchRequest,
                cancellationToken);

            _activeLaunchRequest = launchRequest;
            _restartAttempts = 0;
            _nextRestartAtUtc = DateTime.MinValue;

            _logger.LogInformation(
                "RemoteHost iniciado. RemoteSession={SessionId}. Esperando SignalR.",
                request.SessionId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await CleanupSessionBestEffortAsync(
                request.SessionId);

            throw;
        }
        catch (RemoteHostLaunchException ex)
        {
            _logger.LogError(
                "RemoteHost launch failed. Code={Code}, Message={Message}",
                ex.Code,
                ex.Message);

            await FailSessionAsync(
                request.SessionId,
                ex,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await FailSessionAsync(
                request.SessionId,
                ex,
                cancellationToken);
        }
    }

    // ============================================================
    // HOST HEALTH AND RESTART
    // ============================================================

    private async Task EnsureHostRunningAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var activeWindowsSessionId =
            _hostLauncher.ActiveConsoleSessionId;

        if (!activeWindowsSessionId.HasValue)
        {
            // No hay sesión interactiva activa.
            // No intentamos lanzar el host en Session 0.
            return;
        }

        var runningWindowsSessionId =
            _hostLauncher.RunningWindowsSessionId;

        if (_hostLauncher.IsRunning &&
            runningWindowsSessionId != activeWindowsSessionId)
        {
            _logger.LogInformation(
                "Cambio de sesión Windows: {OldSession} -> {NewSession}. RemoteSession={RemoteSessionId}.",
                runningWindowsSessionId,
                activeWindowsSessionId,
                sessionId);

            await _hostLauncher.StopAsync(
                sessionId,
                cancellationToken);

            _restartAttempts = 0;
            _nextRestartAtUtc = DateTime.MinValue;
        }

        if (_hostLauncher.IsRunning)
        {
            _restartAttempts = 0;
            _nextRestartAtUtc = DateTime.MinValue;
            return;
        }

        var launchRequest = _activeLaunchRequest;

        if (launchRequest is null ||
            launchRequest.SessionId != sessionId)
        {
            await FailSessionAsync(
                sessionId,
                new InvalidOperationException(
                    "RemoteHost terminó sin configuración de recuperación válida."),
                cancellationToken);

            return;
        }

        if (launchRequest.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await StopCurrentSessionAsync(
                sessionId,
                cancellationToken);

            return;
        }

        if (DateTime.UtcNow < _nextRestartAtUtc)
        {
            return;
        }

        if (_restartAttempts >= MaxRestartAttempts)
        {
            await FailSessionAsync(
                sessionId,
                new InvalidOperationException(
                    "RemoteHost agotó sus intentos de recuperación."),
                cancellationToken);

            return;
        }

        _restartAttempts++;

        _nextRestartAtUtc =
            DateTime.UtcNow.Add(RestartInterval);

        try
        {
            _logger.LogWarning(
                "Reiniciando RemoteHost. Session={SessionId}, Attempt={Attempt}/{Max}.",
                sessionId,
                _restartAttempts,
                MaxRestartAttempts);

            await _hostLauncher.StartAsync(
                launchRequest,
                cancellationToken);

            // La misma sesión remota se conserva.
            // No se solicita una nueva RemoteSession.
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RemoteHostLaunchException ex)
        {
            _logger.LogWarning(
                "RemoteHost restart failed. Code={Code}, Message={Message}",
                ex.Code,
                ex.Message);

            if (ex.Code is
                "REMOTE_HOST_BLOCKED_OR_TERMINATED" or
                "REMOTE_HOST_EXITED_IMMEDIATELY")
            {
                await FailSessionAsync(
                    sessionId,
                    ex,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error durante recuperación RemoteSession={SessionId}.",
                sessionId);
        }
    }

    // ============================================================
    // FAILED SESSION
    // ============================================================

    private async Task FailSessionAsync(
        Guid sessionId,
        Exception error,
        CancellationToken cancellationToken)
    {
        try
        {
            await _apiClient.MarkFailedAsync(
                sessionId,
                error.Message,
                cancellationToken);
        }
        catch (Exception reportError)
        {
            _logger.LogWarning(
                reportError,
                "No fue posible reportar fallo RemoteSession={SessionId}.",
                sessionId);
        }

        await CleanupSessionBestEffortAsync(sessionId);
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private async Task CleanupSessionBestEffortAsync(
        Guid sessionId)
    {
        try
        {
            await StopCurrentSessionAsync(
                sessionId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Limpieza falló para RemoteSession={SessionId}.",
                sessionId);
        }
    }

    private async Task StopCurrentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hostLauncher.StopAsync(
                sessionId,
                cancellationToken);
        }
        finally
        {
            _sessionManager.End(sessionId);

            if (_activeLaunchRequest?.SessionId == sessionId)
            {
                // Liberar la referencia al bootstrap y token.
                _activeLaunchRequest = null;
            }

            _restartAttempts = 0;
            _nextRestartAtUtc = DateTime.MinValue;
        }
    }

    private async Task CleanupOnShutdownAsync()
    {
        var current = _sessionManager.CurrentSession;

        if (current is not null)
        {
            await CleanupSessionBestEffortAsync(
                current.SessionId);
        }

        _activeLaunchRequest = null;

        _logger.LogInformation(
            "TitanMDM Remote Support supervisor detenido.");
    }

    private static async Task DelayAsync(
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Cancelación normal del servicio.
        }
    }
}
