using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.RemoteSupport;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Api.Hubs;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/remote-sessions")]
[Authorize]
public sealed class RemoteSessionsController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _dbContext;
    
    private readonly IHubContext<RemoteSupportHub>
    _remoteHub;

    private readonly RemoteControlLeaseService
        _controlLeases;

    private readonly RemoteSupportConnectionRegistry
        _connections;

    private readonly IScopeAccessService
        _scopeAccessService;

    private readonly ILogger<
        RemoteSessionsController>
        _logger;

    public RemoteSessionsController(
    TitanMdmDbContext dbContext,
    RemoteControlLeaseService controlLeases,
    RemoteSupportConnectionRegistry connections,
    IScopeAccessService scopeAccessService,
    IHubContext<RemoteSupportHub> remoteHub,
    ILogger<RemoteSessionsController> logger)
{
    _dbContext = dbContext;
    _controlLeases = controlLeases;
    _connections = connections;
    _scopeAccessService = scopeAccessService;
    _remoteHub = remoteHub;
    _logger = logger;
}

    // ============================================================
    // LIST
    // ============================================================

    [HttpGet]
    public async Task<ActionResult>
        GetSessions(
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.view"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        take =
            Math.Clamp(
                take,
                1,
                200);

        var scope =
            await _scopeAccessService
                .GetScopeSnapshotAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken);

        var query =
            _dbContext
                .RemoteSessions
                .AsNoTracking()
                .Where(
                    session =>
                        session.OrganizationId ==
                            context.Value.OrganizationId);

        /*
         * Organization scope:
         * ve todas las sesiones.
         *
         * Site scope:
         * solamente sesiones cuyos Devices pertenecen
         * a localidades autorizadas.
         *
         * Sin scope:
         * consulta válida pero vacía.
         */

        if (!scope.OrganizationWide)
        {
            var siteIds =
                scope
                    .SiteIds
                    .Where(
                        id =>
                            id != Guid.Empty)
                    .Distinct()
                    .ToArray();

            if (
                siteIds.Length ==
                0)
            {
                query =
                    query.Where(
                        _ =>
                            false);
            }
            else
            {
                query =
                    query.Where(
                        session =>
                            _dbContext
                                .Devices
                                .Any(
                                    device =>
                                        device.Id ==
                                            session.DeviceId
                                        &&
                                        device.OrganizationId ==
                                            context.Value.OrganizationId
                                        &&
                                        !device.IsDeleted
                                        &&
                                        device.SiteId.HasValue
                                        &&
                                        siteIds.Contains(
                                            device.SiteId.Value)));
            }
        }

        var sessions =
            await query
                .OrderByDescending(
                    session =>
                        session.RequestedAtUtc)
                .Take(
                    take)
                .Select(
                    session =>
                        new
                        {
                            session.Id,
                            session.DeviceId,
                            session.RequestedByUserId,
                            session.TechnicianName,
                            session.Reason,

                            status =
                                session.Status
                                    .ToString(),

                            session.AllowKeyboard,
                            session.AllowMouse,
                            session.AllowClipboard,
                            session.AllowFileTransfer,

                            session.RequestedAtUtc,
                            session.ExpiresAtUtc,
                            session.ConnectedAtUtc,
                            session.DisconnectedAtUtc,

                            session.FailureReason,
                            session.TerminationReason,
                            session.TerminatedBy,

                            participantCount =
                                _dbContext
                                    .RemoteSessionParticipants
                                    .Count(
                                        participant =>
                                            participant.RemoteSessionId ==
                                                session.Id
                                            &&
                                            participant.OrganizationId ==
                                                context.Value.OrganizationId),

                            connectedParticipants =
                                _dbContext
                                    .RemoteSessionParticipants
                                    .Count(
                                        participant =>
                                            participant.RemoteSessionId ==
                                                session.Id
                                            &&
                                            participant.OrganizationId ==
                                                context.Value.OrganizationId
                                            &&
                                            participant.IsConnected)
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            sessions);
    }

    // ============================================================
    // DETAIL
    // ============================================================

    [HttpGet("{sessionId:guid}")]
    public async Task<ActionResult>
        GetSession(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.view"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var session =
            await _dbContext
                .RemoteSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            sessionId
                        &&
                        item.OrganizationId ==
                            context.Value.OrganizationId,
                    cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        var events =
            await _dbContext
                .RemoteSessionEvents
                .AsNoTracking()
                .Where(
                    item =>
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.OrganizationId ==
                            context.Value.OrganizationId)
                .OrderBy(
                    item =>
                        item.OccurredAtUtc)
                .Select(
                    item =>
                        new
                        {
                            item.Id,
                            item.EventType,
                            item.Description,
                            item.UserId,
                            item.MetadataJson,
                            item.OccurredAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var participants =
            await _dbContext
                .RemoteSessionParticipants
                .AsNoTracking()
                .Where(
                    item =>
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.OrganizationId ==
                            context.Value.OrganizationId)
                .OrderBy(
                    item =>
                        item.JoinedAtUtc)
                .Select(
                    item =>
                        new
                        {
                            item.Id,
                            item.UserId,
                            item.DisplayName,
                            item.CanControl,
                            item.IsConnected,
                            item.JoinedAtUtc,
                            item.ConnectedAtUtc,
                            item.DisconnectedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var controlLease =
            await _dbContext
                .RemoteSessionControlLeases
                .AsNoTracking()
                .Where(
                    item =>
                        item.RemoteSessionId ==
                            sessionId
                        &&
                        item.OrganizationId ==
                            context.Value.OrganizationId
                        &&
                        item.ExpiresAtUtc >
                            DateTime.UtcNow)
                .Select(
                    item =>
                        new
                        {
                            item.Id,
                            item.UserId,
                            item.DisplayName,
                            item.AcquiredAtUtc,
                            item.ExpiresAtUtc
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        return Ok(
            new
            {
                session.Id,
                session.DeviceId,
                session.RequestedByUserId,
                session.TechnicianName,
                session.Reason,

                status =
                    session.Status
                        .ToString(),

                session.AllowKeyboard,
                session.AllowMouse,
                session.AllowClipboard,
                session.AllowFileTransfer,

                session.RequestedAtUtc,
                session.ExpiresAtUtc,
                session.ConnectedAtUtc,
                session.DisconnectedAtUtc,

                session.FailureReason,
                session.TerminationReason,
                session.TerminatedBy,

                participants,
                controlLease,
                events
            });
    }

    // ============================================================
    // CREATE OR JOIN
    // ============================================================

    [HttpPost]
    public async Task<ActionResult>
        CreateSession(
            [FromBody]
            CreateRemoteSessionRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.manage"))
        {
            return Forbid();
        }

        if (
            request.DeviceId ==
            Guid.Empty)
        {
            return BadRequest(
                new
                {
                    message =
                        "DeviceId es obligatorio."
                });
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Reason))
        {
            return BadRequest(
                new
                {
                    message =
                        "Debe indicar el motivo de la sesión."
                });
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        /*
         * Scope se valida ANTES de cargar/operar el Device.
         */

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    request.DeviceId,
                    cancellationToken))
        {
            _logger.LogWarning(
                "Remote Support scope denied. User={UserId}, Device={DeviceId}, Organization={OrganizationId}.",
                context.Value.UserId,
                request.DeviceId,
                context.Value.OrganizationId);

            return Forbid();
        }

        var technicianName =
            GetTechnicianName(
                context.Value.UserId);

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            request.DeviceId
                        &&
                        item.OrganizationId ==
                            context.Value.OrganizationId
                        &&
                        !item.IsDeleted,
                    cancellationToken);

        if (device is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El dispositivo no existe dentro de la organización."
                });
        }

        if (
            device.Platform !=
            DevicePlatform.Windows)
        {
            return BadRequest(
                new
                {
                    message =
                        "El control remoto interactivo está disponible actualmente para Windows."
                });
        }

        if (!device.IsManaged)
        {
            return Conflict(
                new
                {
                    message =
                        "El dispositivo no está administrado actualmente por TitanMDM."
                });
        }

        /*
         * ========================================================
         * MULTI TECHNICIAN
         * ========================================================
         */

        var existingSession =
            await FindActiveSessionAsync(
                context.Value.OrganizationId,
                request.DeviceId,
                cancellationToken);

        if (
            existingSession is not null)
        {
            await EnsureParticipantAsync(
                context.Value.OrganizationId,
                existingSession.Id,
                context.Value.UserId,
                technicianName,
                cancellationToken);

            AddEvent(
                context.Value.OrganizationId,
                existingSession.Id,
                "PARTICIPANT_JOIN_REQUESTED",
                $"{technicianName} se incorporó a la sesión remota existente.",
                context.Value.UserId);

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            _logger.LogInformation(
                "Technician {UserId} joined existing remote session {SessionId} for device {DeviceId}.",
                context.Value.UserId,
                existingSession.Id,
                device.Id);

            return Ok(
                BuildSessionResponse(
                    existingSession,
                    joinedExisting:
                        true));
        }

        /*
         * ========================================================
         * NEW SESSION
         * ========================================================
         */

        var durationMinutes =
            Math.Clamp(
                request.MaximumDurationMinutes,
                5,
                480);

        var session =
            new RemoteSession(
                context.Value.OrganizationId,
                device.Id,
                context.Value.UserId,
                technicianName,
                request.Reason.Trim(),
                request.AllowKeyboard,
                request.AllowMouse,
                request.AllowClipboard,
                request.AllowFileTransfer,
                DateTime.UtcNow.AddMinutes(
                    durationMinutes));

        _dbContext
            .RemoteSessions
            .Add(
                session);

        _dbContext
            .RemoteSessionParticipants
            .Add(
                new RemoteSessionParticipant(
                    context.Value.OrganizationId,
                    session.Id,
                    context.Value.UserId,
                    technicianName,
                    canControl:
                        true));

        AddEvent(
            context.Value.OrganizationId,
            session.Id,
            "SESSION_REQUESTED",
            $"Sesión remota solicitada por {technicianName}.",
            context.Value.UserId);

        AddEvent(
            context.Value.OrganizationId,
            session.Id,
            "PARTICIPANT_ADDED",
            $"{technicianName} fue agregado como participante inicial.",
            context.Value.UserId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Remote session {SessionId} created for device {DeviceId} by user {UserId}.",
            session.Id,
            device.Id,
            context.Value.UserId);

        return CreatedAtAction(
            nameof(
                GetSession),
            new
            {
                sessionId =
                    session.Id
            },
            BuildSessionResponse(
                session,
                joinedExisting:
                    false));
    }

    // ============================================================
    // PARTICIPANTS
    // ============================================================

    [HttpGet("{sessionId:guid}/participants")]
    public async Task<ActionResult>
        GetParticipants(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.view"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var session =
            await GetSessionForAccessAsync(
                context.Value,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        var participants =
            await _dbContext
                .RemoteSessionParticipants
                .AsNoTracking()
                .Where(
                    item =>
                        item.OrganizationId ==
                            context.Value.OrganizationId
                        &&
                        item.RemoteSessionId ==
                            sessionId)
                .OrderByDescending(
                    item =>
                        item.IsConnected)
                .ThenBy(
                    item =>
                        item.JoinedAtUtc)
                .Select(
                    item =>
                        new
                        {
                            item.Id,
                            item.UserId,
                            item.DisplayName,
                            item.CanControl,
                            item.IsConnected,
                            item.JoinedAtUtc,
                            item.ConnectedAtUtc,
                            item.DisconnectedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            participants);
    }

    // ============================================================
    // CONTROL STATE
    // ============================================================

    [HttpGet("{sessionId:guid}/control")]
    public async Task<ActionResult>
        GetControlLease(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.view"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var session =
            await GetSessionForAccessAsync(
                context.Value,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        var lease =
            await _controlLeases
                .GetStateAsync(
                    context.Value.OrganizationId,
                    sessionId,
                    cancellationToken);

        return Ok(
            BuildLeaseResponse(
                lease,
                lease.UserId ==
                    context.Value.UserId));
    }

    // ============================================================
    // ACQUIRE CONTROL
    // ============================================================

    [HttpPost("{sessionId:guid}/control/acquire")]
    public async Task<ActionResult>
        AcquireControl(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.manage"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        /*
         * IMPORTANTE:
         *
         * No tomamos aquí RemoteSupportConnectionRegistry.SessionLock.
         *
         * RemoteControlLeaseService es la autoridad del lock.
         *
         * Esto evita el double-lock/deadlock anterior.
         */

        var session =
            await GetActiveSessionAsync(
                context.Value.OrganizationId,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var lease =
                await _controlLeases
                    .AcquireAsync(
                        context.Value.OrganizationId,
                        sessionId,
                        context.Value.UserId,
                        GetTechnicianName(
                            context.Value.UserId),
                        cancellationToken);

            return Ok(
                BuildLeaseResponse(
                    lease,
                    ownedByCurrentUser:
                        true));
        }
        catch (
            HubException exception)
        {
            return Conflict(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // RENEW CONTROL
    // ============================================================

    [HttpPost("{sessionId:guid}/control/renew")]
    public async Task<ActionResult>
        RenewControl(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.manage"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var session =
            await GetActiveSessionAsync(
                context.Value.OrganizationId,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var lease =
                await _controlLeases
                    .RenewAsync(
                        context.Value.OrganizationId,
                        sessionId,
                        context.Value.UserId,
                        cancellationToken);

            return Ok(
                BuildLeaseResponse(
                    lease,
                    true));
        }
        catch (
            HubException exception)
        {
            return Conflict(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // RELEASE CONTROL
    // ============================================================

    [HttpPost("{sessionId:guid}/control/release")]
    public async Task<ActionResult>
        ReleaseControl(
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.manage"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var session =
            await GetSessionForAccessAsync(
                context.Value,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        if (
            !await CanAccessSessionDeviceAsync(
                context.Value,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        var released =
            await _controlLeases
                .ReleaseAsync(
                    context.Value.OrganizationId,
                    sessionId,
                    context.Value.UserId,
                    GetTechnicianName(
                        context.Value.UserId),
                    cancellationToken);

        return Ok(
            new
            {
                released
            });
    }

    // ============================================================
    // TERMINATE
    // ============================================================

   
[HttpPost("{sessionId:guid}/terminate")]
public async Task<ActionResult> TerminateSession(
    Guid sessionId,
    [FromBody] TerminateRemoteSessionRequest? request,
    CancellationToken cancellationToken = default)
{
    if (!HasPermission("remote.manage"))
    {
        return Forbid();
    }

    var context = GetSecurityContext();

    if (context is null)
    {
        return Unauthorized();
    }

    var session = await _dbContext.RemoteSessions
        .FirstOrDefaultAsync(
            item =>
                item.Id == sessionId &&
                item.OrganizationId == context.Value.OrganizationId,
            cancellationToken);

    if (session is null)
    {
        return NotFound();
    }

    if (!await CanAccessSessionDeviceAsync(
            context.Value,
            session.DeviceId,
            cancellationToken))
    {
        return Forbid();
    }

    var technicianName =
        GetTechnicianName(context.Value.UserId);

    if (session.Status != RemoteSessionStatus.Completed &&
        session.Status != RemoteSessionStatus.Failed &&
        session.Status != RemoteSessionStatus.Expired &&
        session.Status != RemoteSessionStatus.Cancelled)
    {
        session.Complete(
            technicianName,
            request?.Reason ??
            "Sesión finalizada desde TitanMDM.");

        var leases = await _dbContext
            .RemoteSessionControlLeases
            .Where(item =>
                item.OrganizationId == context.Value.OrganizationId &&
                item.RemoteSessionId == sessionId)
            .ToListAsync(cancellationToken);

        _dbContext.RemoteSessionControlLeases.RemoveRange(leases);

        var participants = await _dbContext
            .RemoteSessionParticipants
            .Where(item =>
                item.OrganizationId == context.Value.OrganizationId &&
                item.RemoteSessionId == sessionId)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants)
        {
            participant.RevokeControl();

            if (participant.IsConnected)
            {
                participant.MarkDisconnected();
            }
        }

        AddEvent(
            context.Value.OrganizationId,
            session.Id,
            "SESSION_TERMINATED",
            $"Sesión finalizada por {technicianName}.",
            context.Value.UserId);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    _connections.RemoveControlLease(
        context.Value.OrganizationId,
        sessionId);

    var technicianGroup = RemoteSupportHub.TechnicianGroup(
        context.Value.OrganizationId,
        sessionId);

    var hostGroup = RemoteSupportHub.HostGroup(
        context.Value.OrganizationId,
        sessionId);

    // La transacción lógica ya terminó.
    // Un fallo de notificación no revierte la sesión en SQL.
    try
    {
        await _remoteHub.Clients
            .Group(technicianGroup)
            .SendAsync(
                "RemoteSessionUpdated",
                new
                {
                    sessionId,
                    status = session.Status.ToString(),
                    connectedAtUtc = session.ConnectedAtUtc
                },
                CancellationToken.None);

        await _remoteHub.Clients
            .Group(technicianGroup)
            .SendAsync(
                "RemoteHostStateChanged",
                new
                {
                    sessionId,
                    connected = false
                },
                CancellationToken.None);

        await _remoteHub.Clients
            .Group(hostGroup)
            .SendAsync(
                "TerminateRemoteSession",
                CancellationToken.None);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(
            ex,
            "RemoteSession={SessionId} finalizó, " +
            "pero falló su notificación SignalR.",
            sessionId);
    }

    return Ok(new
    {
        session.Id,
        status = session.Status.ToString(),
        session.DisconnectedAtUtc,
        session.TerminatedBy,
        session.TerminationReason
    });
}

    // ============================================================
    // HELPERS - SESSION
    // ============================================================

    private async Task<RemoteSession?>
        FindActiveSessionAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken)
    {
        return await _dbContext
            .RemoteSessions
            .FirstOrDefaultAsync(
                session =>
                    session.OrganizationId ==
                        organizationId
                    &&
                    session.DeviceId ==
                        deviceId
                    &&
                    session.ExpiresAtUtc >
                        DateTime.UtcNow
                    &&
                    (
                        session.Status ==
                            RemoteSessionStatus.Requested
                        ||
                        session.Status ==
                            RemoteSessionStatus.Connecting
                        ||
                        session.Status ==
                            RemoteSessionStatus.Connected
                        ||
                        session.Status ==
                            RemoteSessionStatus.Disconnecting
                    ),
                cancellationToken);
    }

    private async Task<RemoteSession?>
        GetActiveSessionAsync(
            Guid organizationId,
            Guid sessionId,
            CancellationToken cancellationToken)
    {
        return await _dbContext
            .RemoteSessions
            .FirstOrDefaultAsync(
                session =>
                    session.OrganizationId ==
                        organizationId
                    &&
                    session.Id ==
                        sessionId
                    &&
                    session.ExpiresAtUtc >
                        DateTime.UtcNow
                    &&
                    session.Status !=
                        RemoteSessionStatus.Completed
                    &&
                    session.Status !=
                        RemoteSessionStatus.Failed
                    &&
                    session.Status !=
                        RemoteSessionStatus.Expired
                    &&
                    session.Status !=
                        RemoteSessionStatus.Cancelled,
                cancellationToken);
    }

    private async Task<RemoteSession?>
        GetSessionForAccessAsync(
            SecurityContext context,
            Guid sessionId,
            CancellationToken cancellationToken)
    {
        if (
            sessionId ==
            Guid.Empty)
        {
            return null;
        }

        return await _dbContext
            .RemoteSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                session =>
                    session.OrganizationId ==
                        context.OrganizationId
                    &&
                    session.Id ==
                        sessionId,
                cancellationToken);
    }

    private Task<bool>
        CanAccessSessionDeviceAsync(
            SecurityContext context,
            Guid deviceId,
            CancellationToken cancellationToken)
    {
        return _scopeAccessService
            .CanAccessDeviceAsync(
                context.OrganizationId,
                context.UserId,
                deviceId,
                cancellationToken);
    }

    // ============================================================
    // HELPERS - PARTICIPANT
    // ============================================================

    private async Task EnsureParticipantAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string technicianName,
        CancellationToken cancellationToken)
    {
        var exists =
            await _dbContext
                .RemoteSessionParticipants
                .AnyAsync(
                    participant =>
                        participant.OrganizationId ==
                            organizationId
                        &&
                        participant.RemoteSessionId ==
                            sessionId
                        &&
                        participant.UserId ==
                            userId,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        _dbContext
            .RemoteSessionParticipants
            .Add(
                new RemoteSessionParticipant(
                    organizationId,
                    sessionId,
                    userId,
                    technicianName,
                    canControl:
                        false));
    }

    // ============================================================
    // HELPERS - EVENTS
    // ============================================================

    private void AddEvent(
        Guid organizationId,
        Guid sessionId,
        string eventType,
        string description,
        Guid? userId)
    {
        _dbContext
            .RemoteSessionEvents
            .Add(
                new RemoteSessionEvent(
                    organizationId,
                    sessionId,
                    eventType,
                    description,
                    userId));
    }

    // ============================================================
    // RESPONSE
    // ============================================================

    private static object BuildSessionResponse(
        RemoteSession session,
        bool joinedExisting)
    {
        return new
        {
            session.Id,
            session.DeviceId,
            session.TechnicianName,
            session.Reason,

            status =
                session.Status
                    .ToString(),

            session.AllowKeyboard,
            session.AllowMouse,
            session.AllowClipboard,
            session.AllowFileTransfer,

            session.RequestedAtUtc,
            session.ExpiresAtUtc,
            session.ConnectedAtUtc,
            session.DisconnectedAtUtc,

            joinedExisting
        };
    }

    /*
     * IMPORTANTE:
     *
     * RemoteControlLeaseService devuelve
     * RemoteControlLeaseState.
     *
     * No volver a usar RemoteSessionControlLease aquí.
     */

    private static object BuildLeaseResponse(
        RemoteControlLeaseState lease,
        bool ownedByCurrentUser)
    {
        return new
        {
            hasController =
                lease.HasController,

            ownedByCurrentUser =
                lease.HasController
                &&
                ownedByCurrentUser,

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

    // ============================================================
    // SECURITY
    // ============================================================

    private SecurityContext?
        GetSecurityContext()
    {
        var organizationText =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userText =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        if (
            !Guid.TryParse(
                organizationText,
                out var organizationId)
            ||
            !Guid.TryParse(
                userText,
                out var userId))
        {
            return null;
        }

        return new SecurityContext(
            organizationId,
            userId);
    }

    private bool HasPermission(
        string permission)
    {
        return User
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
    }

    private string GetTechnicianName(
        Guid userId)
    {
        return
            User.FindFirstValue(
                ClaimTypes.Name)
            ??
            User.FindFirstValue(
                ClaimTypes.Email)
            ??
            userId.ToString();
    }

    private readonly record struct
        SecurityContext(
            Guid OrganizationId,
            Guid UserId);
}

// ================================================================
// CONTRACTS
// ================================================================

public sealed record CreateRemoteSessionRequest(
    Guid DeviceId,
    string Reason,
    bool AllowKeyboard = true,
    bool AllowMouse = true,
    bool AllowClipboard = false,
    bool AllowFileTransfer = false,
    int MaximumDurationMinutes = 120);

public sealed record TerminateRemoteSessionRequest(
    string? Reason);