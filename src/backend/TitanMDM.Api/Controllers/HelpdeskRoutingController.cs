using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Infrastructure.Helpdesk;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/routing")]
public sealed class HelpdeskRoutingController
    : ControllerBase
{
    private readonly IHelpdeskService
        _service;

    public HelpdeskRoutingController(
        IHelpdeskService service)
    {
        _service =
            service;
    }

    // ============================================================
    // PREVIEW BY REQUESTER
    // ============================================================

    [HttpGet("preview")]
    public async Task<IActionResult>
        Preview(
            [FromQuery]
            Guid requesterId,
            [FromQuery]
            string category = "general",
            CancellationToken cancellationToken = default)
    {
        if (!CanViewRouting())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (requesterId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica un solicitante válido."
                });
        }

        category =
            category?
                .Trim()
                .ToLowerInvariant()
            ??
            "general";

        if (string.IsNullOrWhiteSpace(
                category)
            ||
            category.Length > 80)
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica una categoría válida."
                });
        }

        var routing =
            GetRoutingService();

        if (routing is null)
        {
            return Problem(
                detail:
                    "El servicio de routing de Helpdesk no está disponible.",
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
        }

        var result =
            await routing
                .PreviewRoutingAsync(
                    organizationId,
                    requesterId,
                    category,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // DIAGNOSTIC FOR EXISTING TICKET
    // ============================================================

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult>
        TicketDiagnostic(
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        if (!CanViewRouting())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (ticketId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica un ticket válido."
                });
        }

        var routing =
            GetRoutingService();

        if (routing is null)
        {
            return Problem(
                detail:
                    "El servicio de routing de Helpdesk no está disponible.",
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
        }

        var result =
            await routing
                .PreviewTicketRoutingAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        if (result is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                });
        }

        return Ok(
            result);
    }

    // ============================================================
    // RETRY AUTOMATIC ASSIGNMENT
    // ============================================================

    [HttpPost("tickets/{ticketId:guid}/retry")]
    public async Task<IActionResult>
        Retry(
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        if (!CanManageRouting())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (ticketId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica un ticket válido."
                });
        }

        var routing =
            GetRoutingService();

        if (routing is null)
        {
            return Problem(
                detail:
                    "El servicio de routing de Helpdesk no está disponible.",
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
        }

        /*
         * Primero obtenemos el diagnóstico.
         *
         * Esto permite devolver al operador una explicación
         * útil si no existe candidato elegible.
         */
        var diagnostic =
            await routing
                .PreviewTicketRoutingAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        if (diagnostic is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                });
        }

        if (!diagnostic.CanAssign)
        {
            return Conflict(
                new
                {
                    message =
                        diagnostic.Reason,

                    diagnostic
                });
        }

        var assigned =
            await routing
                .RetryAutomaticAssignmentAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        if (!assigned)
        {
            /*
             * Puede ocurrir si entre el preview y el commit
             * cambió la carga/capacidad o otro operador
             * asignó el ticket.
             */
            var refreshed =
                await routing
                    .PreviewTicketRoutingAsync(
                        organizationId,
                        ticketId,
                        cancellationToken);

            return Conflict(
                new
                {
                    message =
                        refreshed?.Reason
                        ??
                        "No se pudo completar la autoasignación porque el estado del ticket o la capacidad del técnico cambió.",

                    diagnostic =
                        refreshed
                });
        }

        var finalDiagnostic =
            await routing
                .PreviewTicketRoutingAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        return Ok(
            new
            {
                success =
                    true,

                message =
                    "El ticket fue asignado automáticamente.",

                diagnostic =
                    finalDiagnostic
                    ??
                    diagnostic
            });
    }

    // ============================================================
    // SERVICE
    // ============================================================

    private HelpdeskService?
        GetRoutingService()
    {
        return _service
            as HelpdeskService;
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private bool TryGetOrganization(
        out Guid organizationId)
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out organizationId);
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private bool CanViewRouting()
    {
        return HasAnyPermission(
            "helpdesk.routing.view",
            "helpdesk.groups.view",
            "helpdesk.technicians.view",
            "helpdesk.ticket.details.view",
            "helpdesk.admin.access",

            // Compatibilidad legacy.
            "tickets.view",
            "helpdesk.view",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool CanManageRouting()
    {
        return HasAnyPermission(
            "helpdesk.routing.manage",
            "helpdesk.ticket.assign",
            "helpdesk.groups.manage",
            "helpdesk.technicians.manage",
            "helpdesk.admin.access",

            // Compatibilidad legacy.
            "tickets.assign",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                permissions.Any(
                    permission =>
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }
}