
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Commands;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

/// <summary>
/// RS-H3.8
/// Operaciones administrativas gestionadas desde una
/// sesión de soporte remoto.
///
/// No ofrece terminal de comandos.
/// No transporta credenciales Windows.
/// No concede privilegios al usuario interactivo.
/// Las operaciones se procesan mediante la cola existente
/// de comandos de WindowsAgent.
/// </summary>
[ApiController]
[Authorize]
[Route("api/remote-sessions/{sessionId:guid}/managed-actions")]
public sealed class RemoteManagedActionsController : ControllerBase
{
    private const string RemoteManage = "remote.manage";
    private const string RemoteElevate = "remote.elevate";
    private const string DeviceCommands = "devices.commands";

    private readonly TitanMdmDbContext _db;
    private readonly IScopeAccessService _scope;
    private readonly IDeviceCommandService _commands;
    private readonly ILogger<RemoteManagedActionsController> _logger;

    public RemoteManagedActionsController(
        TitanMdmDbContext db,
        IScopeAccessService scope,
        IDeviceCommandService commands,
        ILogger<RemoteManagedActionsController> logger)
    {
        _db = db;
        _scope = scope;
        _commands = commands;
        _logger = logger;
    }

    // ========================================================
    // ALLOWED ACTIONS
    // ========================================================

    [HttpGet("catalog")]
    public IActionResult Catalog()
    {
        if (!HasPermission(RemoteManage))
        {
            return Forbid();
        }

        return Ok(new
        {
            operations = new[]
            {
                new
                {
                    code = "RESTART_PRINT_SPOOLER",
                    name = "Reiniciar cola de impresión",
                    description =
                        "Reinicia el servicio Spooler del equipo Windows.",
                    requiresElevationPermission = true,
                    requiresExclusiveControl = true
                }
            },
            arbitraryCommandsAllowed = false,
            interactiveUacAvailable = false
        });
    }

    // ========================================================
    // CREATE MANAGED ACTION
    // ========================================================

    [HttpPost]
    public async Task<IActionResult> Execute(
        Guid sessionId,
        [FromBody] RemoteManagedActionRequest request,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity();

        if (identity is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(RemoteManage) ||
            !HasPermission(RemoteElevate) ||
            !HasPermission(DeviceCommands))
        {
            _logger.LogWarning(
                "Managed action permission denied. Session={SessionId}, User={UserId}",
                sessionId,
                identity.Value.UserId);

            return Forbid();
        }

        if (request is null ||
            !string.Equals(
                request.Operation,
                "RESTART_PRINT_SPOOLER",
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                code = "OPERATION_NOT_ALLOWED",
                message =
                    "La operación solicitada no está autorizada."
            });
        }

        var session = await _db.RemoteSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == sessionId &&
                    item.OrganizationId ==
                        identity.Value.OrganizationId,
                cancellationToken);

        if (session is null)
        {
            return NotFound(new
            {
                code = "REMOTE_SESSION_NOT_FOUND"
            });
        }

        var now = DateTime.UtcNow;

        if (session.Status != RemoteSessionStatus.Connected ||
            session.ExpiresAtUtc <= now)
        {
            return Conflict(new
            {
                code = "REMOTE_SESSION_NOT_ACTIVE"
            });
        }

        if (!await _scope.CanAccessDeviceAsync(
                identity.Value.OrganizationId,
                identity.Value.UserId,
                session.DeviceId,
                cancellationToken))
        {
            return Forbid();
        }

        // SQL Server sigue siendo la autoridad del lease.
        // No confiar en ownsControl enviado por React.
        var ownsLease = await _db
            .RemoteSessionControlLeases
            .AsNoTracking()
            .AnyAsync(
                lease =>
                    lease.OrganizationId ==
                        identity.Value.OrganizationId &&
                    lease.RemoteSessionId == sessionId &&
                    lease.UserId == identity.Value.UserId &&
                    lease.ExpiresAtUtc > now,
                cancellationToken);

        if (!ownsLease)
        {
            return Conflict(new
            {
                code = "EXCLUSIVE_CONTROL_REQUIRED",
                message =
                    "Debes tener el control exclusivo vigente."
            });
        }

        // Registrar la solicitud antes de encolar la acción.
        // No se escriben contraseñas ni payload arbitrario.
        _db.RemoteSessionEvents.Add(
            new RemoteSessionEvent(
                identity.Value.OrganizationId,
                sessionId,
                "MANAGED_ACTION_REQUESTED",
                "Se solicitó reiniciar la cola de impresión.",
                identity.Value.UserId));

        await _db.SaveChangesAsync(cancellationToken);

        // Reutilizar la infraestructura de comandos actual.
        // Únicamente enviamos un nombre de servicio fijo.
        var payload = JsonSerializer.Serialize(new
        {
            ServiceName = "Spooler"
        });

        try
        {
            var command = await _commands.CreateAsync(
                identity.Value.OrganizationId,
                identity.Value.UserId,
                new CreateDeviceCommandRequest(
                    session.DeviceId,
                    "SERVICE_RESTART",
                    payload,
                    ExpirationMinutes: 5),
                cancellationToken);

            _db.RemoteSessionEvents.Add(
                new RemoteSessionEvent(
                    identity.Value.OrganizationId,
                    sessionId,
                    "MANAGED_ACTION_QUEUED",
                    $"Reinicio de Spooler encolado. CommandId={command.Id}",
                    identity.Value.UserId));

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Remote managed action queued. Session={SessionId}, Device={DeviceId}, Command={CommandId}, User={UserId}",
                sessionId,
                session.DeviceId,
                command.Id,
                identity.Value.UserId);

            return Accepted(new
            {
                sessionId,
                commandId = command.Id,
                operation = "RESTART_PRINT_SPOOLER",
                status = "QUEUED",
                message =
                    "Operación encolada. Su ejecución depende del agente Windows."
            });
        }
        catch (DeviceCommandException exception)
        {
            _logger.LogWarning(
                "Remote managed action rejected. Session={SessionId}, Code={Code}",
                sessionId,
                exception.Code);

            return Conflict(new
            {
                code = exception.Code,
                message = exception.Message
            });
        }
    }

    // ========================================================
    // SECURITY
    // ========================================================

    private (Guid OrganizationId, Guid UserId)? GetIdentity()
    {
        var organizationText =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        var userText =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(
                organizationText,
                out var organizationId) ||
            !Guid.TryParse(
                userText,
                out var userId))
        {
            return null;
        }

        return (organizationId, userId);
    }

    private bool HasPermission(string code)
    {
        return User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(
                claim.Value,
                code,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record RemoteManagedActionRequest(
    string Operation);
