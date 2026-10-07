using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/workload")]
public sealed class HelpdeskWorkloadController
    : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskWorkloadController(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // WORKLOAD SUMMARY
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        if (!CanView())
            return Forbid();

        if (!TryIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var active =
            _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Status !=
                            "resolved"
                        &&
                        x.Status !=
                            "closed");

        var counts =
            await active
                .Where(
                    x =>
                        x.AssigneeUserId
                            .HasValue)
                .GroupBy(
                    x =>
                        x.AssigneeUserId!
                            .Value)
                .Select(
                    x =>
                        new
                        {
                            UserId =
                                x.Key,

                            OpenTickets =
                                x.Count()
                        })
                .ToListAsync(
                    cancellationToken);

        var countByUser =
            counts.ToDictionary(
                x =>
                    x.UserId,
                x =>
                    x.OpenTickets);

        var members =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .Select(
                    x =>
                        new
                        {
                            x.UserId,
                            x.IsAvailable,
                            x.AcceptsAutomaticAssignments,
                            x.MaxOpenTickets
                        })
                .ToListAsync(
                    cancellationToken);

        var memberIds =
            members
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToArray();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        memberIds.Contains(
                            x.Id))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName
                        })
                .ToListAsync(
                    cancellationToken);

        var agents =
            users
                .Select(
                    user =>
                    {
                        var usable =
                            members
                                .Where(
                                    x =>
                                        x.UserId ==
                                            user.Id
                                        &&
                                        x.IsAvailable
                                        &&
                                        x.AcceptsAutomaticAssignments
                                        &&
                                        x.MaxOpenTickets >
                                            0)
                                .ToArray();

                        return new
                        {
                            userId =
                                user.Id,

                            name =
                                (
                                    user.FirstName +
                                    " " +
                                    user.LastName
                                )
                                .Trim(),

                            openTickets =
                                countByUser
                                    .GetValueOrDefault(
                                        user.Id),

                            isAvailable =
                                usable.Length >
                                    0,

                            capacity =
                                usable.Length ==
                                    0
                                    ? 0
                                    : usable.Max(
                                        x =>
                                            x.MaxOpenTickets)
                        };
                    })
                .OrderByDescending(
                    x =>
                        x.openTickets)
                .ThenBy(
                    x =>
                        x.name)
                .ToArray();

        return Ok(
            new
            {
                assignedToMe =
                    await active.CountAsync(
                        x =>
                            x.AssigneeUserId ==
                                userId,
                        cancellationToken),

                unassigned =
                    await active.CountAsync(
                        x =>
                            x.AssigneeUserId ==
                                null,
                        cancellationToken),

                active =
                    await active.CountAsync(
                        cancellationToken),

                agents,

                generatedAtUtc =
                    DateTime.UtcNow
            });
    }

    // ============================================================
    // TICKETS
    // ============================================================

    [HttpGet("tickets")]
    public async Task<IActionResult> GetWorkTickets(
        [FromQuery]
        string view = "mine",

        [FromQuery]
        string? search = null,

        [FromQuery]
        string? status = null,

        [FromQuery]
        string? priority = null,

        [FromQuery]
        int page = 1,

        [FromQuery]
        int pageSize = 25,

        CancellationToken cancellationToken = default)
    {
        if (!CanView())
            return Forbid();

        if (!TryIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        view =
            view
                .Trim()
                .ToLowerInvariant();

        /*
         * Kanban is now a first-class workload view.
         */
        if (view is not (
                "mine"
                or "unassigned"
                or "all"
                or "kanban"))
        {
            return BadRequest(
                new
                {
                    message =
                        "Vista de tickets no válida."
                });
        }

        page =
            Math.Max(
                1,
                page);

        pageSize =
            Math.Clamp(
                pageSize,
                1,
                100);

        var query =
            _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId);

        switch (view)
        {
            case "mine":
                query =
                    query.Where(
                        x =>
                            x.AssigneeUserId ==
                                userId
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed");
                break;

            case "unassigned":
                query =
                    query.Where(
                        x =>
                            x.AssigneeUserId ==
                                null
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed");
                break;

            case "kanban":
                /*
                 * Kanban is operational work only.
                 * Resolved/closed belong to historical reporting.
                 */
                query =
                    query.Where(
                        x =>
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed");
                break;

            case "all":
                /*
                 * "All" intentionally includes historical tickets.
                 */
                break;
        }

        // ========================================================
        // FILTERS
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            var term =
                search.Trim();

            query =
                query.Where(
                    x =>
                        x.Number.Contains(
                            term)
                        ||
                        x.Subject.Contains(
                            term)
                        ||
                        x.Category.Contains(
                            term));
        }

        if (!string.IsNullOrWhiteSpace(
                status))
        {
            var selectedStatus =
                status
                    .Trim()
                    .ToLowerInvariant();

            query =
                query.Where(
                    x =>
                        x.Status ==
                            selectedStatus);
        }

        if (!string.IsNullOrWhiteSpace(
                priority))
        {
            var selectedPriority =
                priority
                    .Trim()
                    .ToLowerInvariant();

            /*
             * Compatibility while old tickets may still use urgent
             * and the current SLA model uses critical.
             */
            if (selectedPriority ==
                "urgent")
            {
                query =
                    query.Where(
                        x =>
                            x.Priority ==
                                "urgent"
                            ||
                            x.Priority ==
                                "critical");
            }
            else if (
                selectedPriority ==
                "critical")
            {
                query =
                    query.Where(
                        x =>
                            x.Priority ==
                                "critical"
                            ||
                            x.Priority ==
                                "urgent");
            }
            else
            {
                query =
                    query.Where(
                        x =>
                            x.Priority ==
                                selectedPriority);
            }
        }

        var total =
            await query.CountAsync(
                cancellationToken);

        var rows =
            await query
                .OrderByDescending(
                    x =>
                        x.UpdatedAtUtc)
                .ThenByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Number,
                            x.Subject,
                            x.Status,
                            x.Priority,
                            x.Category,
                            x.RequesterUserId,
                            x.AssigneeUserId,
                            x.CreatedAtUtc,
                            x.UpdatedAtUtc,
                            x.FirstResponseDueAtUtc,
                            x.FirstRespondedAtUtc,
                            x.ResolveDueAtUtc,
                            x.ResolvedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var userIds =
            rows
                .Select(
                    x =>
                        x.RequesterUserId)
                .Concat(
                    rows
                        .Where(
                            x =>
                                x.AssigneeUserId
                                    .HasValue)
                        .Select(
                            x =>
                                x.AssigneeUserId!
                                    .Value))
                .Distinct()
                .ToArray();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        userIds.Contains(
                            x.Id))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName
                        })
                .ToDictionaryAsync(
                    x =>
                        x.Id,

                    x =>
                        (
                            x.FirstName +
                            " " +
                            x.LastName
                        )
                        .Trim(),

                    cancellationToken);

        var now =
            DateTime.UtcNow;

        var items =
            rows.Select(
                x =>
                    new
                    {
                        x.Id,
                        x.Number,
                        x.Subject,
                        x.Status,
                        x.Priority,
                        x.Category,

                        x.RequesterUserId,

                        requesterName =
                            users.GetValueOrDefault(
                                x.RequesterUserId,
                                "Usuario Titan"),

                        x.AssigneeUserId,

                        assigneeName =
                            x.AssigneeUserId
                                .HasValue
                                ? users.GetValueOrDefault(
                                    x.AssigneeUserId
                                        .Value)
                                : null,

                        x.CreatedAtUtc,
                        x.UpdatedAtUtc,

                        slaBreached =
                            (
                                x.FirstResponseDueAtUtc
                                    .HasValue
                                &&
                                !x.FirstRespondedAtUtc
                                    .HasValue
                                &&
                                x.FirstResponseDueAtUtc
                                    .Value <
                                    now
                            )
                            ||
                            (
                                x.ResolveDueAtUtc
                                    .HasValue
                                &&
                                !x.ResolvedAtUtc
                                    .HasValue
                                &&
                                x.ResolveDueAtUtc
                                    .Value <
                                    now
                            )
                    });

        return Ok(
            new
            {
                items,
                total,
                page,
                pageSize
            });
    }

    // ============================================================
    // RBAC
    // ============================================================

    private bool CanView()
    {
        return HasAnyPermission(
            "helpdesk.agent.access",
            "helpdesk.admin.access",

            "helpdesk.inbox.my-work",
            "helpdesk.inbox.unassigned",
            "helpdesk.inbox.all",
            "helpdesk.kanban.view",

            "helpdesk.ticket.details.view",

            // Legacy
            "helpdesk.view",
            "tickets.view",
            "helpdesk.manage");
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
                            StringComparison
                                .OrdinalIgnoreCase)));
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private bool TryIdentity(
        out Guid organizationId,
        out Guid userId)
    {
        var organizationClaim =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub")
            ??
            User.FindFirstValue(
                "user_id")
            ??
            User.FindFirstValue(
                "userId");

        organizationId =
            Guid.Empty;

        userId =
            Guid.Empty;

        return Guid.TryParse(
                   organizationClaim,
                   out organizationId)
               &&
               Guid.TryParse(
                   userClaim,
                   out userId);
    }
}