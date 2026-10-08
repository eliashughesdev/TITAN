using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public HelpdeskRoutingOperationsController(
        IHelpdeskService helpdesk)
    {
        _helpdesk =
            helpdesk;
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
                .RetryAutomaticAssignmentEnterpriseAsync(
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