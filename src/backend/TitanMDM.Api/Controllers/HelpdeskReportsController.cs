using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/reports")]
public sealed class HelpdeskReportsController
    : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskReportsController(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // ENTERPRISE KPI SUMMARY
    // ============================================================

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!CanViewReports())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (!TryGetRange(
                from,
                to,
                out var start,
                out var end))
        {
            return BadRequest(
                new
                {
                    message =
                        "El rango debe ser válido y no superar 366 días."
                });
        }

        var now =
            DateTime.UtcNow;

        // ========================================================
        // TICKETS CREATED INSIDE SELECTED PERIOD
        // ========================================================

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.CreatedAtUtc >=
                            start
                        &&
                        x.CreatedAtUtc <
                            end)
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
                            x.Source,
                            x.AssigneeUserId,
                            x.SiteId,
                            x.CreatedAtUtc,
                            x.UpdatedAtUtc,
                            x.FirstRespondedAtUtc,
                            x.FirstResponseDueAtUtc,
                            x.ResolvedAtUtc,
                            x.ResolveDueAtUtc,
                            x.TotalSlaPausedSeconds,
                            x.FirstResponsePausedSeconds
                        })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // CURRENT GLOBAL BACKLOG
        //
        // This intentionally includes active tickets created
        // before the selected reporting period.
        // ========================================================

        var activeBacklog =
            await _db.HelpdeskTickets
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
                            "closed")
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Status,
                            x.Priority,
                            x.AssigneeUserId,
                            x.CreatedAtUtc,
                            x.FirstRespondedAtUtc,
                            x.FirstResponseDueAtUtc,
                            x.ResolveDueAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // NAMES
        // ========================================================

        var assigneeIds =
            tickets
                .Where(
                    x =>
                        x.AssigneeUserId
                            .HasValue)
                .Select(
                    x =>
                        x.AssigneeUserId!
                            .Value)
                .Distinct()
                .ToArray();

        var agentNames =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        assigneeIds.Contains(
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

        var siteIds =
            tickets
                .Where(
                    x =>
                        x.SiteId
                            .HasValue)
                .Select(
                    x =>
                        x.SiteId!
                            .Value)
                .Distinct()
                .ToArray();

        var siteNames =
            await _db.Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        siteIds.Contains(
                            x.Id))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Name,
                            x.Code
                        })
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    x =>
                        string.IsNullOrWhiteSpace(
                            x.Code)
                            ? x.Name
                            : $"{x.Name} ({x.Code})",
                    cancellationToken);

        // ========================================================
        // FIRST RESPONSE TIMES
        // ========================================================

        var firstResponseTimes =
            tickets
                .Where(
                    x =>
                        x.FirstRespondedAtUtc
                            .HasValue)
                .Select(
                    x =>
                        Math.Max(
                            0,
                            (
                                x.FirstRespondedAtUtc!
                                    .Value
                                -
                                x.CreatedAtUtc
                            )
                            .TotalHours
                            -
                            (
                                x.FirstResponsePausedSeconds /
                                3600d
                            )))
                .ToArray();

        // ========================================================
        // RESOLUTION TIMES
        // ========================================================

        var resolutionTimes =
            tickets
                .Where(
                    x =>
                        x.ResolvedAtUtc
                            .HasValue)
                .Select(
                    x =>
                        Math.Max(
                            0,
                            (
                                x.ResolvedAtUtc!
                                    .Value
                                -
                                x.CreatedAtUtc
                            )
                            .TotalHours
                            -
                            (
                                x.TotalSlaPausedSeconds /
                                3600d
                            )))
                .ToArray();

        // ========================================================
        // FIRST RESPONSE SLA
        // ========================================================

        var firstResponseEvaluated =
            tickets
                .Where(
                    x =>
                        x.FirstResponseDueAtUtc
                            .HasValue
                        &&
                        (
                            x.FirstRespondedAtUtc
                                .HasValue
                            ||
                            x.FirstResponseDueAtUtc <
                                now
                        ))
                .ToArray();

        var firstResponseMet =
            firstResponseEvaluated
                .Count(
                    x =>
                        x.FirstRespondedAtUtc
                            .HasValue
                        &&
                        x.FirstRespondedAtUtc <=
                            x.FirstResponseDueAtUtc);

        var firstResponseBreached =
            firstResponseEvaluated.Length -
            firstResponseMet;

        var firstResponseCompliance =
            Percentage(
                firstResponseMet,
                firstResponseEvaluated.Length);

        // ========================================================
        // RESOLUTION SLA
        // ========================================================

        var resolutionEvaluated =
            tickets
                .Where(
                    x =>
                        x.ResolveDueAtUtc
                            .HasValue
                        &&
                        (
                            x.ResolvedAtUtc
                                .HasValue
                            ||
                            x.ResolveDueAtUtc <
                                now
                        ))
                .ToArray();

        var resolutionMet =
            resolutionEvaluated
                .Count(
                    x =>
                        x.ResolvedAtUtc
                            .HasValue
                        &&
                        x.ResolvedAtUtc <=
                            x.ResolveDueAtUtc);

        var resolutionBreached =
            resolutionEvaluated.Length -
            resolutionMet;

        var resolutionCompliance =
            Percentage(
                resolutionMet,
                resolutionEvaluated.Length);

        // ========================================================
        // BASIC TOTALS
        // ========================================================

        var resolved =
            tickets.Count(
                x =>
                    x.Status
                        is "resolved"
                        or "closed");

        var unresolved =
            tickets.Count -
            resolved;

        var unassigned =
            tickets.Count(
                x =>
                    !x.AssigneeUserId
                        .HasValue
                    &&
                    x.Status
                        is not (
                            "resolved"
                            or
                            "closed"));

        var pendingUser =
            tickets.Count(
                x =>
                    x.Status ==
                        "pendinguser");

        var overdueFirstResponse =
            activeBacklog.Count(
                x =>
                    !x.FirstRespondedAtUtc
                        .HasValue
                    &&
                    x.FirstResponseDueAtUtc
                        .HasValue
                    &&
                    x.FirstResponseDueAtUtc <
                        now);

        var overdueResolution =
            activeBacklog.Count(
                x =>
                    x.ResolveDueAtUtc
                        .HasValue
                    &&
                    x.ResolveDueAtUtc <
                        now);

        // ========================================================
        // STATUS
        // ========================================================

        var byStatus =
            tickets
                .GroupBy(
                    x =>
                        x.Status)
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key,

                            count =
                                group.Count()
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .ToArray();

        // ========================================================
        // PRIORITY
        // ========================================================

        var byPriority =
            tickets
                .GroupBy(
                    x =>
                        x.Priority)
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key,

                            count =
                                group.Count()
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .ToArray();

        // ========================================================
        // CATEGORY
        // ========================================================

        var byCategory =
            tickets
                .GroupBy(
                    x =>
                        x.Category)
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key,

                            count =
                                group.Count()
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .Take(
                    15)
                .ToArray();

        // ========================================================
        // SOURCE
        // ========================================================

        var bySource =
            tickets
                .GroupBy(
                    x =>
                        x.Source)
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key,

                            count =
                                group.Count()
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .ToArray();

        // ========================================================
        // SITE
        // ========================================================

        var bySite =
            tickets
                .GroupBy(
                    x =>
                        x.SiteId)
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key.HasValue
                                &&
                                siteNames.TryGetValue(
                                    group.Key.Value,
                                    out var siteName)
                                    ? siteName
                                    : "Sin localidad",

                            count =
                                group.Count(),

                            resolved =
                                group.Count(
                                    ticket =>
                                        ticket.Status
                                            is "resolved"
                                            or "closed")
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .Take(
                    20)
                .ToArray();

        // ========================================================
        // AGENT PERFORMANCE
        // ========================================================

        var byAgent =
            tickets
                .GroupBy(
                    x =>
                        x.AssigneeUserId)
                .Select(
                    group =>
                    {
                        var assigned =
                            group.Count();

                        var agentResolved =
                            group.Count(
                                ticket =>
                                    ticket.Status
                                        is "resolved"
                                        or "closed");

                        var active =
                            assigned -
                            agentResolved;

                        var slaBreached =
                            group.Count(
                                ticket =>
                                    (
                                        ticket.FirstRespondedAtUtc
                                            .HasValue
                                        &&
                                        ticket.FirstResponseDueAtUtc
                                            .HasValue
                                        &&
                                        ticket.FirstRespondedAtUtc >
                                            ticket.FirstResponseDueAtUtc
                                    )
                                    ||
                                    (
                                        ticket.ResolvedAtUtc
                                            .HasValue
                                        &&
                                        ticket.ResolveDueAtUtc
                                            .HasValue
                                        &&
                                        ticket.ResolvedAtUtc >
                                            ticket.ResolveDueAtUtc
                                    ));

                        return new
                        {
                            label =
                                group.Key.HasValue
                                &&
                                agentNames.TryGetValue(
                                    group.Key.Value,
                                    out var agentName)
                                    ? agentName
                                    : "Sin asignar",

                            count =
                                assigned,

                            resolved =
                                agentResolved,

                            active,

                            resolutionRate =
                                Percentage(
                                    agentResolved,
                                    assigned),

                            slaBreached
                        };
                    })
                .OrderByDescending(
                    x =>
                        x.count)
                .Take(
                    20)
                .ToArray();

        // ========================================================
        // DAILY TREND
        // ========================================================

        var rangeDays =
            Enumerable
                .Range(
                    0,
                    (
                        DateOnly.FromDateTime(
                            end.AddDays(-1))
                            .DayNumber
                        -
                        DateOnly.FromDateTime(
                            start)
                            .DayNumber
                    )
                    +
                    1)
                .Select(
                    index =>
                        DateOnly.FromDateTime(
                            start)
                            .AddDays(
                                index))
                .ToArray();

        var createdByDay =
            tickets
                .GroupBy(
                    x =>
                        DateOnly.FromDateTime(
                            x.CreatedAtUtc))
                .ToDictionary(
                    x =>
                        x.Key,
                    x =>
                        x.Count());

        var resolvedByDay =
            tickets
                .Where(
                    x =>
                        x.ResolvedAtUtc
                            .HasValue)
                .GroupBy(
                    x =>
                        DateOnly.FromDateTime(
                            x.ResolvedAtUtc!
                                .Value))
                .ToDictionary(
                    x =>
                        x.Key,
                    x =>
                        x.Count());

        var daily =
            rangeDays
                .Select(
                    day =>
                        new
                        {
                            date =
                                day.ToString(
                                    "yyyy-MM-dd"),

                            created =
                                createdByDay
                                    .GetValueOrDefault(
                                        day),

                            resolved =
                                resolvedByDay
                                    .GetValueOrDefault(
                                        day)
                        })
                .ToArray();

        // ========================================================
        // CURRENT BACKLOG AGING
        // ========================================================

        var aging =
            activeBacklog
                .Select(
                    x =>
                        Math.Max(
                            0,
                            (
                                now -
                                x.CreatedAtUtc
                            )
                            .TotalHours))
                .ToArray();

        var backlogAging =
            new[]
            {
                new
                {
                    label =
                        "0–4 h",

                    count =
                        aging.Count(
                            value =>
                                value <
                                4)
                },

                new
                {
                    label =
                        "4–8 h",

                    count =
                        aging.Count(
                            value =>
                                value >=
                                    4
                                &&
                                value <
                                    8)
                },

                new
                {
                    label =
                        "8–24 h",

                    count =
                        aging.Count(
                            value =>
                                value >=
                                    8
                                &&
                                value <
                                    24)
                },

                new
                {
                    label =
                        "1–3 días",

                    count =
                        aging.Count(
                            value =>
                                value >=
                                    24
                                &&
                                value <
                                    72)
                },

                new
                {
                    label =
                        "3–7 días",

                    count =
                        aging.Count(
                            value =>
                                value >=
                                    72
                                &&
                                value <
                                    168)
                },

                new
                {
                    label =
                        "7+ días",

                    count =
                        aging.Count(
                            value =>
                                value >=
                                    168)
                }
            };

        // ========================================================
        // AUTOMATION EVENTS — E9
        // ========================================================

        var automationEvents =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.CreatedAtUtc >=
                            start
                        &&
                        x.CreatedAtUtc <
                            end)
                .GroupBy(
                    x =>
                        x.EventType)
                .Select(
                    group =>
                        new
                        {
                            type =
                                group.Key,

                            count =
                                group.Count()
                        })
                .ToListAsync(
                    cancellationToken);

        int EventCount(
            params string[] names)
        {
            return automationEvents
                .Where(
                    x =>
                        names.Contains(
                            x.type,
                            StringComparer
                                .OrdinalIgnoreCase))
                .Sum(
                    x =>
                        x.count);
        }

        var automation =
            new
            {
                autoAssigned =
                    EventCount(
                        "auto_assigned"),

                autoHandovers =
                    EventCount(
                        "auto_handover"),

                classifications =
                    EventCount(
                        "auto_classified"),

                classificationReviews =
                    EventCount(
                        "classification_review"),

                escalations =
                    EventCount(
                        "sla_escalated"),

                reminders =
                    EventCount(
                        "unassigned_reminder",
                        "assigned_attention_reminder",
                        "inactivity_reminder",
                        "first_response_warning",
                        "resolution_warning"),

                overdueAlerts =
                    EventCount(
                        "first_response_overdue",
                        "resolution_overdue"),

                reopened =
                    EventCount(
                        "reopened")
            };

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(
            new
            {
                from =
                    DateOnly.FromDateTime(
                        start),

                to =
                    DateOnly.FromDateTime(
                        end.AddDays(-1)),

                total =
                    tickets.Count,

                resolved,

                unresolved,

                currentBacklog =
                    activeBacklog.Count,

                unassigned,

                pendingUser,

                overdueFirstResponse,

                overdueResolution,

                averageFirstResponseHours =
                    Average(
                        firstResponseTimes),

                medianFirstResponseHours =
                    Median(
                        firstResponseTimes),

                averageResolutionHours =
                    Average(
                        resolutionTimes),

                medianResolutionHours =
                    Median(
                        resolutionTimes),

                firstResponseSla =
                    new
                    {
                        evaluated =
                            firstResponseEvaluated
                                .Length,

                        met =
                            firstResponseMet,

                        breached =
                            firstResponseBreached,

                        compliancePercent =
                            firstResponseCompliance
                    },

                resolutionSla =
                    new
                    {
                        evaluated =
                            resolutionEvaluated
                                .Length,

                        met =
                            resolutionMet,

                        breached =
                            resolutionBreached,

                        compliancePercent =
                            resolutionCompliance
                    },

                byStatus,

                byPriority,

                byCategory,

                bySource,

                bySite,

                byAgent,

                backlogAging,

                daily,

                automation,

                generatedAtUtc =
                    now
            });
    }

    // ============================================================
    // CSV EXPORT
    // ============================================================

    [HttpGet("tickets.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!CanViewReports())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (!TryGetRange(
                from,
                to,
                out var start,
                out var end))
        {
            return BadRequest(
                new
                {
                    message =
                        "El rango debe ser válido y no superar 366 días."
                });
        }

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.CreatedAtUtc >=
                            start
                        &&
                        x.CreatedAtUtc <
                            end)
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .Select(
                    x =>
                        new
                        {
                            x.Number,
                            x.Subject,
                            x.Status,
                            x.Priority,
                            x.Category,
                            x.Source,
                            x.AssigneeUserId,
                            x.ExternalRequesterEmail,
                            x.CreatedAtUtc,
                            x.FirstRespondedAtUtc,
                            x.FirstResponseDueAtUtc,
                            x.ResolvedAtUtc,
                            x.ResolveDueAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var assigneeIds =
            tickets
                .Where(
                    x =>
                        x.AssigneeUserId
                            .HasValue)
                .Select(
                    x =>
                        x.AssigneeUserId!
                            .Value)
                .Distinct()
                .ToArray();

        var names =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        assigneeIds.Contains(
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

        var csv =
            new StringBuilder();

        csv.AppendLine(
            "Numero,Asunto,Estado,Prioridad,Categoria,Origen," +
            "Agente,Correo externo,Creado UTC," +
            "Primera respuesta UTC,SLA primera respuesta UTC," +
            "Resuelto UTC,SLA resolucion UTC");

        foreach (var ticket in tickets)
        {
            var agent =
                ticket.AssigneeUserId
                    .HasValue
                &&
                names.TryGetValue(
                    ticket.AssigneeUserId.Value,
                    out var name)
                    ? name
                    : "";

            csv.AppendLine(
                string.Join(
                    ",",
                    new[]
                    {
                        Cell(
                            ticket.Number),

                        Cell(
                            ticket.Subject),

                        Cell(
                            ticket.Status),

                        Cell(
                            ticket.Priority),

                        Cell(
                            ticket.Category),

                        Cell(
                            ticket.Source),

                        Cell(
                            agent),

                        Cell(
                            ticket.ExternalRequesterEmail),

                        Cell(
                            ticket.CreatedAtUtc
                                .ToString(
                                    "O")),

                        Cell(
                            ticket.FirstRespondedAtUtc?
                                .ToString(
                                    "O")),

                        Cell(
                            ticket.FirstResponseDueAtUtc?
                                .ToString(
                                    "O")),

                        Cell(
                            ticket.ResolvedAtUtc?
                                .ToString(
                                    "O")),

                        Cell(
                            ticket.ResolveDueAtUtc?
                                .ToString(
                                    "O"))
                    }));
        }

        var bytes =
            new UTF8Encoding(
                true)
                .GetPreamble()
                .Concat(
                    Encoding.UTF8
                        .GetBytes(
                            csv.ToString()))
                .ToArray();

        return File(
            bytes,
            "text/csv; charset=utf-8",
            $"titanmdm-helpdesk-{start:yyyyMMdd}-" +
            $"{end.AddDays(-1):yyyyMMdd}.csv");
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static double? Average(
        IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        return Math.Round(
            values.Average(),
            2);
    }

    private static double? Median(
        IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var ordered =
            values
                .OrderBy(
                    value =>
                        value)
                .ToArray();

        var middle =
            ordered.Length /
            2;

        var value =
            ordered.Length %
            2 ==
            0
                ? (
                    ordered[middle - 1]
                    +
                    ordered[middle]
                )
                /
                2d
                : ordered[middle];

        return Math.Round(
            value,
            2);
    }

    private static double Percentage(
        int value,
        int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Round(
            value *
            100d /
            total,
            1);
    }

    private bool CanViewReports()
    {
        return User.Claims
            .Any(
                claim =>
                    claim.Type ==
                        "permission"
                    &&
                    (
                        string.Equals(
                            claim.Value,
                            "helpdesk.view",
                            StringComparison
                                .OrdinalIgnoreCase)
                        ||
                        string.Equals(
                            claim.Value,
                            "tickets.view",
                            StringComparison
                                .OrdinalIgnoreCase)
                    ));
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

    private static bool TryGetRange(
        DateOnly? from,
        DateOnly? to,
        out DateTime start,
        out DateTime end)
    {
        var today =
            DateOnly.FromDateTime(
                DateTime.UtcNow);

        var first =
            from
            ??
            today.AddDays(
                -29);

        var last =
            to
            ??
            today;

        start =
            DateTime.SpecifyKind(
                first.ToDateTime(
                    TimeOnly.MinValue),
                DateTimeKind.Utc);

        end =
            DateTime.SpecifyKind(
                last
                    .AddDays(
                        1)
                    .ToDateTime(
                        TimeOnly.MinValue),
                DateTimeKind.Utc);

        return last >=
                   first
               &&
               last <=
                   today
               &&
               last.DayNumber -
               first.DayNumber <=
                   365;
    }

    private static string Cell(
        string? value)
    {
        var text =
            (
                value ??
                ""
            )
            .Replace(
                "\r",
                " ")
            .Replace(
                "\n",
                " ");

        if (
            text.Length >
            0
            &&
            "=+-@\t".Contains(
                text[0]))
        {
            text =
                "'" +
                text;
        }

        return "\"" +
               text.Replace(
                   "\"",
                   "\"\"") +
               "\"";
    }
}