using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Helpdesk;

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

        var diagnostic =
            await _service
                .GetRoutingDiagnosticAsync(
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

        return Ok(
            diagnostic);
    }

    // ============================================================
    // RETRY AUTOMATIC ASSIGNMENT
    // ============================================================

    // The canonical retry contract is owned by HelpdeskRoutingOperationsController.
    [NonAction]
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

        // ========================================================
        // 1. DIAGNOSTIC BEFORE WRITE
        // ========================================================

        var diagnostic =
            await _service
                .GetRoutingDiagnosticAsync(
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

        /*
         * El ticket ya puede estar asignado.
         *
         * En ese caso no debemos volver a ejecutar el motor.
         */
        if (diagnostic.AlreadyAssigned)
        {
            return Conflict(
                new
                {
                    message =
                        "El ticket ya tiene un técnico asignado.",

                    diagnostic
                });
        }

        if (diagnostic.PendingUser)
        {
            return Conflict(
                new
                {
                    message =
                        "El ticket está esperando respuesta del usuario y no puede autoasignarse.",

                    diagnostic
                });
        }

        /*
         * SelectedCandidate representa el candidato real que
         * devolvió EvaluateRoutingAsync().
         */
        if (diagnostic.SelectedCandidate is null)
        {
            return Conflict(
                new
                {
                    message =
                        string.IsNullOrWhiteSpace(
                            diagnostic.EngineReason)
                            ?
                            "No existe un técnico elegible para este ticket."
                            :
                            diagnostic.EngineReason,

                    diagnostic
                });
        }

        // ========================================================
        // 2. ENTERPRISE WRITE PATH
        // ========================================================

        var assigned =
            await _service
                .RetryAutomaticAssignmentEnterpriseAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        if (!assigned)
        {
            /*
             * Entre diagnóstico y commit pudieron cambiar:
             *
             * - carga;
             * - capacidad;
             * - disponibilidad;
             * - horario;
             * - membresía;
             * - estado del ticket;
             * - asignación por otro proceso.
             *
             * Volvemos a consultar el estado real.
             */
            var refreshed =
                await _service
                    .GetRoutingDiagnosticAsync(
                        organizationId,
                        ticketId,
                        cancellationToken);

            return Conflict(
                new
                {
                    message =
                        refreshed is not null
                        &&
                        !string.IsNullOrWhiteSpace(
                            refreshed.EngineReason)
                            ?
                            refreshed.EngineReason
                            :
                            "No se pudo completar la autoasignación durante la revalidación transaccional.",

                    diagnostic =
                        refreshed
                        ??
                        diagnostic
                });
        }

        // ========================================================
        // 3. FINAL STATE
        // ========================================================

        var finalDiagnostic =
            await _service
                .GetRoutingDiagnosticAsync(
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
    // ROUTING HEALTH
    // ============================================================

    // Avoid an ambiguous endpoint with HelpdeskRoutingOperationsController.
    [NonAction]
    public async Task<IActionResult>
        Health(
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

        var result =
            await _service
                .GetRoutingHealthAsync(
                    organizationId,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // DIAGNOSTIC BY BUSINESS REFERENCE
    //
    // Supports:
    // HD-18
    // hd-18
    // 18
    // GUID
    // ============================================================

    [HttpGet("diagnostic/{ticketReference}")]
    public async Task<IActionResult>
        DiagnosticByReference(
            string ticketReference,
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

        if (string.IsNullOrWhiteSpace(
                ticketReference))
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica un ticket válido."
                });
        }

        var diagnostic =
            await _service
                .GetRoutingDiagnosticByReferenceAsync(
                    organizationId,
                    ticketReference,
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

        return Ok(
            diagnostic);
    }

    // ============================================================
    // QUEUE SIMULATION
    // ============================================================

    [HttpPost("queue/simulate")]
    public async Task<IActionResult>
        SimulateQueue(
            [FromQuery]
            int maxTickets = 100,
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

        var result =
            await _service
                .RetryAutomaticAssignmentForOpenTicketsAsync(
                    organizationId,
                    Math.Clamp(
                        maxTickets,
                        1,
                        200),
                    true,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // QUEUE PROCESS
    // ============================================================

    [HttpPost("queue/process")]
    public async Task<IActionResult>
        ProcessQueue(
            [FromQuery]
            int maxTickets = 100,
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

        var result =
            await _service
                .RetryAutomaticAssignmentForOpenTicketsAsync(
                    organizationId,
                    Math.Clamp(
                        maxTickets,
                        1,
                        200),
                    false,
                    cancellationToken);

        return Ok(
            result);
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

            // Legacy compatibility.
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

            // Legacy compatibility.
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
