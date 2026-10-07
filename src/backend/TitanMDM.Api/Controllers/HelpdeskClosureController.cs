using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/closure")]
public sealed class HelpdeskClosureController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    private readonly IConfiguration
        _configuration;

    public HelpdeskClosureController(
        TitanMdmDbContext db,
        IConfiguration configuration)
    {
        _db =
            db;

        _configuration =
            configuration;
    }

    private bool Has(
        string code)
    {
        return User.Claims.Any(
            x =>
                x.Type ==
                    "permission"
                &&
                string.Equals(
                    x.Value,
                    code,
                    StringComparison.OrdinalIgnoreCase));
    }

    private bool Manager =>
        Has("helpdesk.admin.access")
        ||
        Has("helpdesk.sla.manage")
        ||
        Has("helpdesk.manage")
        ||
        Has("settings.manage");

    private bool CanViewAnalytics =>
        Manager
        ||
        Has("helpdesk.sla.view")
        ||
        Has("helpdesk.kpi.view")
        ||
        Has("helpdesk.analytics.view")
        ||
        Has("helpdesk.view");

    private async Task<Guid?>
        Organization(
            CancellationToken cancellationToken)
    {
        var organizationClaim =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var actorClaim =
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

        if (!Guid.TryParse(
                organizationClaim,
                out var organizationId)
            ||
            !Guid.TryParse(
                actorClaim,
                out var userId))
        {
            return null;
        }

        var valid =
            await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        return valid
            ? organizationId
            : null;
    }

    // ============================================================
    // SETTINGS
    // ============================================================

    [HttpGet("settings")]
    public async Task<IActionResult>
        Settings(
            CancellationToken cancellationToken)
    {
        if (!Manager)
        {
            return Forbid();
        }

        var organizationId =
            await Organization(
                cancellationToken);

        if (organizationId is null)
        {
            return Unauthorized();
        }

        var settings =
            await _db
                .Set<HelpdeskAutomationSettings>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken)
            ??
            new HelpdeskAutomationSettings(
                organizationId.Value);

        return Ok(
            new
            {
                settings.ClassificationEnabled,

                settings.EscalationDelayMinutes,

                settings.ReopenDays,

                settings.LowFirstResponseMinutes,
                settings.LowResolutionMinutes,

                settings.MediumFirstResponseMinutes,
                settings.MediumResolutionMinutes,

                settings.HighFirstResponseMinutes,
                settings.HighResolutionMinutes,

                settings.CriticalFirstResponseMinutes,
                settings.CriticalResolutionMinutes,

                settings.PauseSlaWhenWaitingUser,

                settings.Revision,

                settings.UpdatedAtUtc,

                modelConfigured =
                    _configuration
                        .GetValue<bool>(
                            "HelpdeskAssistant:Enabled")
                    &&
                    !string.IsNullOrWhiteSpace(
                        _configuration[
                            "HelpdeskAssistant:Model"])
            });
    }

    [HttpPut("settings")]
    public async Task<IActionResult>
        SaveSettings(
            [FromBody]
            SettingsRequest request,
            CancellationToken cancellationToken)
    {
        if (!Manager)
        {
            return Forbid();
        }

        var organizationId =
            await Organization(
                cancellationToken);

        if (organizationId is null)
        {
            return Unauthorized();
        }

        var item =
            await _db
                .Set<HelpdeskAutomationSettings>()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken);

        if ((item?.Revision ?? 0) !=
            request.Revision)
        {
            return Conflict(
                new
                {
                    message =
                        "La configuración cambió. Actualiza la pantalla antes de guardar."
                });
        }

        if (item is null)
        {
            item =
                new HelpdeskAutomationSettings(
                    organizationId.Value);

            _db.Add(
                item);
        }

        try
        {
            item.Configure(
                request.ClassificationEnabled,
                request.EscalationDelayMinutes,
                request.ReopenDays,
                request.LowFirstResponseMinutes,
                request.LowResolutionMinutes,
                request.MediumFirstResponseMinutes,
                request.MediumResolutionMinutes,
                request.HighFirstResponseMinutes,
                request.HighResolutionMinutes,
                request.CriticalFirstResponseMinutes,
                request.CriticalResolutionMinutes,
                request.PauseSlaWhenWaitingUser);

            await _db.SaveChangesAsync(
                cancellationToken);

            return await Settings(
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(
                new
                {
                    message =
                        "La configuración fue modificada por otro administrador. Actualiza la pantalla."
                });
        }
        catch (DbUpdateException)
        {
            return Conflict(
                new
                {
                    message =
                        "No se pudo guardar la configuración."
                });
        }
    }

    // ============================================================
    // READINESS
    // ============================================================

    [HttpGet("readiness")]
    public async Task<IActionResult>
        Readiness(
            CancellationToken cancellationToken)
    {
        if (!Manager)
        {
            return Forbid();
        }

        var organizationId =
            await Organization(
                cancellationToken);

        if (organizationId is null)
        {
            return Unauthorized();
        }

        return Ok(
            new
            {
                zones =
                    await _db.HelpdeskZones
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsActive,
                            cancellationToken),

                groups =
                    await _db.HelpdeskTeams
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsActive,
                            cancellationToken),

                availableMemberships =
                    await _db.HelpdeskTeamMembers
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsAvailable
                                &&
                                x.AcceptsAutomaticAssignments,
                            cancellationToken),

                activeSchedules =
                    await _db
                        .Set<HelpdeskTechnicianSchedule>()
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsEnabled,
                            cancellationToken),

                activeTemplates =
                    await _db
                        .Set<HelpdeskRequestTemplate>()
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsActive,
                            cancellationToken),

                unlocatedUsers =
                    await _db.Users
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsActive
                                &&
                                !x.SiteId.HasValue,
                            cancellationToken),

                assistantUsers =
                    await _db.HelpdeskAssistantAccess
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.IsEnabled,
                            cancellationToken)
            });
    }

    // ============================================================
    // ANALYTICS
    // ============================================================

    [HttpGet("analytics")]
    public async Task<IActionResult>
        Analytics(
            [FromQuery]
            DateTime? from,
            [FromQuery]
            DateTime? to,
            CancellationToken cancellationToken)
    {
        if (!CanViewAnalytics)
        {
            return Forbid();
        }

        if (from.HasValue &&
            to.HasValue &&
            from.Value.Date >
                to.Value.Date)
        {
            return BadRequest(
                new
                {
                    message =
                        "El inicio debe ser anterior al fin."
                });
        }

        if (to.HasValue &&
            to.Value.Date >=
                DateTime.MaxValue.Date)
        {
            return BadRequest();
        }

        var organizationId =
            await Organization(
                cancellationToken);

        if (organizationId is null)
        {
            return Unauthorized();
        }

        var query =
            _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value);

        if (from.HasValue)
        {
            var start =
                from.Value.Date;

            query =
                query.Where(
                    x =>
                        x.CreatedAtUtc >=
                            start);
        }

        if (to.HasValue)
        {
            var end =
                to.Value.Date
                    .AddDays(1);

            query =
                query.Where(
                    x =>
                        x.CreatedAtUtc <
                            end);
        }

        var now =
            DateTime.UtcNow;

        var total =
            await query.CountAsync(
                cancellationToken);

        var resolved =
            await query.CountAsync(
                x =>
                    x.Status ==
                        "resolved"
                    ||
                    x.Status ==
                        "closed",
                cancellationToken);

        var first =
            await query
                .Where(
                    x =>
                        x.FirstRespondedAtUtc !=
                            null)
                .Select(
                    x =>
                        (double?)
                        (
                            EF.Functions
                                .DateDiffSecond(
                                    x.CreatedAtUtc,
                                    x.FirstRespondedAtUtc!
                                        .Value)
                            -
                            x.FirstResponsePausedSeconds
                        )
                        /
                        3600)
                .AverageAsync(
                    cancellationToken);

        var resolution =
            await query
                .Where(
                    x =>
                        x.ResolvedAtUtc !=
                            null)
                .Select(
                    x =>
                        (double?)
                        (
                            EF.Functions
                                .DateDiffSecond(
                                    x.CreatedAtUtc,
                                    x.ResolvedAtUtc!
                                        .Value)
                            -
                            x.TotalSlaPausedSeconds
                        )
                        /
                        3600)
                .AverageAsync(
                    cancellationToken);

        var requesterCounts =
            await query
                .GroupBy(
                    x =>
                        x.RequesterUserId)
                .Select(
                    group =>
                        new
                        {
                            UserId =
                                group.Key,

                            Count =
                                group.Count()
                        })
                .ToListAsync(
                    cancellationToken);

        var requesterIds =
            requesterCounts
                .Select(
                    x =>
                        x.UserId)
                .ToArray();

        var requesterSites =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        requesterIds.Contains(
                            x.Id))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.SiteId
                        })
                .ToListAsync(
                    cancellationToken);

        var siteIds =
            requesterSites
                .Where(
                    x =>
                        x.SiteId.HasValue)
                .Select(
                    x =>
                        x.SiteId!.Value)
                .Distinct()
                .ToArray();

        var siteNames =
            await _db.Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        siteIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    x =>
                        x.Name,
                    cancellationToken);

        var requesterSiteNames =
            requesterSites
                .ToDictionary(
                    x =>
                        x.Id,
                    x =>
                        x.SiteId.HasValue
                            ? siteNames
                                .GetValueOrDefault(
                                    x.SiteId.Value,
                                    "Localidad no disponible")
                            : "Sin localidad");

        var byZone =
            requesterCounts
                .GroupBy(
                    x =>
                        requesterSiteNames
                            .GetValueOrDefault(
                                x.UserId,
                                "Sin localidad"))
                .Select(
                    group =>
                        new
                        {
                            label =
                                group.Key,

                            count =
                                group.Sum(
                                    x =>
                                        x.Count)
                        })
                .OrderByDescending(
                    x =>
                        x.count)
                .ToArray();

        var agentCounts =
            await query
                .GroupBy(
                    x =>
                        x.AssigneeUserId)
                .Select(
                    group =>
                        new
                        {
                            UserId =
                                group.Key,

                            Count =
                                group.Count()
                        })
                .ToListAsync(
                    cancellationToken);

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value)
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

        var byAgent =
            agentCounts
                .Select(
                    x =>
                        new
                        {
                            label =
                                x.UserId.HasValue
                                    ? users
                                        .GetValueOrDefault(
                                            x.UserId.Value,
                                            "Técnico no disponible")
                                    : "Sin técnico",

                            count =
                                x.Count
                        })
                .ToArray();

        var since =
            now.AddHours(
                -24);

        return Ok(
            new
            {
                total,

                resolved,

                active =
                    total -
                    resolved,

                byZone,

                byAgent,

                paused =
                    await query.CountAsync(
                        x =>
                            x.Status ==
                                "pendinguser",
                        cancellationToken),

                unassigned =
                    await query.CountAsync(
                        x =>
                            x.AssigneeUserId ==
                                null
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed",
                        cancellationToken),

                firstOverdue =
                    await query.CountAsync(
                        x =>
                            x.FirstRespondedAtUtc ==
                                null
                            &&
                            x.FirstResponseDueAtUtc <=
                                now
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed",
                        cancellationToken),

                resolutionOverdue =
                    await query.CountAsync(
                        x =>
                            x.ResolveDueAtUtc <=
                                now
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "closed",
                        cancellationToken),

                averageFirstResponseHours =
                    first,

                averageResolutionHours =
                    resolution,

                escalations24h =
                    await _db.HelpdeskTicketEvents
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId.Value
                                &&
                                x.EventType ==
                                    "sla_escalated"
                                &&
                                x.CreatedAtUtc >=
                                    since,
                            cancellationToken),

                byStatus =
                    await query
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
                        .ToListAsync(
                            cancellationToken),

                byCategory =
                    await query
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
                        .ToListAsync(
                            cancellationToken),

                daily =
                    await query
                        .GroupBy(
                            x =>
                                x.CreatedAtUtc.Date)
                        .Select(
                            group =>
                                new
                                {
                                    date =
                                        group.Key,

                                    count =
                                        group.Count()
                                })
                        .OrderBy(
                            x =>
                                x.date)
                        .ToListAsync(
                            cancellationToken),

                alerts =
                    await (
                        from activity
                            in _db.HelpdeskTicketEvents
                                .AsNoTracking()

                        join ticket
                            in query
                            on activity.TicketId
                            equals ticket.Id

                        where
                            activity.OrganizationId ==
                                organizationId.Value
                            &&
                            activity.CreatedAtUtc >=
                                since
                            &&
                            (
                                activity.EventType ==
                                    "sla_escalated"
                                ||
                                activity.EventType ==
                                    "classification_review"
                                ||
                                activity.EventType ==
                                    "auto_classified"
                                ||
                                activity.EventType ==
                                    "auto_handover"
                            )

                        orderby
                            activity.CreatedAtUtc
                                descending

                        select new
                        {
                            activity.Id,
                            activity.TicketId,
                            ticket.Number,
                            activity.EventType,
                            activity.Summary,
                            activity.CreatedAtUtc
                        }
                    )
                    .Take(100)
                    .ToListAsync(
                        cancellationToken),

                generatedAtUtc =
                    now
            });
    }

    public sealed record SettingsRequest(
        bool ClassificationEnabled,
        int EscalationDelayMinutes,
        int ReopenDays,
        int LowFirstResponseMinutes,
        int LowResolutionMinutes,
        int MediumFirstResponseMinutes,
        int MediumResolutionMinutes,
        int HighFirstResponseMinutes,
        int HighResolutionMinutes,
        int CriticalFirstResponseMinutes,
        int CriticalResolutionMinutes,
        bool PauseSlaWhenWaitingUser,
        int Revision);
}