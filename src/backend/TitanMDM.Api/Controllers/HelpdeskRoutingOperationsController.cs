using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using TitanMDM.Api.AI;
using TitanMDM.Api.Services;
using TitanMDM.Application.Helpdesk;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/routing")]
public sealed class HelpdeskRoutingOperationsController
    : ControllerBase
{
    private readonly IHelpdeskService
        _helpdesk;
    private readonly HelpdeskRoutingWorkerState _workerState;
    private readonly OpenRouterOptions _aiOptions;

    public HelpdeskRoutingOperationsController(
        IHelpdeskService helpdesk,
        HelpdeskRoutingWorkerState workerState,
        IOptions<OpenRouterOptions> aiOptions)
    {
        _helpdesk =
            helpdesk;
        _workerState = workerState;
        _aiOptions = aiOptions.Value;
    }

    [HttpGet("worker")]
    public ActionResult<HelpdeskRoutingWorkerSnapshot> GetWorker()
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(out _))
        {
            return Unauthorized();
        }

        var aiAvailable =
            _aiOptions.Enabled
            && _aiOptions.EnableClassification
            && !string.IsNullOrWhiteSpace(_aiOptions.ApiKey);

        return Ok(_workerState.Snapshot(aiAvailable));
    }

    // ============================================================
    // HEALTH
    // ============================================================

    [HttpGet("health")]
    public async Task<ActionResult<HelpdeskRoutingHealthSnapshot>>
        GetHealth(
            CancellationToken cancellationToken)
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var result =
            await _helpdesk
                .GetRoutingHealthAsync(
                    organizationId,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // TICKET DIAGNOSTIC
    // ============================================================

    // ============================================================
    // HD-D5 - TICKET DIAGNOSTIC BY REFERENCE
    //
    // Accepts:
    // - HD-16
    // - 16
    // - internal GUID
    // ============================================================

    [HttpGet("tickets/reference/{ticketReference}/diagnostic")]
    public async Task<ActionResult<HelpdeskRoutingDiagnosticSnapshot>>
        DiagnosticByReference(
            string ticketReference,
            CancellationToken cancellationToken)
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var result =
            await _helpdesk
                .GetRoutingDiagnosticByReferenceAsync(
                    organizationId,
                    ticketReference,
                    cancellationToken);

        if (result is null)
        {
            return NotFound(
                new
                {
                    message =
                        $"No se encontrÃ³ el ticket '{ticketReference}'."
                });
        }

        return Ok(
            result);
    }
    [HttpGet("tickets/{ticketId:guid}/diagnostic")]
    public async Task<ActionResult<HelpdeskRoutingDiagnosticSnapshot>>
        Diagnostic(
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var result =
            await _helpdesk
                .GetRoutingDiagnosticAsync(
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
    // RETRY SINGLE
    // ============================================================

    [HttpPost("tickets/{ticketId:guid}/retry")]
    public async Task<IActionResult>
        RetryTicket(
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var before =
            await _helpdesk
                .GetRoutingDiagnosticAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        if (before is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                });
        }

        var assigned =
            await _helpdesk
                .RetryAutomaticAssignmentWithFallbackAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        var after =
            await _helpdesk
                .GetRoutingDiagnosticAsync(
                    organizationId,
                    ticketId,
                    cancellationToken);

        return Ok(
            new
            {
                assigned,
                before,
                after
            });
    }

    // ============================================================
    // RETRY OPEN QUEUE
    // ============================================================

    [HttpPost("retry-open")]
    public async Task<ActionResult<HelpdeskRoutingQueueResult>>
        RetryOpen(
            [FromBody]
            RetryOpenRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var maxTickets =
            Math.Clamp(
                request.MaxTickets,
                1,
                200);

        var result =
            await _helpdesk
                .RetryAutomaticAssignmentForOpenTicketsAsync(
                    organizationId,
                    maxTickets,
                    request.DryRun,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // AUTHORIZATION
    // ============================================================

    private bool CanView()
    {
        return HasAnyPermission(
            "helpdesk.routing.view",
            "helpdesk.routing.manage",
            "helpdesk.automation.view",
            "helpdesk.automation.manage",
            "helpdesk.technicians.view",
            "helpdesk.technicians.manage",
            "helpdesk.groups.view",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool CanManage()
    {
        return HasAnyPermission(
            "helpdesk.routing.manage",
            "helpdesk.automation.manage",
            "helpdesk.technicians.manage",
            "helpdesk.groups.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return User.Claims
            .Any(
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
    // CONTRACTS
    // ============================================================

    public sealed record RetryOpenRequest(
        int MaxTickets = 50,
        bool DryRun = true);
}
