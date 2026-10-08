using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    public async Task<HelpdeskRoutingHealthSnapshot> GetRoutingHealthAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var since24h = now.AddHours(-24);

        var openTickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Status != "closed" &&
                        x.Status != "resolved",
                    cancellationToken);

        var unassignedTickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.AssigneeUserId == null &&
                        x.Status != "closed" &&
                        x.Status != "resolved" &&
                        x.Status != "pendinguser",
                    cancellationToken);

        var pendingUserTickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Status == "pendinguser",
                    cancellationToken);

        var autoAssignedLast24Hours =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.EventType == "auto_assigned" &&
                        x.CreatedAtUtc >= since24h,
                    cancellationToken);

        var routingWaitingLast24Hours =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.EventType == "routing_waiting" &&
                        x.CreatedAtUtc >= since24h,
                    cancellationToken);

        var slaEscalatedLast24Hours =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.EventType == "sla_escalated" &&
                        x.CreatedAtUtc >= since24h,
                    cancellationToken);

        var autoAssignedDurations =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.AssigneeUserId != null &&
                        x.UpdatedAtUtc >= since24h &&
                        x.CreatedAtUtc <= x.UpdatedAtUtc)
                .Select(
                    x =>
                        EF.Functions.DateDiffSecond(
                            x.CreatedAtUtc,
                            x.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

        var averageAutoAssignMinutesLast24Hours =
            autoAssignedDurations.Count == 0
                ? 0
                : Math.Round(autoAssignedDurations.Average() / 60d, 2);

        var activeTeams =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.IsActive,
                    cancellationToken);

        var activeMembers =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.IsAvailable &&
                        x.AcceptsAutomaticAssignments &&
                        x.MaxOpenTickets > 0,
                    cancellationToken);

        var schedules =
            await _db.Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

        var membersOnDutyNow =
            schedules
                .Where(x => x.IsOnDuty(now))
                .Select(x => x.UserId)
                .Distinct()
                .Count();

        return new HelpdeskRoutingHealthSnapshot(
            openTickets,
            unassignedTickets,
            pendingUserTickets,
            autoAssignedLast24Hours,
            routingWaitingLast24Hours,
            slaEscalatedLast24Hours,
            averageAutoAssignMinutesLast24Hours,
            activeTeams,
            activeMembers,
            membersOnDutyNow);
    }

    public async Task<HelpdeskRoutingDiagnosticSnapshot?> GetRoutingDiagnosticAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == ticketId,
                    cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var routing =
            await EvaluateRoutingAsync(
                organizationId,
                ticket.RequesterUserId,
                ticket.Category,
                cancellationToken,
                ticket.RequestedTeamId,
                ticket.SiteId,
                ticket.SiteLocationId);

        var now = DateTime.UtcNow;

        var teams =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.IsActive)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Name,
                            x.Categories
                        })
                .ToListAsync(cancellationToken);

        var members =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .Select(
                    x =>
                        new
                        {
                            x.TeamId,
                            x.UserId,
                            x.IsAvailable,
                            x.AcceptsAutomaticAssignments,
                            x.MaxOpenTickets
                        })
                .ToListAsync(cancellationToken);

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.IsActive)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName,
                            x.Email
                        })
                .ToListAsync(cancellationToken);

        var schedules =
            await _db.Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

        var openLoads =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.AssigneeUserId != null &&
                        x.Status != "closed" &&
                        x.Status != "resolved")
                .GroupBy(x => x.AssigneeUserId!.Value)
                .Select(
                    g =>
                        new
                        {
                            UserId = g.Key,
                            Count = g.Count()
                        })
                .ToListAsync(cancellationToken);

        var loadMap =
            openLoads.ToDictionary(x => x.UserId, x => x.Count);

        static bool TeamContainsCategory(string? categories, string category)
        {
            if (string.IsNullOrWhiteSpace(categories))
            {
                return false;
            }

            return categories
                .Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Any(
                    x =>
                        string.Equals(
                            x,
                            category,
                            StringComparison.OrdinalIgnoreCase));
        }

        var candidates =
            new List<HelpdeskRoutingCandidatePreview>();

        foreach (var team in teams)
        {
            if (!TeamContainsCategory(team.Categories, ticket.Category))
            {
                continue;
            }

            var teamMembers =
                members.Where(x => x.TeamId == team.Id).ToList();

            foreach (var member in teamMembers)
            {
                var user =
                    users.FirstOrDefault(x => x.Id == member.UserId);

                if (user is null)
                {
                    continue;
                }

                var userSchedules =
                    schedules
                        .Where(
                            x =>
                                x.TeamId == team.Id &&
                                x.UserId == member.UserId)
                        .ToList();

                var hasSchedule = userSchedules.Count > 0;
                var onDutyNow = hasSchedule && userSchedules.Any(x => x.IsOnDuty(now));
                var openTickets = loadMap.TryGetValue(member.UserId, out var count) ? count : 0;

                var reasons = new List<string>();

                if (!member.IsAvailable)
                {
                    reasons.Add("Miembro no disponible");
                }

                if (!member.AcceptsAutomaticAssignments)
                {
                    reasons.Add("No acepta autoasignaciÃ³n");
                }

                if (member.MaxOpenTickets <= 0)
                {
                    reasons.Add("Capacidad no configurada");
                }
                else if (openTickets >= member.MaxOpenTickets)
                {
                    reasons.Add("Capacidad agotada");
                }

                if (!hasSchedule)
                {
                    reasons.Add("Sin horario configurado");
                }
                else if (!onDutyNow)
                {
                    reasons.Add("Fuera de turno");
                }

                var canAssign =
                    reasons.Count == 0 &&
                    ticket.AssigneeUserId == null &&
                    ticket.Status != "pendinguser" &&
                    ticket.Status != "closed" &&
                    ticket.Status != "resolved";

                candidates.Add(
                    new HelpdeskRoutingCandidatePreview(
                        team.Id,
                        team.Name,
                        member.UserId,
                        $"{user.FirstName} {user.LastName}".Trim(),
                        user.Email,
                        member.IsAvailable,
                        member.AcceptsAutomaticAssignments,
                        member.MaxOpenTickets,
                        openTickets,
                        hasSchedule,
                        onDutyNow,
                        canAssign ? "assignable" : "blocked",
                        canAssign
                            ? "Candidato operativo"
                            : string.Join("; ", reasons)));
            }
        }

        candidates =
            candidates
                .OrderBy(x => x.Decision == "assignable" ? 0 : 1)
                .ThenBy(x => x.OpenTickets)
                .ThenBy(x => x.TeamName)
                .ThenBy(x => x.TechnicianName)
                .ToList();

        HelpdeskRoutingCandidatePreview? selected = null;

        if (routing.Candidate is not null)
        {
            selected =
                candidates.FirstOrDefault(
                    x =>
                        x.TeamId == routing.Candidate.TeamId &&
                        x.UserId == routing.Candidate.UserId);
        }

        return new HelpdeskRoutingDiagnosticSnapshot(
            ticket.Id,
            ticket.Number,
            ticket.Status,
            ticket.Category,
            ticket.Subject,
            ticket.ExternalRequesterEmail,
            ticket.RequesterUserId,
            ticket.SiteId,
            ticket.SiteLocationId,
            ticket.RequestedTeamId,
            ticket.AssigneeUserId,
            ticket.AssigneeUserId.HasValue,
            string.Equals(ticket.Status, "pendinguser", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(routing.Reason)
                ? "Sin observaciones."
                : routing.Reason,
            selected,
            candidates);
    }

    public async Task<HelpdeskRoutingQueueResult> RetryAutomaticAssignmentForOpenTicketsAsync(
        Guid organizationId,
        int maxTickets,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        maxTickets = Math.Clamp(maxTickets, 1, 200);

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.AssigneeUserId == null &&
                        x.Status != "closed" &&
                        x.Status != "resolved" &&
                        x.Status != "pendinguser")
                .OrderBy(x => x.CreatedAtUtc)
                .Take(maxTickets)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Number,
                            x.Category,
                            x.Status,
                            x.RequesterUserId,
                            x.RequestedTeamId,
                            x.SiteId,
                            x.SiteLocationId
                        })
                .ToListAsync(cancellationToken);

        var items = new List<HelpdeskRoutingQueueItemResult>();
        var assigned = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var ticket in tickets)
        {
            try
            {
                var routing =
                    await EvaluateRoutingAsync(
                        organizationId,
                        ticket.RequesterUserId,
                        ticket.Category,
                        cancellationToken,
                        ticket.RequestedTeamId,
                        ticket.SiteId,
                        ticket.SiteLocationId);

                if (routing.Candidate is null)
                {
                    skipped++;

                    items.Add(
                        new HelpdeskRoutingQueueItemResult(
                            ticket.Id,
                            ticket.Number,
                            ticket.Category,
                            ticket.Status,
                            "skipped",
                            string.IsNullOrWhiteSpace(routing.Reason)
                                ? "Sin candidato vÃ¡lido."
                                : routing.Reason));

                    continue;
                }

                if (dryRun)
                {
                    skipped++;

                    items.Add(
                        new HelpdeskRoutingQueueItemResult(
                            ticket.Id,
                            ticket.Number,
                            ticket.Category,
                            ticket.Status,
                            "dry-run",
                           $"Asignable a {routing.Candidate.UserName} / {routing.Candidate.TeamName}."));

                    continue;
                }

                var ok =
                    await RetryAutomaticAssignmentEnterpriseAsync(
                        organizationId,
                        ticket.Id,
                        cancellationToken);

                if (ok)
                {
                    assigned++;

                    items.Add(
                        new HelpdeskRoutingQueueItemResult(
                            ticket.Id,
                            ticket.Number,
                            ticket.Category,
                            ticket.Status,
                            "assigned",
                           $"Asignado automÃ¡ticamente a {routing.Candidate.UserName} / {routing.Candidate.TeamName}."));
                }
                else
                {
                    failed++;

                    items.Add(
                        new HelpdeskRoutingQueueItemResult(
                            ticket.Id,
                            ticket.Number,
                            ticket.Category,
                            ticket.Status,
                            "failed",
                            "El candidato existÃ­a, pero la asignaciÃ³n fallÃ³ durante la revalidaciÃ³n transaccional."));
                }
            }
            catch (Exception exception)
            {
                failed++;

                items.Add(
                    new HelpdeskRoutingQueueItemResult(
                        ticket.Id,
                        ticket.Number,
                        ticket.Category,
                        ticket.Status,
                        "failed",
                        Limit(exception.Message, 300)));
            }
        }

        return new HelpdeskRoutingQueueResult(
            tickets.Count,
            assigned,
            skipped,
            failed,
            dryRun,
            items);
    }

    private static string Limit(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength];
    }
}
