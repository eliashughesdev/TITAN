
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

/// <summary>
/// Puerta de autorización para operaciones de elevación remota.
///
/// Esta versión NO ejecuta procesos, NO obtiene contraseñas
/// y NO entrega tokens privilegiados al navegador.
///
/// La capacidad real permanecerá bloqueada hasta conectar
/// un ejecutor autorizado y auditado.
/// </summary>
[ApiController]
[Authorize]
[Route("api/remote-sessions/{sessionId:guid}/elevation")]
public sealed class RemoteElevationController : ControllerBase
{
    private readonly TitanMdmDbContext _db;
    private readonly IScopeAccessService _scope;
    private readonly ILogger<RemoteElevationController> _logger;

    public RemoteElevationController(
        TitanMdmDbContext db,
        IScopeAccessService scope,
        ILogger<RemoteElevationController> logger)
    {
        _db = db;
        _scope = scope;
        _logger = logger;
    }

    // ========================================================
    // READ CAPABILITY
    // ========================================================

    [HttpGet("capability")]
    public async Task<IActionResult> GetCapability(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity();

        if (identity is null)
            return Unauthorized();

        if (!HasPermission("remote.view"))
            return Forbid();

        var session = await _db.RemoteSessions
            .AsNoTracking()
            .Where(x =>
                x.Id == sessionId &&
                x.OrganizationId == identity.Value.OrganizationId)
            .Select(x => new
            {
                x.Id,
                x.DeviceId,
                x.Status,
                x.ExpiresAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            return NotFound();

        if (!await _scope.CanAccessDeviceAsync(
                identity.Value.OrganizationId,
                identity.Value.UserId,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        var permissionPresent =
            HasPermission("remote.manage") &&
            HasPermission("remote.elevate");

        return Ok(new
        {
            sessionId,
            permissionPresent,
            sessionConnected =
                session.Status == RemoteSessionStatus.Connected &&
                session.ExpiresAtUtc > DateTime.UtcNow,
            interactiveElevationAvailable = false,
            managedElevationAvailable = false,
            status = "NOT_CONFIGURED",
            reason =
                "El ejecutor privilegiado todavía no está autorizado."
        });
    }

    // ========================================================
    // REQUEST PREFLIGHT
    // ========================================================

    [HttpPost("preflight")]
    public async Task<IActionResult> Preflight(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity();

        if (identity is null)
            return Unauthorized();

        if (!HasPermission("remote.manage") ||
            !HasPermission("remote.elevate"))
        {
            _logger.LogWarning(
                "Remote elevation permission denied. Session={SessionId}, User={UserId}.",
                sessionId,
                identity.Value.UserId);

            return Forbid();
        }

        var session = await _db.RemoteSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == sessionId &&
                    x.OrganizationId == identity.Value.OrganizationId,
                cancellationToken);

        if (session is null)
            return NotFound();

        if (!await _scope.CanAccessDeviceAsync(
                identity.Value.OrganizationId,
                identity.Value.UserId,
                session.DeviceId,
                cancellationToken))
        {
            _logger.LogWarning(
                "Remote elevation scope denied. Session={SessionId}, User={UserId}.",
                sessionId,
                identity.Value.UserId);

            return Forbid();
        }

        var now = DateTime.UtcNow;

        if (session.Status != RemoteSessionStatus.Connected ||
            session.ExpiresAtUtc <= now)
        {
            return Conflict(new
            {
                code = "REMOTE_SESSION_NOT_CONNECTED",
                message =
                    "La sesión remota no está conectada o expiró."
            });
        }

        // La propiedad del control se valida contra SQL Server,
        // no contra un estado enviado por React.
        var ownsActiveLease =
            await _db.RemoteSessionControlLeases
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId == identity.Value.OrganizationId &&
                        x.RemoteSessionId == sessionId &&
                        x.UserId == identity.Value.UserId &&
                        x.ExpiresAtUtc > now,
                    cancellationToken);

        if (!ownsActiveLease)
        {
            return Conflict(new
            {
                code = "REMOTE_CONTROL_LEASE_REQUIRED",
                message =
                    "El técnico necesita un lease exclusivo vigente."
            });
        }

        // La auditoría conserva la identidad, sesión y resultado;
        // no almacena contraseñas, comandos ni capturas.
        _db.RemoteSessionEvents.Add(
            new RemoteSessionEvent(
                identity.Value.OrganizationId,
                sessionId,
                "ELEVATION_PREFLIGHT_BLOCKED",
                "Solicitud de elevación verificada; ejecutor no configurado.",
                identity.Value.UserId));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Remote elevation preflight blocked by configuration. Session={SessionId}, User={UserId}.",
            sessionId,
            identity.Value.UserId);

        return Conflict(new
        {
            code = "REMOTE_ELEVATION_NOT_CONFIGURED",
            message =
                "La elevación remota todavía no está habilitada.",
            interactiveElevationAvailable = false,
            managedElevationAvailable = false
        });
    }

    // ========================================================
    // AUTHENTICATION AND PERMISSIONS
    // ========================================================

    private (Guid OrganizationId, Guid UserId)? GetIdentity()
    {
        var organization =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        var user =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(organization, out var organizationId) ||
            !Guid.TryParse(user, out var userId))
        {
            return null;
        }

        return (organizationId, userId);
    }

    private bool HasPermission(string permission)
    {
        return User.Claims.Any(
            claim =>
                claim.Type == "permission" &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }
}
