using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.Services;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device/remote-support")]
[AllowAnonymous]
public sealed class RemoteSupportAgentController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IDeviceAuthenticator
        _deviceAuthenticator;

    private readonly RemoteSupportNotifier
        _notifier;

    private readonly RemoteHostTokenService
        _remoteHostTokenService;

    private readonly ILogger<
        RemoteSupportAgentController> _logger;

    public RemoteSupportAgentController(
        TitanMdmDbContext dbContext,
        IDeviceAuthenticator deviceAuthenticator,
        RemoteSupportNotifier notifier,
        RemoteHostTokenService remoteHostTokenService,
        ILogger<RemoteSupportAgentController> logger)
    {
        _dbContext =
            dbContext;

        _deviceAuthenticator =
            deviceAuthenticator;

        _notifier =
            notifier;

        _remoteHostTokenService =
            remoteHostTokenService;

        _logger =
            logger;
    }

    /*
     * ============================================================
     * PENDING REMOTE SUPPORT SESSIONS
     * ============================================================
     */

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        var now =
            DateTime.UtcNow;

        var sessions =
            await _dbContext
                .RemoteSessions
                .Where(
                    x =>
                        x.DeviceId ==
                            authentication.DeviceId
                        &&
                        (
                            x.Status ==
                                RemoteSessionStatus.Requested
                            ||
                            x.Status ==
                                RemoteSessionStatus.Connecting
                            ||
                            x.Status ==
                                RemoteSessionStatus.Connected
                        ))
                .OrderBy(
                    x =>
                        x.RequestedAtUtc)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var expired in
            sessions.Where(
                x =>
                    x.ExpiresAtUtc <=
                        now
                    &&
                    !x.IsTerminal))
        {
            expired.Expire();

            _dbContext
                .RemoteSessionEvents
                .Add(
                    new RemoteSessionEvent(
                        expired.OrganizationId,
                        expired.Id,
                        "SESSION_EXPIRED",
                        "La sesión remota expiró antes de finalizar."));
        }

        if (
            _dbContext
                .ChangeTracker
                .HasChanges())
        {
            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);
        }

        var active =
            sessions
                .Where(
                    x =>
                        !x.IsTerminal
                        &&
                        x.ExpiresAtUtc >
                            now)
                .Select(
                    x =>
                        new
                        {
                            sessionId =
                                x.Id,

                            deviceId =
                                x.DeviceId,

                            requestedByUserId =
                                x.RequestedByUserId,

                            technicianDisplayName =
                                x.TechnicianName,

                            reason =
                                x.Reason,

                            allowKeyboard =
                                x.AllowKeyboard,

                            allowMouse =
                                x.AllowMouse,

                            allowClipboard =
                                x.AllowClipboard,

                            allowFileTransfer =
                                x.AllowFileTransfer,

                            requestedAtUtc =
                                x.RequestedAtUtc,

                            expiresAtUtc =
                                x.ExpiresAtUtc,

                            status =
                                x.Status.ToString()
                        })
                .ToList();

        return Ok(
            active);
    }

    /*
     * ============================================================
     * REMOTE HOST BOOTSTRAP
     * ============================================================
     *
     * Windows Agent solicita un token temporal para poder iniciar
     * TitanMDM.RemoteHost.exe.
     *
     * RemoteHost NO usa la credencial permanente del dispositivo.
     * Usa un token efímero ligado exclusivamente a:
     *
     * - OrganizationId
     * - DeviceId
     * - SessionId
     *
     * Esto separa correctamente:
     *
     * WindowsAgent credentials
     *          ↓
     * host-bootstrap
     *          ↓
     * temporary RemoteHost token
     *          ↓
     * SignalR RemoteHost
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/host-bootstrap")]
    public async Task<IActionResult>
        CreateHostBootstrap(
            Guid sessionId,
            CancellationToken cancellationToken)
    {
        /*
         * --------------------------------------------------------
         * Authenticate Windows Agent
         * --------------------------------------------------------
         */

        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        if (
            sessionId ==
            Guid.Empty)
        {
            return BadRequest(
                new
                {
                    code =
                        "INVALID_REMOTE_SESSION_ID",

                    message =
                        "SessionId no es válido."
                });
        }

        /*
         * --------------------------------------------------------
         * Load session
         * --------------------------------------------------------
         */

        var session =
            await _dbContext
                .RemoteSessions
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            sessionId
                        &&
                        x.DeviceId ==
                            authentication.DeviceId,
                    cancellationToken);

        if (
            session is null)
        {
            return NotFound(
                new
                {
                    code =
                        "REMOTE_SESSION_NOT_FOUND",

                    message =
                        "La sesión remota no existe para este dispositivo."
                });
        }

        /*
         * --------------------------------------------------------
         * Validate state
         * --------------------------------------------------------
         */

        if (
            session.IsTerminal)
        {
            return Conflict(
                new
                {
                    code =
                        "REMOTE_SESSION_TERMINAL",

                    message =
                        "La sesión remota ya finalizó."
                });
        }

        if (
            session.HasExpired)
        {
            session.Expire();

            _dbContext
                .RemoteSessionEvents
                .Add(
                    new RemoteSessionEvent(
                        session.OrganizationId,
                        session.Id,
                        "SESSION_EXPIRED",
                        "La sesión remota expiró antes de crear el RemoteHost bootstrap."));

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            return Conflict(
                new
                {
                    code =
                        "REMOTE_SESSION_EXPIRED",

                    message =
                        "La sesión remota ha expirado."
                });
        }

        /*
         * --------------------------------------------------------
         * Determine token lifetime
         * --------------------------------------------------------
         *
         * Nunca permitimos que el token RemoteHost sobreviva
         * a la propia sesión.
         */

        var remainingLifetime =
            session.ExpiresAtUtc -
            DateTime.UtcNow;

        if (
            remainingLifetime <=
            TimeSpan.Zero)
        {
            return Conflict(
                new
                {
                    code =
                        "REMOTE_SESSION_EXPIRED",

                    message =
                        "La sesión remota ha expirado."
                });
        }

        /*
         * Límite adicional:
         *
         * un token RemoteHost individual nunca dura más de
         * 30 minutos. Si la sesión dura más, posteriormente
         * podremos implementar renovación automática.
         */
        var tokenLifetime =
            remainingLifetime >
            TimeSpan.FromMinutes(
                30)
                ? TimeSpan.FromMinutes(
                    30)
                : remainingLifetime;

        /*
         * --------------------------------------------------------
         * Create one-time scoped RemoteHost token
         * --------------------------------------------------------
         */

        var accessToken =
            _remoteHostTokenService
                .Create(
                    session.OrganizationId,
                    session.DeviceId,
                    session.Id,
                    tokenLifetime);

        /*
         * --------------------------------------------------------
         * Build server URL
         * --------------------------------------------------------
         *
         * Durante desarrollo:
         *
         *     http://localhost:8020
         *
         * En producción devolverá el host real del API.
         */

        var serverUrl =
            $"{Request.Scheme}://{Request.Host}";

        /*
         * --------------------------------------------------------
         * Audit
         * --------------------------------------------------------
         */

        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    session.OrganizationId,
                    session.Id,
                    "REMOTE_HOST_BOOTSTRAP_CREATED",
                    "TitanMDM generó credenciales temporales para iniciar RemoteHost."));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "RemoteHost bootstrap created. " +
            "SessionId={SessionId}, DeviceId={DeviceId}, OrganizationId={OrganizationId}, ServerUrl={ServerUrl}.",
            session.Id,
            session.DeviceId,
            session.OrganizationId,
            serverUrl);

        /*
         * --------------------------------------------------------
         * Response expected by Windows Agent
         * --------------------------------------------------------
         */

        return Ok(
            new
                RemoteHostBootstrapResponse(
                    ServerUrl:
                        serverUrl,

                    AccessToken:
                        accessToken,

                    ExpiresAtUtc:
                        DateTime.UtcNow.Add(
                            tokenLifetime)));
    }

    /*
     * ============================================================
     * SESSION STATE: CONNECTING
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/connecting")]
    public async Task<IActionResult> Connecting(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ChangeStateAsync(
            sessionId,
            RemoteAgentTransition.Connecting,
            null,
            cancellationToken);
    }

    /*
     * ============================================================
     * SESSION STATE: CONNECTED
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/connected")]
    public async Task<IActionResult> Connected(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ChangeStateAsync(
            sessionId,
            RemoteAgentTransition.Connected,
            null,
            cancellationToken);
    }

    /*
     * ============================================================
     * SESSION STATE: COMPLETED
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/completed")]
    public async Task<IActionResult> Completed(
        Guid sessionId,
        [FromBody]
        RemoteAgentCompletedRequest? request,
        CancellationToken cancellationToken)
    {
        return await ChangeStateAsync(
            sessionId,
            RemoteAgentTransition.Completed,
            request?.Reason,
            cancellationToken);
    }

    /*
     * ============================================================
     * SESSION STATE: FAILED
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/failed")]
    public async Task<IActionResult> Failed(
        Guid sessionId,
        [FromBody]
        RemoteAgentFailedRequest request,
        CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.Reason))
        {
            return BadRequest(
                new
                {
                    code =
                        "INVALID_FAILURE_REASON",

                    message =
                        "Reason es obligatorio."
                });
        }

        return await ChangeStateAsync(
            sessionId,
            RemoteAgentTransition.Failed,
            request.Reason,
            cancellationToken);
    }

    /*
     * ============================================================
     * CHANGE SESSION STATE
     * ============================================================
     */

    private async Task<IActionResult>
        ChangeStateAsync(
            Guid sessionId,
            RemoteAgentTransition transition,
            string? reason,
            CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        var session =
            await _dbContext
                .RemoteSessions
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            sessionId
                        &&
                        x.DeviceId ==
                            authentication.DeviceId,
                    cancellationToken);

        if (
            session is null)
        {
            return NotFound(
                new
                {
                    code =
                        "REMOTE_SESSION_NOT_FOUND",

                    message =
                        "La sesión remota no existe para este dispositivo."
                });
        }

        if (
            session.IsTerminal)
        {
            return Conflict(
                new
                {
                    code =
                        "REMOTE_SESSION_TERMINAL",

                    message =
                        "La sesión remota ya finalizó."
                });
        }

        string eventType;
        string description;

        try
        {
            switch (
                transition)
            {
                case RemoteAgentTransition.Connecting:

                    session.MarkConnecting();

                    eventType =
                        "AGENT_CONNECTING";

                    description =
                        "El agente Windows inició la preparación de la sesión remota.";

                    break;

                case RemoteAgentTransition.Connected:

                    session.MarkConnected();

                    eventType =
                        "AGENT_CONNECTED";

                    description =
                        "El agente Windows confirmó la sesión remota activa.";

                    break;

                case RemoteAgentTransition.Completed:

                    session.Complete(
                        "WindowsAgent",
                        reason
                        ??
                        "La sesión finalizó desde el agente Windows.");

                    eventType =
                        "AGENT_COMPLETED";

                    description =
                        "El agente Windows confirmó la finalización de la sesión.";

                    break;

                case RemoteAgentTransition.Failed:

                    session.Fail(
                        reason
                        ??
                        "Remote support failure.");

                    eventType =
                        "AGENT_FAILED";

                    description =
                        reason
                        ??
                        "La sesión remota falló en el agente Windows.";

                    break;

                default:

                    return BadRequest();
            }
        }
        catch (
            InvalidOperationException ex)
        {
            return Conflict(
                new
                {
                    code =
                        "INVALID_REMOTE_SESSION_STATE",

                    message =
                        ex.Message
                });
        }

        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    session.OrganizationId,
                    session.Id,
                    eventType,
                    description));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        var payload =
            new
            {
                sessionId =
                    session.Id,

                deviceId =
                    session.DeviceId,

                status =
                    session.Status.ToString(),

                session.ConnectedAtUtc,

                session.DisconnectedAtUtc,

                updatedAtUtc =
                    session.UpdatedAtUtc
            };

        await _notifier
            .PublishAsync(
                session.OrganizationId,
                session.Id,
                payload,
                cancellationToken);

        _logger.LogInformation(
            "Remote session {SessionId} transitioned to {Status}.",
            session.Id,
            session.Status);

        return Ok(
            payload);
    }

    /*
     * ============================================================
     * DEVICE AUTHENTICATION
     * ============================================================
     */

    private async Task<DeviceAuthenticationResult>
        AuthenticateDeviceAsync(
            CancellationToken cancellationToken)
    {
        var deviceIdHeader =
            Request.Headers[
                    "X-Titan-Device-Id"]
                .FirstOrDefault();

        var deviceSecret =
            Request.Headers[
                    "X-Titan-Device-Secret"]
                .FirstOrDefault();

        if (
            !Guid.TryParse(
                deviceIdHeader,
                out var deviceId))
        {
            return DeviceAuthenticationResult
                .Fail(
                    Unauthorized(
                        new
                        {
                            code =
                                "INVALID_DEVICE_ID",

                            message =
                                "X-Titan-Device-Id no es válido."
                        }));
        }

        try
        {
            await _deviceAuthenticator
                .AuthenticateAsync(
                    deviceId,
                    deviceSecret
                    ??
                    string.Empty,
                    cancellationToken);

            return DeviceAuthenticationResult
                .Ok(
                    deviceId);
        }
        catch (
            DeviceAuthenticationException ex)
        {
            return DeviceAuthenticationResult
                .Fail(
                    Unauthorized(
                        new
                        {
                            code =
                                ex.Code,

                            message =
                                ex.Message
                        }));
        }
    }

    /*
     * ============================================================
     * INTERNAL TYPES
     * ============================================================
     */

    private enum RemoteAgentTransition
    {
        Connecting,
        Connected,
        Completed,
        Failed
    }

    private sealed record DeviceAuthenticationResult(
        bool Success,
        Guid DeviceId,
        IActionResult? Error)
    {
        public static DeviceAuthenticationResult Ok(
            Guid deviceId)
        {
            return new(
                true,
                deviceId,
                null);
        }

        public static DeviceAuthenticationResult Fail(
            IActionResult error)
        {
            return new(
                false,
                Guid.Empty,
                error);
        }
    }
}

/*
 * ================================================================
 * API CONTRACTS
 * ================================================================
 */

public sealed record RemoteHostBootstrapResponse(
    string ServerUrl,
    string AccessToken,
    DateTime ExpiresAtUtc);

public sealed record RemoteAgentCompletedRequest(
    string? Reason);

public sealed record RemoteAgentFailedRequest(
    string Reason);