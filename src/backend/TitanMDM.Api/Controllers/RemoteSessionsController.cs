using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/remote-sessions")]
[Authorize]
public sealed class RemoteSessionsController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly ILogger<
        RemoteSessionsController>
        _logger;

    public RemoteSessionsController(
        TitanMdmDbContext dbContext,
        ILogger<RemoteSessionsController> logger)
    {
        _dbContext =
            dbContext;

        _logger =
            logger;
    }

    /*
     * ============================================================
     * LIST
     * ============================================================
     */

    [HttpGet]
    public async Task<ActionResult>
        GetSessions(
            [FromQuery]
            int take = 50,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.view"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        take =
            Math.Clamp(
                take,
                1,
                200);

        var sessions =
            await _dbContext
                .RemoteSessions
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .OrderByDescending(
                    x =>
                        x.RequestedAtUtc)
                .Take(take)
                .Select(
                    x => new
                    {
                        x.Id,
                        x.DeviceId,
                        x.RequestedByUserId,
                        x.TechnicianName,
                        x.Reason,

                        status =
                            x.Status.ToString(),

                        x.AllowKeyboard,
                        x.AllowMouse,
                        x.AllowClipboard,
                        x.AllowFileTransfer,

                        x.RequestedAtUtc,
                        x.ExpiresAtUtc,
                        x.ConnectedAtUtc,
                        x.DisconnectedAtUtc,

                        x.FailureReason,
                        x.TerminationReason,
                        x.TerminatedBy,

                        participantCount =
                            _dbContext
                                .RemoteSessionParticipants
                                .Count(
                                    participant =>
                                        participant.RemoteSessionId ==
                                            x.Id
                                        &&
                                        participant.OrganizationId ==
                                            organizationId),

                        connectedParticipants =
                            _dbContext
                                .RemoteSessionParticipants
                                .Count(
                                    participant =>
                                        participant.RemoteSessionId ==
                                            x.Id
                                        &&
                                        participant.OrganizationId ==
                                            organizationId
                                        &&
                                        participant.IsConnected)
                    })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            sessions);
    }

    /*
     * ============================================================
     * DETAIL
     * ============================================================
     */

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

        var organizationId =
            GetOrganizationId();

        var session =
            await _dbContext
                .RemoteSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            sessionId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        var events =
            await _dbContext
                .RemoteSessionEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.OccurredAtUtc)
                .Select(
                    x => new
                    {
                        x.Id,
                        x.EventType,
                        x.Description,
                        x.UserId,
                        x.MetadataJson,
                        x.OccurredAtUtc
                    })
                .ToListAsync(
                    cancellationToken);

        var participants =
            await _dbContext
                .RemoteSessionParticipants
                .AsNoTracking()
                .Where(
                    x =>
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.JoinedAtUtc)
                .Select(
                    x => new
                    {
                        x.Id,
                        x.UserId,
                        x.DisplayName,
                        x.CanControl,
                        x.IsConnected,
                        x.JoinedAtUtc,
                        x.ConnectedAtUtc,
                        x.DisconnectedAtUtc
                    })
                .ToListAsync(
                    cancellationToken);

        var controlLease =
            await _dbContext
                .RemoteSessionControlLeases
                .AsNoTracking()
                .Where(
                    x =>
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.ExpiresAtUtc >
                            DateTime.UtcNow)
                .Select(
                    x => new
                    {
                        x.Id,
                        x.UserId,
                        x.DisplayName,
                        x.AcquiredAtUtc,
                        x.ExpiresAtUtc
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
                    session.Status.ToString(),

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

    /*
     * ============================================================
     * CREATE OR JOIN
     * ============================================================
     */

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

        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        var technicianName =
            GetTechnicianName(
                userId);

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.DeviceId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        !x.IsDeleted,
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
                        "El control remoto interactivo de este bloque está disponible para Windows."
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
         *
         * Si ya existe una sesión activa para este dispositivo,
         * NO creamos otro RemoteHost ni otro stream.
         *
         * El técnico se añade como participante.
         */

        var existingSession =
            await FindActiveSessionAsync(
                organizationId,
                request.DeviceId,
                cancellationToken);

        if (existingSession is not null)
        {
            await EnsureParticipantAsync(
                organizationId,
                existingSession.Id,
                userId,
                technicianName,
                cancellationToken);

            AddEvent(
                organizationId,
                existingSession.Id,
                "PARTICIPANT_JOIN_REQUESTED",
                $"{technicianName} se incorporó a la sesión remota existente.",
                userId);

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            _logger.LogInformation(
                "Technician {UserId} joined existing remote session {SessionId} for device {DeviceId}.",
                userId,
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
                organizationId,
                device.Id,
                userId,
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

        var participant =
            new RemoteSessionParticipant(
                organizationId,
                session.Id,
                userId,
                technicianName,
                canControl:
                    true);

        _dbContext
            .RemoteSessionParticipants
            .Add(
                participant);

        AddEvent(
            organizationId,
            session.Id,
            "SESSION_REQUESTED",
            $"Sesión remota solicitada por {technicianName}.",
            userId);

        AddEvent(
            organizationId,
            session.Id,
            "PARTICIPANT_ADDED",
            $"{technicianName} fue agregado como participante inicial.",
            userId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Remote session {SessionId} created for device {DeviceId} by user {UserId}.",
            session.Id,
            device.Id,
            userId);

        return CreatedAtAction(
            nameof(GetSession),
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

    /*
     * ============================================================
     * PARTICIPANTS
     * ============================================================
     */

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

        var organizationId =
            GetOrganizationId();

        if (
            !await SessionExistsAsync(
                organizationId,
                sessionId,
                cancellationToken))
        {
            return NotFound();
        }

        var participants =
            await _dbContext
                .RemoteSessionParticipants
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId)
                .OrderByDescending(
                    x =>
                        x.IsConnected)
                .ThenBy(
                    x =>
                        x.JoinedAtUtc)
                .Select(
                    x => new
                    {
                        x.Id,
                        x.UserId,
                        x.DisplayName,
                        x.CanControl,
                        x.IsConnected,
                        x.JoinedAtUtc,
                        x.ConnectedAtUtc,
                        x.DisconnectedAtUtc
                    })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            participants);
    }

    /*
     * ============================================================
     * CONTROL LEASE
     * ============================================================
     */

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

        var organizationId =
            GetOrganizationId();

        if (
            !await SessionExistsAsync(
                organizationId,
                sessionId,
                cancellationToken))
        {
            return NotFound();
        }

        await RemoveExpiredLeaseAsync(
            organizationId,
            sessionId,
            cancellationToken);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId,
                    cancellationToken);

        if (lease is null)
        {
            return Ok(
                new
                {
                    hasController =
                        false
                });
        }

        return Ok(
            new
            {
                hasController =
                    true,

                lease.UserId,
                lease.DisplayName,
                lease.AcquiredAtUtc,
                lease.ExpiresAtUtc
            });
    }

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

        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        var technicianName =
            GetTechnicianName(
                userId);

        var session =
            await GetActiveSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        await EnsureParticipantAsync(
            organizationId,
            sessionId,
            userId,
            technicianName,
            cancellationToken);

        await RemoveExpiredLeaseAsync(
            organizationId,
            sessionId,
            cancellationToken);

        /*
         * Serializable ayuda a evitar que dos técnicos obtengan
         * control simultáneamente.
         *
         * El índice UNIQUE de SessionId es la segunda defensa.
         */

        await using var transaction =
            await _dbContext
                .Database
                .BeginTransactionAsync(
                    System.Data
                        .IsolationLevel
                        .Serializable,
                    cancellationToken);

        try
        {
            var lease =
                await _dbContext
                    .RemoteSessionControlLeases
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.RemoteSessionId ==
                                sessionId,
                        cancellationToken);

            if (lease is not null)
            {
                if (
                    lease.UserId ==
                    userId)
                {
                    lease.Renew(
                        TimeSpan.FromSeconds(
                            45));

                    await _dbContext
                        .SaveChangesAsync(
                            cancellationToken);

                    await transaction
                        .CommitAsync(
                            cancellationToken);

                    return Ok(
                        BuildLeaseResponse(
                            lease,
                            ownedByCurrentUser:
                                true));
                }

                await transaction
                    .RollbackAsync(
                        cancellationToken);

                return Conflict(
                    new
                    {
                        message =
                            $"El control está siendo utilizado por {lease.DisplayName}.",

                        controller =
                            lease.DisplayName,

                        lease.UserId,
                        lease.ExpiresAtUtc
                    });
            }

            lease =
                new RemoteSessionControlLease(
                    organizationId,
                    sessionId,
                    userId,
                    technicianName,
                    TimeSpan.FromSeconds(
                        45));

            _dbContext
                .RemoteSessionControlLeases
                .Add(
                    lease);

            var participant =
                await _dbContext
                    .RemoteSessionParticipants
                    .FirstAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.RemoteSessionId ==
                                sessionId
                            &&
                            x.UserId ==
                                userId,
                        cancellationToken);

            participant
                .GrantControl();

            AddEvent(
                organizationId,
                sessionId,
                "CONTROL_ACQUIRED",
                $"{technicianName} obtuvo control de teclado y mouse.",
                userId);

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            await transaction
                .CommitAsync(
                    cancellationToken);

            return Ok(
                BuildLeaseResponse(
                    lease,
                    ownedByCurrentUser:
                        true));
        }
        catch (DbUpdateException)
        {
            await transaction
                .RollbackAsync(
                    cancellationToken);

            return Conflict(
                new
                {
                    message =
                        "Otro técnico obtuvo el control de la sesión simultáneamente."
                });
        }
    }

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

        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.UserId ==
                            userId,
                    cancellationToken);

        if (lease is null)
        {
            return Conflict(
                new
                {
                    message =
                        "El usuario actual no posee el control."
                });
        }

        lease.Renew(
            TimeSpan.FromSeconds(
                45));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            BuildLeaseResponse(
                lease,
                ownedByCurrentUser:
                    true));
    }

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

        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        var technicianName =
            GetTechnicianName(
                userId);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.UserId ==
                            userId,
                    cancellationToken);

        if (lease is null)
        {
            return Ok(
                new
                {
                    released =
                        false
                });
        }

        _dbContext
            .RemoteSessionControlLeases
            .Remove(
                lease);

        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.UserId ==
                            userId,
                    cancellationToken);

        participant
            ?.RevokeControl();

        AddEvent(
            organizationId,
            sessionId,
            "CONTROL_RELEASED",
            $"{technicianName} liberó el control remoto.",
            userId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            new
            {
                released =
                    true
            });
    }

    /*
     * ============================================================
     * TERMINATE
     * ============================================================
     */

    [HttpPost("{sessionId:guid}/terminate")]
    public async Task<ActionResult>
        TerminateSession(
            Guid sessionId,
            [FromBody]
            TerminateRemoteSessionRequest? request,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                "remote.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        var technicianName =
            GetTechnicianName(
                userId);

        var session =
            await _dbContext
                .RemoteSessions
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            sessionId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        session.Complete(
            technicianName,
            request?.Reason
            ??
            "Sesión finalizada desde TitanMDM.");

        var leases =
            await _dbContext
                .RemoteSessionControlLeases
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId)
                .ToListAsync(
                    cancellationToken);

        _dbContext
            .RemoteSessionControlLeases
            .RemoveRange(
                leases);

        var participants =
            await _dbContext
                .RemoteSessionParticipants
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var participant
            in participants)
        {
            participant
                .RevokeControl();

            if (
                participant.IsConnected)
            {
                participant
                    .MarkDisconnected();
            }
        }

        AddEvent(
            organizationId,
            session.Id,
            "SESSION_TERMINATED",
            $"Sesión finalizada por {technicianName}.",
            userId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            new
            {
                session.Id,

                status =
                    session.Status.ToString(),

                session.DisconnectedAtUtc,
                session.TerminatedBy,
                session.TerminationReason
            });
    }

    /*
     * ============================================================
     * HELPERS
     * ============================================================
     */

    private async Task<RemoteSession?>
        FindActiveSessionAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken)
    {
        return await _dbContext
            .RemoteSessions
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.DeviceId ==
                        deviceId
                    &&
                    x.ExpiresAtUtc >
                        DateTime.UtcNow
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
                        ||
                        x.Status ==
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
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Id ==
                        sessionId
                    &&
                    x.ExpiresAtUtc >
                        DateTime.UtcNow
                    &&
                    x.Status !=
                        RemoteSessionStatus.Completed
                    &&
                    x.Status !=
                        RemoteSessionStatus.Failed
                    &&
                    x.Status !=
                        RemoteSessionStatus.Expired
                    &&
                    x.Status !=
                        RemoteSessionStatus.Cancelled,
                cancellationToken);
    }

    private async Task<bool>
        SessionExistsAsync(
            Guid organizationId,
            Guid sessionId,
            CancellationToken cancellationToken)
    {
        return await _dbContext
            .RemoteSessions
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Id ==
                        sessionId,
                cancellationToken);
    }

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
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.UserId ==
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

    private async Task RemoveExpiredLeaseAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.ExpiresAtUtc <=
                            DateTime.UtcNow,
                    cancellationToken);

        if (lease is null)
        {
            return;
        }

        _dbContext
            .RemoteSessionControlLeases
            .Remove(
                lease);

        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RemoteSessionId ==
                            sessionId
                        &&
                        x.UserId ==
                            lease.UserId,
                    cancellationToken);

        participant
            ?.RevokeControl();

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

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
                session.Status.ToString(),

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

    private static object BuildLeaseResponse(
        RemoteSessionControlLease lease,
        bool ownedByCurrentUser)
    {
        return new
        {
            hasController =
                true,

            ownedByCurrentUser,

            lease.UserId,
            lease.DisplayName,
            lease.AcquiredAtUtc,
            lease.ExpiresAtUtc
        };
    }

    private bool HasPermission(
        string permission)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }

    private Guid GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        if (
            !Guid.TryParse(
                value,
                out var organizationId)
            ||
            organizationId ==
                Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Organization claim is missing.");
        }

        return organizationId;
    }

    private Guid GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (
            !Guid.TryParse(
                value,
                out var userId)
            ||
            userId ==
                Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "User claim is missing.");
        }

        return userId;
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
}

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