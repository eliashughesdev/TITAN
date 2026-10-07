using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class RemoteSupportBackgroundService
    : BackgroundService
{
    private const int MaxRestartAttempts =
        3;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(
            5);

    private static readonly TimeSpan IdentityWaitInterval =
        TimeSpan.FromSeconds(
            15);

    private static readonly TimeSpan RestartInterval =
        TimeSpan.FromSeconds(
            10);

    private readonly RemoteSupportApiClient
        _apiClient;

    private readonly RemoteSupportSessionManager
        _sessionManager;

    private readonly RemoteDesktopHostLauncher
        _hostLauncher;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly AgentLifecycleCoordinator
        _lifecycle;

    private readonly ILogger<
        RemoteSupportBackgroundService>
        _logger;

    private RemoteDesktopStartRequest?
        _activeLaunchRequest;

    private int
        _restartAttempts;

    private DateTime
        _nextRestartAtUtc;

    private bool
        _identityWaitingLogged;

    public RemoteSupportBackgroundService(
        RemoteSupportApiClient apiClient,
        RemoteSupportSessionManager sessionManager,
        RemoteDesktopHostLauncher hostLauncher,
        DeviceIdentityStore identityStore,
        AgentLifecycleCoordinator lifecycle,
        ILogger<RemoteSupportBackgroundService> logger)
    {
        _apiClient =
            apiClient;

        _sessionManager =
            sessionManager;

        _hostLauncher =
            hostLauncher;

        _identityStore =
            identityStore;

        _lifecycle =
            lifecycle;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TitanMDM Remote Support service iniciado.");

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            try
            {
                var identity =
                    await _identityStore
                        .LoadAsync(
                            stoppingToken);

                /*
                 * =================================================
                 * IDENTITY GATE
                 * =================================================
                 *
                 * Remote Support jamás consulta el backend antes
                 * de que el agente tenga identidad persistente.
                 * =================================================
                 */

                if (identity is null)
                {
                    if (!_identityWaitingLogged)
                    {
                        _logger.LogInformation(
                            "Remote Support en espera: el agente aún no posee identidad TitanMDM.");

                        _identityWaitingLogged =
                            true;
                    }

                    await DelayAsync(
                        IdentityWaitInterval,
                        stoppingToken);

                    continue;
                }

                _identityWaitingLogged =
                    false;

                await PollAsync(
                    stoppingToken);

                await DelayAsync(
                    PollInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    "Remote Support no pudo comunicarse con TitanMDM: {Message}",
                    ex.Message);

                await DelayAsync(
                    TimeSpan.FromSeconds(
                        15),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Remote Support polling failed.");

                await DelayAsync(
                    TimeSpan.FromSeconds(
                        15),
                    stoppingToken);
            }
        }

        var current =
            _sessionManager
                .CurrentSession;

        if (current is not null)
        {
            try
            {
                await StopCurrentSessionAsync(
                    current.SessionId,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RemoteHost cleanup falló al detener el servicio.");
            }
        }

        _logger.LogInformation(
            "TitanMDM Remote Support service detenido.");
    }

    private async Task PollAsync(
        CancellationToken cancellationToken)
    {
        var pending =
            await _apiClient
                .GetPendingAsync(
                    cancellationToken);

        var current =
            _sessionManager
                .CurrentSession;

        if (current is not null)
        {
            var serverSession =
                pending.FirstOrDefault(
                    x =>
                        x.SessionId ==
                        current.SessionId);

            if (
                serverSession is null
                ||
                serverSession.ExpiresAtUtc <=
                    DateTime.UtcNow)
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

        var request =
            pending
                .Where(
                    x =>
                        x.ExpiresAtUtc >
                        DateTime.UtcNow)
                .OrderBy(
                    x =>
                        x.RequestedAtUtc)
                .FirstOrDefault();

        if (request is null)
        {
            return;
        }

        await StartSessionAsync(
            request,
            cancellationToken);
    }

    private async Task StartSessionAsync(
        RemoteSupportRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !_sessionManager
                .TryBegin(
                    request,
                    out _))
        {
            return;
        }

        try
        {
            await _apiClient
                .MarkConnectingAsync(
                    request.SessionId,
                    cancellationToken);

            var bootstrap =
                await _apiClient
                    .CreateHostBootstrapAsync(
                        request.SessionId,
                        cancellationToken);

            var launchRequest =
                new RemoteDesktopStartRequest(
                    SessionId:
                        request.SessionId,

                    TechnicianName:
                        request
                            .TechnicianDisplayName,

                    Reason:
                        request.Reason,

                    AllowKeyboard:
                        request.AllowKeyboard,

                    AllowMouse:
                        request.AllowMouse,

                    AllowClipboard:
                        request.AllowClipboard,

                    AllowFileTransfer:
                        request.AllowFileTransfer,

                    ExpiresAtUtc:
                        request.ExpiresAtUtc,

                    ServerUrl:
                        bootstrap.ServerUrl,

                    AccessToken:
                        bootstrap.AccessToken);

            await _hostLauncher
                .StartAsync(
                    launchRequest,
                    cancellationToken);

            _activeLaunchRequest =
                launchRequest;

            _restartAttempts =
                0;

            _nextRestartAtUtc =
                DateTime.MinValue;

            _logger.LogInformation(
                "RemoteHost iniciado para RemoteSession={SessionId}. " +
                "Esperando registro SignalR.",
                request.SessionId);
        }
        catch (RemoteHostLaunchException ex)
        {
            _logger.LogError(
                "RemoteHost no pudo iniciar. Code={Code}, Message={Message}",
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

    private async Task EnsureHostRunningAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var activeWindowsSessionId =
            _hostLauncher
                .ActiveConsoleSessionId;

        if (!activeWindowsSessionId.HasValue)
        {
            return;
        }

        var runningWindowsSessionId =
            _hostLauncher
                .RunningWindowsSessionId;

        if (
            _hostLauncher.IsRunning
            &&
            runningWindowsSessionId !=
                activeWindowsSessionId)
        {
            _logger.LogInformation(
                "Windows session cambió de {PreviousSessionId} a {CurrentSessionId}. " +
                "Reiniciando RemoteHost RemoteSession={RemoteSessionId}.",
                runningWindowsSessionId,
                activeWindowsSessionId,
                sessionId);

            await _hostLauncher
                .StopAsync(
                    sessionId,
                    cancellationToken);

            _restartAttempts =
                0;

            _nextRestartAtUtc =
                DateTime.MinValue;
        }

        if (_hostLauncher.IsRunning)
        {
            _restartAttempts =
                0;

            _nextRestartAtUtc =
                DateTime.MinValue;

            return;
        }

        if (
            _activeLaunchRequest is null
            ||
            _activeLaunchRequest.SessionId !=
                sessionId)
        {
            await FailSessionAsync(
                sessionId,
                new InvalidOperationException(
                    "RemoteHost terminó y no existe configuración válida para reiniciarlo."),
                cancellationToken);

            return;
        }

        if (
            _activeLaunchRequest.ExpiresAtUtc <=
                DateTime.UtcNow)
        {
            await StopCurrentSessionAsync(
                sessionId,
                cancellationToken);

            return;
        }

        if (
            DateTime.UtcNow <
            _nextRestartAtUtc)
        {
            return;
        }

        if (
            _restartAttempts >=
            MaxRestartAttempts)
        {
            await FailSessionAsync(
                sessionId,
                new InvalidOperationException(
                    "RemoteHost no pudo recuperarse después de tres intentos."),
                cancellationToken);

            return;
        }

        _restartAttempts++;

        _nextRestartAtUtc =
            DateTime.UtcNow
                .Add(
                    RestartInterval);

        try
        {
            _logger.LogWarning(
                "RemoteHost terminó inesperadamente. " +
                "RemoteSession={SessionId}, intento {Attempt}/{Maximum}.",
                sessionId,
                _restartAttempts,
                MaxRestartAttempts);

            await _hostLauncher
                .StartAsync(
                    _activeLaunchRequest,
                    cancellationToken);
        }
        catch (RemoteHostLaunchException ex)
        {
            _logger.LogWarning(
                "Reinicio RemoteHost falló. Code={Code}, Message={Message}",
                ex.Code,
                ex.Message);

            if (
                ex.Code ==
                "REMOTE_HOST_BLOCKED_OR_TERMINATED"
                ||
                ex.Code ==
                "REMOTE_HOST_EXITED_IMMEDIATELY")
            {
                await FailSessionAsync(
                    sessionId,
                    ex,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Reinicio RemoteHost falló para RemoteSession={SessionId}.",
                sessionId);
        }
    }

    private async Task FailSessionAsync(
        Guid sessionId,
        Exception error,
        CancellationToken cancellationToken)
    {
        try
        {
            await _apiClient
                .MarkFailedAsync(
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

        try
        {
            await StopCurrentSessionAsync(
                sessionId,
                CancellationToken.None);
        }
        catch (Exception stopError)
        {
            _logger.LogWarning(
                stopError,
                "No fue posible limpiar RemoteSession={SessionId}.",
                sessionId);
        }
    }

    private async Task StopCurrentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await _hostLauncher
            .StopAsync(
                sessionId,
                cancellationToken);

        _sessionManager
            .End(
                sessionId);

        _activeLaunchRequest =
            null;

        _restartAttempts =
            0;

        _nextRestartAtUtc =
            DateTime.MinValue;
    }

    private static async Task DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                delay,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
            // apagado normal
        }
    }
}