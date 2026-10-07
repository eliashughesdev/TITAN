using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/follow-up")]
public sealed class HelpdeskFollowupController
    : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    private static readonly string[] AlertTypes =
    [
        "unassigned_reminder",
        "assigned_attention_reminder",
        "inactivity_reminder",
        "first_response_warning",
        "first_response_overdue",
        "resolution_warning",
        "resolution_overdue",
        "auto_handover"
    ];

    public HelpdeskFollowupController(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        var manager =
            Has("helpdesk.manage") ||
            Has("settings.manage");

        if (!manager &&
            !Has("tickets.comment") &&
            !Has("tickets.assign"))
        {
            return Forbid();
        }

        if (all && !manager)
            return Forbid();

        var organizationValue =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        var userValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("user_id") ??
            User.FindFirstValue("userId");

        if (!Guid.TryParse(
                organizationValue,
                out var organizationId) ||
            !Guid.TryParse(
                userValue,
                out var userId))
        {
            return Unauthorized();
        }

        var activeUser = await _db.Users
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == userId &&
                    x.OrganizationId == organizationId &&
                    x.IsActive,
                cancellationToken);

        if (!activeUser)
            return Unauthorized();

        var now = DateTime.UtcNow;
        var firstWarning = now.AddMinutes(30);
        var resolveWarning = now.AddHours(1);

        var active = _db.HelpdeskTickets
            .AsNoTracking()
            .Where(
                x =>
                    x.OrganizationId == organizationId &&
                    x.Status != "resolved" &&
                    x.Status != "closed");

        if (!all)
        {
            active = active.Where(
                x => x.AssigneeUserId == userId);
        }

        var activeCount =
            await active.CountAsync(cancellationToken);

        var unassigned = await active.CountAsync(
            x => x.AssigneeUserId == null,
            cancellationToken);

        var waitingUser = await active.CountAsync(
            x => x.Status == "pendinguser",
            cancellationToken);

        var firstOverdue = await active.CountAsync(
            x =>
                x.FirstRespondedAtUtc == null &&
                x.FirstResponseDueAtUtc != null &&
                x.FirstResponseDueAtUtc <= now,
            cancellationToken);

        var resolutionOverdue = await active.CountAsync(
            x =>
                x.ResolvedAtUtc == null &&
                x.ResolveDueAtUtc != null &&
                x.ResolveDueAtUtc <= now,
            cancellationToken);

        var approaching = await active.CountAsync(
            x =>
                (
                    x.FirstRespondedAtUtc == null &&
                    x.FirstResponseDueAtUtc > now &&
                    x.FirstResponseDueAtUtc <= firstWarning
                ) ||
                (
                    x.ResolvedAtUtc == null &&
                    x.ResolveDueAtUtc > now &&
                    x.ResolveDueAtUtc <= resolveWarning
                ),
            cancellationToken);

        var needingAttention = active.Where(
            x =>
                x.AssigneeUserId == null ||
                (
                    x.FirstRespondedAtUtc == null &&
                    x.FirstResponseDueAtUtc != null &&
                    x.FirstResponseDueAtUtc <= firstWarning
                ) ||
                (
                    x.ResolvedAtUtc == null &&
                    x.ResolveDueAtUtc != null &&
                    x.ResolveDueAtUtc <= resolveWarning
                ));

        var attentionTotal =
            await needingAttention.CountAsync(
                cancellationToken);

        var tickets = await needingAttention
            .OrderBy(
                x =>
                    x.ResolveDueAtUtc ??
                    x.FirstResponseDueAtUtc ??
                    x.CreatedAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(100)
            .Select(
                x => new
                {
                    x.Id,
                    x.Number,
                    x.Subject,
                    x.Status,
                    x.Priority,
                    x.Category,
                    x.AssigneeUserId,
                    x.CreatedAtUtc,
                    x.FirstResponseDueAtUtc,
                    x.FirstRespondedAtUtc,
                    x.ResolveDueAtUtc,
                    x.ResolvedAtUtc
                })
            .ToListAsync(cancellationToken);

        var recentSince = now.AddDays(-1);

        var alerts = await (
            from activity in
                _db.HelpdeskTicketEvents.AsNoTracking()
            join ticket in active
                on activity.TicketId equals ticket.Id
            where
                activity.OrganizationId == organizationId &&
                AlertTypes.Contains(activity.EventType) &&
                activity.CreatedAtUtc >= recentSince
            orderby activity.CreatedAtUtc descending
            select new
            {
                activity.Id,
                activity.TicketId,
                ticket.Number,
                ticket.Subject,
                activity.EventType,
                activity.Summary,
                activity.CreatedAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(
            new
            {
                scope = all ? "organization" : "mine",
                canViewAll = manager,
                generatedAtUtc = now,

                summary = new
                {
                    active = activeCount,
                    unassigned,
                    waitingUser,
                    firstOverdue,
                    resolutionOverdue,
                    approaching
                },

                attentionTotal,
                tickets,
                alerts
            });
    }

    private bool Has(string permission)
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