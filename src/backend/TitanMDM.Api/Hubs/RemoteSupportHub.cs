using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.RemoteSupport;
using TitanMDM.Api.Services;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Hubs;

[AllowAnonymous]
public sealed class RemoteSupportHub
    : Hub
{
    public const string Route =
        "/hubs/remote-support";

    private const string JoinedSessionsKey =
        "TitanMDM.RemoteSupport.JoinedSessions";

    private readonly TitanMdmDbContext
        _dbContext;

    private readonly RemoteHostTokenService
        _tokenService;

    private readonly RemoteSupportConnectionRegistry
        _connections;

    private readonly RemoteControlLeaseService
        _controlLeases;

    private readonly IScopeAccessService
        _scopeAccessService;

    private readonly ILogger<
        RemoteSupportHub>
        _logger;

    public RemoteSupportHub(
        TitanMdmDbContext dbContext,
        RemoteHostTokenService tokenService,
        RemoteSupportConnectionRegistry connections,
        RemoteControlLeaseService controlLeases,
        IScopeAccessService scopeAccessService,
        ILogger<RemoteSupportHub> logger)
    {
        _dbContext =
            dbContext;

        _tokenService =
            tokenService;

        _connections =
            connections;

        _controlLeases =
            controlLeases;

        _scopeAccessService =
            scopeAccessService;

        _logger =
            logger;
    }

    // ============================================================
    // CONNECTION
    // ============================================================

    public override async Task
        OnConnectedAsync()
    {
        if (
            IsHumanConnection())
        {
            var organizationId =
                GetHumanOrganizationId();

            var userId =
                GetHumanUserId();

            _connections
                .RegisterHuman(
                    Context.ConnectionId,
                    organizationId,
                    userId,
                    GetHumanDisplayName(
                        userId));

            await Groups
                .AddToGroupAsync(
                    Context.ConnectionId,
                    OrganizationGroup(
                        organizationId));

            _logger.LogInformation(
                "TitanMDM Remote Support technician connected. ConnectionId={ConnectionId}, OrganizationId={OrganizationId}, UserId={UserId}.",
                Context.ConnectionId,
                organizationId,
                userId);
        }
        else
        {
            _logger.LogInformation(
                "TitanMDM Remote Support RemoteHost connection established. ConnectionId={ConnectionId}.",
                Context.ConnectionId);
        }

        await base
            .OnConnectedAsync();
    }

    public override async Task
        OnDisconnectedAsync(
            Exception? exception)
    {
        try
        {
            if (
                IsHumanConnection())
            {
                await HandleHumanDisconnectAsync();
            }
            else
            {
                await HandleRemoteHostDisconnectAsync();
            }
        }
        catch (
            Exception cleanupException)
        {
            _logger.LogWarning(
                cleanupException,
                "Remote Support disconnect cleanup failed. ConnectionId={ConnectionId}.",
                Context.ConnectionId);
        }

        _connections
            .Remove(
                Context.ConnectionId);

        if (
            exception is null)
        {
            _logger.LogInformation(
                "TitanMDM Remote Support connection closed. ConnectionId={ConnectionId}.",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "TitanMDM Remote Support connection closed with error. ConnectionId={ConnectionId}.",
                Context.ConnectionId);
        }

        await base
            .OnDisconnectedAsync(
                exception);
    }

    // ============================================================
    // TECHNICIAN SESSION
    // ============================================================

    public async Task JoinSession(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.view");

        var userId =
            GetHumanUserId();

        var displayName =
            GetHumanDisplayName(
                userId);

        var participant =
            await EnsureParticipantAsync(
                session.OrganizationId,
                session.Id,
                userId,
                displayName);

        participant
            .MarkConnected();

        AddJoinedSession(
            session.Id);

        _connections
            .JoinSession(
                Context.ConnectionId,
                new RemoteSessionAccessState(
                    session.OrganizationId,
                    session.Id,
                    session.ExpiresAtUtc,
                    session.AllowMouse,
                    session.AllowKeyboard));

        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    session.OrganizationId,
                    session.Id,
                    "PARTICIPANT_CONNECTED",
                    $"{displayName} se conectó a la sesión remota.",
                    userId));

        await _dbContext
            .SaveChangesAsync();

        await Groups
            .AddToGroupAsync(
                Context.ConnectionId,
                TechnicianGroup(
                    session.OrganizationId,
                    session.Id));

        await Clients
            .Group(
                TechnicianGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "ParticipantStateChanged",
                new
                {
                    sessionId =
                        session.Id,

                    userId,
                    displayName,

                    connected =
                        true
                });

        await BroadcastControlStateAsync(
            session.OrganizationId,
            session.Id);

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "RequestMonitorState");

        _logger.LogInformation(
            "Technician joined remote session. SessionId={SessionId}, UserId={UserId}, ConnectionId={ConnectionId}.",
            session.Id,
            userId,
            Context.ConnectionId);
    }

    public async Task LeaveSession(
        Guid sessionId)
    {
        if (
            !IsHumanConnection()
            ||
            sessionId ==
                Guid.Empty)
        {
            return;
        }

        /*
         * Validamos Scope antes de ejecutar operaciones de salida.
         *
         * Si el scope fue revocado durante la sesión,
         * limpiamos el estado local de todas formas.
         */

        var organizationId =
            GetHumanOrganizationId();

        var userId =
            GetHumanUserId();

        var displayName =
            GetHumanDisplayName(
                userId);

        await MarkParticipantDisconnectedAsync(
            organizationId,
            sessionId,
            userId,
            displayName);

        RemoveJoinedSession(
            sessionId);

        _connections
            .LeaveSession(
                Context.ConnectionId,
                sessionId);

        await Groups
            .RemoveFromGroupAsync(
                Context.ConnectionId,
                TechnicianGroup(
                    organizationId,
                    sessionId));

        await Clients
            .Group(
                TechnicianGroup(
                    organizationId,
                    sessionId))
            .SendAsync(
                "ParticipantStateChanged",
                new
                {
                    sessionId,
                    userId,
                    displayName,

                    connected =
                        false
                });

        await BroadcastControlStateAsync(
            organizationId,
            sessionId);
    }

    // ============================================================
    // CONTROL OWNERSHIP
    // ============================================================

    public async Task<object>
        AcquireControl(
            Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        var userId =
            GetHumanUserId();

        var displayName =
            GetHumanDisplayName(
                userId);

        var lease =
            await _controlLeases
                .AcquireAsync(
                    session.OrganizationId,
                    session.Id,
                    userId,
                    displayName,
                    Context.ConnectionAborted);

        await BroadcastControlStateAsync(
            session.OrganizationId,
            session.Id,
            lease);

        return new
        {
            sessionId =
                session.Id,

            hasController =
                lease.HasController,

            ownedByCurrentUser =
                true,

            userId =
                lease.UserId,

            displayName =
                lease.DisplayName,

            acquiredAtUtc =
                lease.AcquiredAtUtc,

            expiresAtUtc =
                lease.ExpiresAtUtc
        };
    }

    public async Task RenewControl(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        var lease =
            await _controlLeases
                .RenewAsync(
                    session.OrganizationId,
                    session.Id,
                    GetHumanUserId(),
                    Context.ConnectionAborted);

        await BroadcastControlStateAsync(
            session.OrganizationId,
            session.Id,
            lease);
    }

    public async Task ReleaseControl(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        var userId =
            GetHumanUserId();

        await _controlLeases
            .ReleaseAsync(
                session.OrganizationId,
                session.Id,
                userId,
                GetHumanDisplayName(
                    userId),
                Context.ConnectionAborted);

        await BroadcastControlStateAsync(
            session.OrganizationId,
            session.Id);
    }

    public async Task RequestControlState(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.view");

        await BroadcastControlStateAsync(
            session.OrganizationId,
            session.Id);
    }

    // ============================================================
    // MONITORS
    // ============================================================

    public async Task SelectMonitor(
        Guid sessionId,
        int monitorIndex)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        await RequireControlLeaseAsync(
            session);

        if (
            monitorIndex <
                0
            ||
            monitorIndex >
                15)
        {
            throw new HubException(
                "Índice de monitor no válido.");
        }

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "SelectMonitor",
                monitorIndex);
    }

    public async Task PreviousMonitor(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        await RequireControlLeaseAsync(
            session);

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "PreviousMonitor");
    }

    public async Task NextMonitor(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.manage");

        await RequireControlLeaseAsync(
            session);

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "NextMonitor");
    }

    public async Task RequestMonitorState(
        Guid sessionId)
    {
        var session =
            await GetHumanSessionAsync(
                sessionId,
                "remote.view");

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "RequestMonitorState");
    }

    public async Task PublishMonitorState(
        Guid sessionId,
        int selectedMonitorIndex,
        IReadOnlyList<
            RemoteMonitorInfoDto>
            monitors)
    {
        var remoteHost =
            ValidateRemoteHost(
                sessionId);

        if (
            monitors is null
            ||
            monitors.Count ==
                0
            ||
            monitors.Count >
                16)
        {
            throw new HubException(
                "Cantidad de monitores no válida.");
        }

        if (
            selectedMonitorIndex <
                0
            ||
            selectedMonitorIndex >=
                monitors.Count)
        {
            throw new HubException(
                "Monitor seleccionado no válido.");
        }

        await Clients
            .Group(
                TechnicianGroup(
                    remoteHost.OrganizationId,
                    sessionId))
            .SendAsync(
                "RemoteMonitorState",
                new
                {
                    sessionId,
                    selectedMonitorIndex,

                    monitors =
                        monitors.Select(
                            monitor =>
                                new
                                {
                                    index =
                                        monitor.Index,

                                    deviceName =
                                        monitor.DeviceName,

                                    width =
                                        monitor.Width,

                                    height =
                                        monitor.Height,

                                    isPrimary =
                                        monitor.IsPrimary,

                                    label =
                                        monitor.Label
                                })
                });
    }

    // ============================================================
    // REMOTE HOST REGISTRATION
    // ============================================================

    public async Task RegisterRemoteHost(
        Guid sessionId)
    {
        var remoteHost =
            ValidateRemoteHost(
                sessionId);

        var session =
            await _dbContext
                .RemoteSessions
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            sessionId
                        &&
                        item.DeviceId ==
                            remoteHost.DeviceId
                        &&
                        item.OrganizationId ==
                            remoteHost.OrganizationId
                        &&
                        item.Status !=
                            RemoteSessionStatus.Completed
                        &&
                        item.Status !=
                            RemoteSessionStatus.Failed
                        &&
                        item.Status !=
                            RemoteSessionStatus.Expired
                        &&
                        item.Status !=
                            RemoteSessionStatus.Cancelled);

        if (session is null)
        {
            throw new HubException(
                "La sesión remota no está disponible.");
        }

        if (
            session.ExpiresAtUtc <=
            DateTime.UtcNow)
        {
            session
                .Expire();

            await _dbContext
                .SaveChangesAsync();

            throw new HubException(
                "La sesión remota expiró.");
        }

        if (
            session.Status ==
                RemoteSessionStatus.Requested
            ||
            session.Status ==
                RemoteSessionStatus.Connecting)
        {
            session
                .MarkConnected();
        }

        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    session.OrganizationId,
                    session.Id,
                    "REMOTE_HOST_CONNECTED",
                    "TitanMDM Remote Host estableció el canal interactivo."));

        await _dbContext
            .SaveChangesAsync();

        await Groups
            .AddToGroupAsync(
                Context.ConnectionId,
                HostGroup(
                    session.OrganizationId,
                    session.Id));

        _connections
            .RegisterRemoteHost(
                Context.ConnectionId,
                session.OrganizationId,
                session.DeviceId,
                session.Id);

        await Clients
            .Group(
                TechnicianGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "RemoteSessionUpdated",
                new
                {
                    sessionId =
                        session.Id,

                    status =
                        session.Status
                            .ToString(),

                    connectedAtUtc =
                        session.ConnectedAtUtc
                });

        await Clients
            .Group(
                TechnicianGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "RemoteHostStateChanged",
                new
                {
                    sessionId =
                        session.Id,

                    connected =
                        true
                });

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.Id))
            .SendAsync(
                "RequestMonitorState");

        _logger.LogInformation(
            "RemoteHost registered successfully. SessionId={SessionId}, DeviceId={DeviceId}, ConnectionId={ConnectionId}.",
            session.Id,
            remoteHost.DeviceId,
            Context.ConnectionId);
    }

    
    // ============================================================
    // WINDOWS DESKTOP STATE — RS-H3.2
    // ============================================================

    public async Task PublishDesktopState(
        Guid sessionId,
        string kind,
        bool canCapture,
        long transitionSequence,
        DateTime observedAtUtc)
    {
        // Solo un RemoteHost con token de sesión válido
        // puede publicar cambios de escritorio.
        var remoteHost = ValidateRemoteHost(sessionId);

        if (transitionSequence < 0)
        {
            throw new HubException(
                "Secuencia de escritorio inválida.");
        }

        var normalizedKind = kind?.Trim() ?? string.Empty;

        var allowedKinds = new[]
        {
            "Default",
            "Winlogon",
            "ScreenSaver",
            "Other",
            "AccessDenied",
            "Unavailable",
            "Unknown"
        };

        if (!allowedKinds.Contains(
                normalizedKind,
                StringComparer.Ordinal))
        {
            throw new HubException(
                "Estado de escritorio no reconocido.");
        }

        if (canCapture && normalizedKind != "Default")
        {
            throw new HubException(
                "Un escritorio protegido no puede " +
                "declararse capturable por RemoteHost.");
        }

        // El timestamp procede del host, pero no se utilizará
        // como autoridad de seguridad.
        var reportedAtUtc = observedAtUtc.Kind switch
        {
            DateTimeKind.Utc => observedAtUtc,
            DateTimeKind.Local => observedAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(
                observedAtUtc, DateTimeKind.Utc)
        };

        await Clients
            .Group(
                TechnicianGroup(
                    remoteHost.OrganizationId,
                    sessionId))
            .SendAsync(
                "RemoteDesktopStateChanged",
                new
                {
                    sessionId,
                    kind = normalizedKind,
                    canCapture,
                    transitionSequence,
                    observedAtUtc = reportedAtUtc.ToString("O")
                },
                Context.ConnectionAborted);
    }


    // ============================================================
    // VIDEO
    // ============================================================

    public async Task PublishFrame(
        Guid sessionId,
        long sequence,
        int width,
        int height,
        string mimeType,
        byte[] data,
        DateTime capturedAtUtc,
        int displayIndex,
        int displayCount,
        string displayLabel)
    {
        var remoteHost =
            ValidateRemoteHost(
                sessionId);

        if (
            sequence <
            0)
        {
            return;
        }

        if (
            width <=
                0
            ||
            height <=
                0
            ||
            width >
                16384
            ||
            height >
                16384)
        {
            throw new HubException(
                "Dimensiones de frame no válidas.");
        }

        if (
            displayCount <
                1
            ||
            displayCount >
                16)
        {
            throw new HubException(
                "Cantidad de monitores no válida.");
        }

        if (
            displayIndex <
                0
            ||
            displayIndex >=
                displayCount)
        {
            throw new HubException(
                "Índice de monitor no válido.");
        }

        if (
            string.IsNullOrWhiteSpace(
                mimeType))
        {
            throw new HubException(
                "MimeType del frame no válido.");
        }

        if (
            !mimeType.Equals(
                "image/jpeg",
                StringComparison.OrdinalIgnoreCase)
            &&
            !mimeType.Equals(
                "image/png",
                StringComparison.OrdinalIgnoreCase)
            &&
            !mimeType.Equals(
                "image/webp",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException(
                "Formato de frame no permitido.");
        }

        if (
            data is null
            ||
            data.Length ==
                0)
        {
            return;
        }

        if (
            data.Length >
            6_000_000)
        {
            throw new HubException(
                "Frame demasiado grande.");
        }

        await Clients
            .Group(
                TechnicianGroup(
                    remoteHost.OrganizationId,
                    sessionId))
            .SendAsync(
                "RemoteFrame",
                new
                {
                    sessionId,
                    sequence,
                    width,
                    height,
                    mimeType,
                    data,

                    capturedAtUtc =
                        capturedAtUtc
                            .ToString(
                                "O"),

                    displayIndex,
                    displayCount,
                    displayLabel
                });
    }

    // ============================================================
    // MOUSE
    // ============================================================

    public async Task PointerMove(
        Guid sessionId,
        double x,
        double y)
    {
        var session =
            GetJoinedSessionAccess(
                sessionId,
                "remote.manage");

        if (
            !session.AllowMouse)
        {
            return;
        }

        RequireCachedControlLease(
            session);

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.SessionId))
            .SendAsync(
                "PointerMove",
                Math.Clamp(
                    x,
                    0,
                    1),
                Math.Clamp(
                    y,
                    0,
                    1));
    }

    public async Task PointerButton(
        Guid sessionId,
        string action)
    {
        var session =
            GetJoinedSessionAccess(
                sessionId,
                "remote.manage");

        if (
            !session.AllowMouse)
        {
            return;
        }

        RequireCachedControlLease(
            session);

        var allowed =
            action is
                "left-down"
                or "left-up"
                or "right-down"
                or "right-up";

        if (!allowed)
        {
            throw new HubException(
                "Evento de mouse no válido.");
        }

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.SessionId))
            .SendAsync(
                "PointerButton",
                action);
    }

    public async Task PointerWheel(
        Guid sessionId,
        int delta)
    {
        var session =
            GetJoinedSessionAccess(
                sessionId,
                "remote.manage");

        if (
            !session.AllowMouse)
        {
            return;
        }

        RequireCachedControlLease(
            session);

        delta =
            Math.Clamp(
                delta,
                -1200,
                1200);

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.SessionId))
            .SendAsync(
                "PointerWheel",
                delta);
    }

    // ============================================================
    // KEYBOARD
    // ============================================================

    public async Task Keyboard(
        Guid sessionId,
        int virtualKey,
        bool keyDown)
    {
        var session =
            GetJoinedSessionAccess(
                sessionId,
                "remote.manage");

        if (
            !session.AllowKeyboard)
        {
            return;
        }

        RequireCachedControlLease(
            session);

        if (
            virtualKey <
                1
            ||
            virtualKey >
                255)
        {
            return;
        }

        await Clients
            .Group(
                HostGroup(
                    session.OrganizationId,
                    session.SessionId))
            .SendAsync(
                "Keyboard",
                virtualKey,
                keyDown);
    }

    // ============================================================
    // SESSION VALIDATION
    // ============================================================

    private async Task<RemoteSession>
        GetHumanSessionAsync(
            Guid sessionId,
            string permission)
    {
        RequireHumanPermission(
            permission);

        if (
            sessionId ==
            Guid.Empty)
        {
            throw new HubException(
                "SessionId no válido.");
        }

        var organizationId =
            GetHumanOrganizationId();

        var userId =
            GetHumanUserId();

        var session =
            await _dbContext
                .RemoteSessions
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            sessionId
                        &&
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.Status !=
                            RemoteSessionStatus.Completed
                        &&
                        item.Status !=
                            RemoteSessionStatus.Failed
                        &&
                        item.Status !=
                            RemoteSessionStatus.Expired
                        &&
                        item.Status !=
                            RemoteSessionStatus.Cancelled,
                    Context.ConnectionAborted);

        if (session is null)
        {
            throw new HubException(
                "La sesión remota no está disponible.");
        }

        /*
         * ========================================================
         * RBAC SCOPE
         * ========================================================
         *
         * Esta validación protege:
         *
         * JoinSession
         * AcquireControl
         * RenewControl
         * ReleaseControl
         * SelectMonitor
         * PreviousMonitor
         * NextMonitor
         * RequestMonitorState
         *
         * y cualquier futura operación que use este helper.
         * ========================================================
         */

        var canAccess =
            await _scopeAccessService
                .CanAccessDeviceAsync(
                    organizationId,
                    userId,
                    session.DeviceId,
                    Context.ConnectionAborted);

        if (!canAccess)
        {
            _logger.LogWarning(
                "Remote Support SignalR scope denied. UserId={UserId}, SessionId={SessionId}, DeviceId={DeviceId}.",
                userId,
                session.Id,
                session.DeviceId);

            throw new HubException(
                "El dispositivo se encuentra fuera de las localidades autorizadas para el usuario.");
        }

        if (
            session.ExpiresAtUtc <=
            DateTime.UtcNow)
        {
            session
                .Expire();

            await _dbContext
                .SaveChangesAsync(
                    Context.ConnectionAborted);

            throw new HubException(
                "La sesión remota expiró.");
        }

        return session;
    }

    /*
     * Mouse y teclado son eventos de alta frecuencia.
     *
     * No ejecutamos SQL en cada PointerMove.
     *
     * Solamente pueden llegar aquí después de JoinSession,
     * que ya validó Permission + Scope.
     *
     * Además requieren el lease de control.
     */

    private RemoteSessionAccessState
        GetJoinedSessionAccess(
            Guid sessionId,
            string permission)
    {
        RequireHumanPermission(
            permission);

        if (
            !_connections
                .TryGetJoinedSession(
                    Context.ConnectionId,
                    sessionId,
                    out var session))
        {
            throw new HubException(
                "La conexión no está unida a la sesión remota.");
        }

        if (
            session.ExpiresAtUtc <=
            DateTime.UtcNow)
        {
            _connections
                .LeaveSession(
                    Context.ConnectionId,
                    sessionId);

            throw new HubException(
                "La sesión remota expiró.");
        }

        return session;
    }

    // ============================================================
    // PARTICIPANTS
    // ============================================================

    private async Task<RemoteSessionParticipant>
        EnsureParticipantAsync(
            Guid organizationId,
            Guid sessionId,
            Guid userId,
            string displayName)
    {
        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    item =>
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.UserId ==
                            userId,
                    Context.ConnectionAborted);

        if (
            participant is not null)
        {
            return participant;
        }

        participant =
            new RemoteSessionParticipant(
                organizationId,
                sessionId,
                userId,
                displayName,
                canControl:
                    false);

        _dbContext
            .RemoteSessionParticipants
            .Add(
                participant);

        return participant;
    }

    private async Task
        MarkParticipantDisconnectedAsync(
            Guid organizationId,
            Guid sessionId,
            Guid userId,
            string displayName)
    {
        await using var sessionLock =
            await _connections
                .LockSessionAsync(
                    organizationId,
                    sessionId,
                    Context.ConnectionAborted);

        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    item =>
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.UserId ==
                            userId,
                    Context.ConnectionAborted);

        if (
            participant is not null)
        {
            participant
                .MarkDisconnected();
        }

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    item =>
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.UserId ==
                            userId,
                    Context.ConnectionAborted);

        if (
            lease is not null)
        {
            _dbContext
                .RemoteSessionControlLeases
                .Remove(
                    lease);

            participant
                ?.RevokeControl();
        }

        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    organizationId,
                    sessionId,
                    "PARTICIPANT_DISCONNECTED",
                    $"{displayName} salió de la sesión remota.",
                    userId));

        await _dbContext
            .SaveChangesAsync(
                Context.ConnectionAborted);

        if (
            lease is not null)
        {
            _connections
                .RemoveControlLease(
                    organizationId,
                    sessionId);
        }
    }

    // ============================================================
    // CONTROL LEASE
    // ============================================================

    private Task
        RequireControlLeaseAsync(
            RemoteSession session)
    {
        return _controlLeases
            .RequireOwnershipAsync(
                session.OrganizationId,
                session.Id,
                GetHumanUserId(),
                Context.ConnectionAborted);
    }

    private void RequireCachedControlLease(
        RemoteSessionAccessState session)
    {
        if (
            !_connections
                .TryGetControlLease(
                    session.OrganizationId,
                    session.SessionId,
                    out var lease))
        {
            throw new HubException(
                "La sesión está en modo solo lectura. Solicita el control para utilizar teclado o mouse.");
        }

        if (
            lease.UserId !=
            GetHumanUserId())
        {
            throw new HubException(
                $"La sesión está siendo controlada por {lease.DisplayName}.");
        }
    }

    private async Task
        BroadcastControlStateAsync(
            Guid organizationId,
            Guid sessionId)
    {
        var lease =
            await _controlLeases
                .GetStateAsync(
                    organizationId,
                    sessionId,
                    Context.ConnectionAborted);

        await BroadcastControlStateAsync(
            organizationId,
            sessionId,
            lease);
    }

    private async Task
        BroadcastControlStateAsync(
            Guid organizationId,
            Guid sessionId,
            RemoteControlLeaseState lease)
    {
        if (
            !lease.HasController)
        {
            await Clients
                .Group(
                    TechnicianGroup(
                        organizationId,
                        sessionId))
                .SendAsync(
                    "RemoteControlState",
                    new
                    {
                        sessionId,

                        hasController =
                            false
                    });

            return;
        }

        await Clients
            .Group(
                TechnicianGroup(
                    organizationId,
                    sessionId))
            .SendAsync(
                "RemoteControlState",
                new
                {
                    sessionId,

                    hasController =
                        lease.HasController,

                    userId =
                        lease.UserId,

                    displayName =
                        lease.DisplayName,

                    acquiredAtUtc =
                        lease.AcquiredAtUtc,

                    expiresAtUtc =
                        lease.ExpiresAtUtc
                });
    }

    // ============================================================
    // DISCONNECT
    // ============================================================

    private async Task
        HandleHumanDisconnectAsync()
    {
        if (
            !Context.Items
                .TryGetValue(
                    JoinedSessionsKey,
                    out var raw)
            ||
            raw is not HashSet<Guid>
                joinedSessions
            ||
            joinedSessions.Count ==
                0)
        {
            return;
        }

        var organizationId =
            GetHumanOrganizationId();

        var userId =
            GetHumanUserId();

        var displayName =
            GetHumanDisplayName(
                userId);

        foreach (
            var sessionId
            in joinedSessions
                .ToArray())
        {
            await MarkParticipantDisconnectedAsync(
                organizationId,
                sessionId,
                userId,
                displayName);

            await BroadcastControlStateAsync(
                organizationId,
                sessionId);

            await Clients
                .Group(
                    TechnicianGroup(
                        organizationId,
                        sessionId))
                .SendAsync(
                    "ParticipantStateChanged",
                    new
                    {
                        sessionId,
                        userId,
                        displayName,

                        connected =
                            false
                    });
        }

        joinedSessions
            .Clear();
    }

    private async Task
        HandleRemoteHostDisconnectAsync()
    {
        var httpContext =
            Context
                .GetHttpContext();

        if (
            httpContext is null)
        {
            return;
        }

        var sessionHeader =
            httpContext
                .Request
                .Headers[
                    "X-Titan-Remote-Session"]
                .FirstOrDefault();

        if (
            !Guid.TryParse(
                sessionHeader,
                out var sessionId)
            ||
            sessionId ==
                Guid.Empty)
        {
            return;
        }

        var token =
            httpContext
                .Request
                .Headers[
                    "X-Titan-Remote-Token"]
                .FirstOrDefault();

        if (
            !_tokenService
                .TryValidate(
                    token ??
                    string.Empty,
                    sessionId,
                    out var entry))
        {
            return;
        }

        await Clients
            .Group(
                TechnicianGroup(
                    entry.OrganizationId,
                    sessionId))
            .SendAsync(
                "RemoteHostStateChanged",
                new
                {
                    sessionId,

                    connected =
                        false
                });

        _logger.LogWarning(
            "RemoteHost disconnected. SessionId={SessionId}, DeviceId={DeviceId}.",
            sessionId,
            entry.DeviceId);
    }

    // ============================================================
    // JOINED SESSIONS
    // ============================================================

    private void AddJoinedSession(
        Guid sessionId)
    {
        if (
            !Context.Items
                .TryGetValue(
                    JoinedSessionsKey,
                    out var raw)
            ||
            raw is not HashSet<Guid>
                sessions)
        {
            sessions =
                new HashSet<Guid>();

            Context.Items[
                JoinedSessionsKey] =
                sessions;
        }

        sessions.Add(
            sessionId);
    }

    private void RemoveJoinedSession(
        Guid sessionId)
    {
        if (
            Context.Items
                .TryGetValue(
                    JoinedSessionsKey,
                    out var raw)
            &&
            raw is HashSet<Guid>
                sessions)
        {
            sessions.Remove(
                sessionId);
        }
    }

    // ============================================================
    // REMOTE HOST VALIDATION
    // ============================================================

    private RemoteHostTokenEntry
        ValidateRemoteHost(
            Guid sessionId)
    {
        var httpContext =
            Context
                .GetHttpContext()
            ??
            throw new HubException(
                "HTTP context no disponible.");

        var token =
            httpContext
                .Request
                .Headers[
                    "X-Titan-Remote-Token"]
                .FirstOrDefault();

        var sessionHeader =
            httpContext
                .Request
                .Headers[
                    "X-Titan-Remote-Session"]
                .FirstOrDefault();

        if (
            !Guid.TryParse(
                sessionHeader,
                out var headerSessionId)
            ||
            headerSessionId !=
                sessionId)
        {
            throw new HubException(
                "Identidad RemoteHost no válida.");
        }

        if (
            !_tokenService
                .TryValidate(
                    token ??
                    string.Empty,
                    sessionId,
                    out var entry))
        {
            throw new HubException(
                "Token RemoteHost inválido o expirado.");
        }

        return entry;
    }

    // ============================================================
    // HUMAN AUTHORIZATION
    // ============================================================

    private bool IsHumanConnection()
    {
        return
            Context.User
                ?.Identity
                ?.IsAuthenticated ==
            true;
    }

    private void RequireHumanPermission(
        string permission)
    {
        if (
            !IsHumanConnection())
        {
            throw new HubException(
                "Autenticación humana requerida.");
        }

        var allowed =
            Context.User!
                .Claims
                .Any(
                    claim =>
                        claim.Type ==
                            "permission"
                        &&
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new HubException(
                $"El usuario no posee el permiso '{permission}'.");
        }
    }

    private Guid GetHumanOrganizationId()
    {
        var value =
            Context.User?
                .FindFirstValue(
                    "organization_id")
            ??
            Context.User?
                .FindFirstValue(
                    "organizationId");

        if (
            !Guid.TryParse(
                value,
                out var organizationId)
            ||
            organizationId ==
                Guid.Empty)
        {
            throw new HubException(
                "OrganizationId no disponible.");
        }

        return organizationId;
    }

    private Guid GetHumanUserId()
    {
        var value =
            Context.User?
                .FindFirstValue(
                    ClaimTypes.NameIdentifier)
            ??
            Context.User?
                .FindFirstValue(
                    "sub");

        if (
            !Guid.TryParse(
                value,
                out var userId)
            ||
            userId ==
                Guid.Empty)
        {
            throw new HubException(
                "UserId no disponible.");
        }

        return userId;
    }

    private string GetHumanDisplayName(
        Guid userId)
    {
        return
            Context.User?
                .FindFirstValue(
                    ClaimTypes.Name)
            ??
            Context.User?
                .FindFirstValue(
                    ClaimTypes.Email)
            ??
            userId
                .ToString();
    }

    // ============================================================
    // GROUPS
    // ============================================================

    public static string
        OrganizationGroup(
            Guid organizationId)
    {
        return
            $"organization:{organizationId:N}";
    }

    public static string
        TechnicianGroup(
            Guid organizationId,
            Guid sessionId)
    {
        return
            $"organization:{organizationId:N}:remote:{sessionId:N}:technicians";
    }

    public static string
        HostGroup(
            Guid organizationId,
            Guid sessionId)
    {
        return
            $"organization:{organizationId:N}:remote:{sessionId:N}:host";
    }

    public static string
        SessionGroup(
            Guid organizationId,
            Guid sessionId)
    {
        return TechnicianGroup(
            organizationId,
            sessionId);
    }
}

public sealed record RemoteMonitorInfoDto(
    int Index,
    string DeviceName,
    int Width,
    int Height,
    bool IsPrimary,
    string Label);