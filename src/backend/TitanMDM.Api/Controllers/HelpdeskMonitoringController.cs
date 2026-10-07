using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/monitoring")]
public sealed class HelpdeskMonitoringController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    private static readonly string[] AlertTypes =
    [
        "unassigned_reminder",
        "first_response_warning",
        "first_response_overdue",
        "resolution_warning",
        "resolution_overdue"
    ];

    public HelpdeskMonitoringController(TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        if (!CanView()) return Forbid();
        if (!TryGetOrganization(out var organizationId)) return Unauthorized();

        var now = DateTime.UtcNow;
        var tickets = _db.HelpdeskTickets.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        var active = tickets.Where(x =>
            x.Status != "resolved" &&
            x.Status != "closed");

        var total = await tickets.CountAsync(cancellationToken);
        var activeCount = await active.CountAsync(cancellationToken);
        var unassigned = await active.CountAsync(
            x => x.AssigneeUserId == null,
            cancellationToken);

        var overdueFirstResponse = await active.CountAsync(
            x => x.FirstRespondedAtUtc == null &&
                 x.FirstResponseDueAtUtc != null &&
                 x.FirstResponseDueAtUtc < now,
            cancellationToken);

        var overdueResolution = await active.CountAsync(
            x => x.ResolvedAtUtc == null &&
                 x.ResolveDueAtUtc != null &&
                 x.ResolveDueAtUtc < now,
            cancellationToken);

        var byStatus = await tickets
            .GroupBy(x => x.Status)
            .Select(x => new { status = x.Key, count = x.Count() })
            .ToListAsync(cancellationToken);

        var byPriority = await tickets
            .GroupBy(x => x.Priority)
            .Select(x => new { priority = x.Key, count = x.Count() })
            .ToListAsync(cancellationToken);

        var byCategory = await tickets
            .GroupBy(x => x.Category)
            .Select(x => new { category = x.Key, count = x.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total,
            active = activeCount,
            unassigned,
            overdueFirstResponse,
            overdueResolution,
            byStatus,
            byPriority,
            byCategory,
            generatedAtUtc = now
        });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!CanView()) return Forbid();
        if (!TryGetOrganization(out var organizationId)) return Unauthorized();

        limit = Math.Clamp(limit, 1, 100);

        var alerts = await (
            from activity in _db.HelpdeskTicketEvents.AsNoTracking()
            join ticket in _db.HelpdeskTickets.AsNoTracking()
                on activity.TicketId equals ticket.Id
            where activity.OrganizationId == organizationId &&
                  ticket.OrganizationId == organizationId &&
                  AlertTypes.Contains(activity.EventType)
            orderby activity.CreatedAtUtc descending
            select new
            {
                activity.Id,
                activity.TicketId,
                ticket.Number,
                ticket.Subject,
                ticket.Status,
                ticket.Priority,
                ticket.AssigneeUserId,
                activity.EventType,
                activity.Summary,
                activity.CreatedAtUtc
            }
        )
        .Take(limit)
        .ToListAsync(cancellationToken);

        return Ok(alerts);
    }

    private bool CanView() =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            (string.Equals(claim.Value, "helpdesk.view", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "tickets.view", StringComparison.OrdinalIgnoreCase)));

    private bool TryGetOrganization(out Guid organizationId)
    {
        var value =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        return Guid.TryParse(value, out organizationId);
    }
}