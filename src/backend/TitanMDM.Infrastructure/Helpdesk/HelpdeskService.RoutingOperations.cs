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
            await (
                from routingEvent in _db.HelpdeskTicketEvents.AsNoTracking()
                join ticket in _db.HelpdeskTickets.AsNoTracking()
                    on routingEvent.TicketId equals ticket.Id
                where routingEvent.OrganizationId == organizationId
                    && routingEvent.EventType == "auto_assigned"
                    && routingEvent.CreatedAtUtc >= since24h
                    && ticket.CreatedAtUtc <= routingEvent.CreatedAtUtc
                select EF.Functions.DateDiffSecond(
                    ticket.CreatedAtUtc,
                    routingEvent.CreatedAtUtc))
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

        var eligibleTechnicianIds = await EligibleTechnicians(organizationId)
            .ToListAsync(cancellationToken);

        var activeMemberships = await (
                from member in _db.HelpdeskTeamMembers.AsNoTracking()
                join team in _db.HelpdeskTeams.AsNoTracking()
                    on member.TeamId equals team.Id
                where member.OrganizationId == organizationId
                    && team.IsActive
                    && member.IsAvailable
                    && member.AcceptsAutomaticAssignments
                    && member.MaxOpenTickets > 0
                    && eligibleTechnicianIds.Contains(member.UserId)
                select new
                {
                    member.TeamId,
                    member.UserId
                })
            .ToListAsync(cancellationToken);

        var activeMembers = activeMemberships
            .Select(x => x.UserId)
            .Distinct()
            .Count();

        var schedules =
            await _db.Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

        var membersOnDutyNow = activeMemberships
            .Where(member =>
            {
                var memberSchedules = schedules
                    .Where(x =>
                        x.TeamId == member.TeamId
                        && x.UserId == member.UserId)
                    .ToArray();
                return memberSchedules.Length == 0
                    || memberSchedules.Any(x => x.IsOnDuty(now));
            })
            .Select(x => x.UserId)
            .Distinct()
            .Count();

        var automaticRetriesPending = await _db.HelpdeskTickets
            .AsNoTracking()
            .CountAsync(x =>
                x.OrganizationId == organizationId
                && x.AssigneeUserId == null
                && x.RoutingAttempts > 0
                && x.Status != "closed"
                && x.Status != "resolved"
                && x.Status != "pendinguser",
                cancellationToken);

        var firstResponseSlaOverdue = await _db.HelpdeskTickets
            .AsNoTracking()
            .CountAsync(x =>
                x.OrganizationId == organizationId
                && x.Status != "closed"
                && x.Status != "resolved"
                && x.FirstRespondedAtUtc == null
                && x.FirstResponseDueAtUtc < now,
                cancellationToken);

        var resolutionSlaOverdue = await _db.HelpdeskTickets
            .AsNoTracking()
            .CountAsync(x =>
                x.OrganizationId == organizationId
                && x.Status != "closed"
                && x.Status != "resolved"
                && x.ResolvedAtUtc == null
                && x.ResolveDueAtUtc < now,
                cancellationToken);

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
            membersOnDutyNow,
            automaticRetriesPending,
            firstResponseSlaOverdue,
            resolutionSlaOverdue);
    }

    // ============================================================
    // HD-D5
    // DIAGNOSTIC BY BUSINESS NUMBER OR INTERNAL GUID
    // ============================================================

    public async Task<HelpdeskRoutingDiagnosticSnapshot?>
        GetRoutingDiagnosticByReferenceAsync(
            Guid organizationId,
            string ticketReference,
            CancellationToken cancellationToken = default)
    {
        var reference =
            (
                ticketReference
                ??
                string.Empty
            )
            .Trim();

        if (string.IsNullOrWhiteSpace(
                reference))
        {
            return null;
        }

        if (
            Guid.TryParse(
                reference,
                out var ticketId))
        {
            return await GetRoutingDiagnosticAsync(
                organizationId,
                ticketId,
                cancellationToken);
        }

        var normalized =
            reference
                .Trim()
                .ToUpperInvariant();

        /*
         * Permitimos:
         *
         * HD-5
         * hd-5
         * 5
         *
         * pero el identificador comercial almacenado continÃºa
         * siendo el canÃ³nico HD-X.
         */
        if (
            int.TryParse(
                normalized,
                out var numericTicket)
            &&
            numericTicket >
                0)
        {
            normalized =
                $"HD-{numericTicket}";
        }

        var resolvedId =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Number
                            .ToUpper() ==
                            normalized)
                .Select(
                    x =>
                        (Guid?)
                        x.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (!resolvedId.HasValue)
        {
            return null;
        }

        return await GetRoutingDiagnosticAsync(
            organizationId,
            resolvedId.Value,
            cancellationToken);
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

        // Comparte las reglas RBAC, equipos y cobertura con el motor de asignacion.
        var eligibleIds = (await EligibleTechnicians(organizationId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var routingTeams = await LoadRoutingTeamsAsync(
            organizationId,
            NormalizeRoutingCategory(ticket.Category),
            ticket.RequestedTeamId,
            cancellationToken);

        var validCoverages = routing.SiteId.HasValue
            ? await LoadRankedCoveragesAsync(
                organizationId,
                routing.SiteId.Value,
                routing.SiteLocationId,
                NormalizeRoutingCategory(ticket.Category),
                routingTeams.Keys.ToArray(),
                cancellationToken)
            : [];

        var coveredTeamIds = validCoverages
            .Select(x => x.Coverage.TeamId)
            .ToHashSet();
        var candidates =
            new List<HelpdeskRoutingCandidatePreview>();

        foreach (var team in teams)
        {
            var teamMembers = members.Where(x => x.TeamId == team.Id).ToList();

            foreach (var member in teamMembers)
            {
                var user = users.FirstOrDefault(x => x.Id == member.UserId);
                if (user is null)
                {
                    continue;
                }

                var userSchedules = schedules
                    .Where(x => x.TeamId == team.Id && x.UserId == member.UserId)
                    .ToList();

                var hasSchedule = userSchedules.Count > 0;
                // No schedule: membership availability governs.
                // With schedule: a slot must be active.
                var onDutyNow = !hasSchedule || userSchedules.Any(x => x.IsOnDuty(now));
                var openTickets = loadMap.GetValueOrDefault(member.UserId);
                var reasons = new List<string>();

                if (!routingTeams.ContainsKey(team.Id))
                    reasons.Add("Grupo incompatible, inactivo o no solicitado");

                if (!coveredTeamIds.Contains(team.Id))
                    reasons.Add("Grupo sin cobertura para localidad y categoria");

                if (!eligibleIds.Contains(member.UserId))
                    reasons.Add("Tecnico sin permisos RBAC de Helpdesk");

                if (!member.IsAvailable)
                    reasons.Add("Miembro no disponible");

                if (!member.AcceptsAutomaticAssignments)
                    reasons.Add("No acepta autoasignacion");

                if (member.MaxOpenTickets <= 0)
                    reasons.Add("Capacidad no configurada");
                else if (openTickets >= member.MaxOpenTickets)
                    reasons.Add("Capacidad agotada");

                if (!onDutyNow)
                    reasons.Add("Fuera de turno");

                if (ticket.AssigneeUserId.HasValue)
                    reasons.Add("Ticket ya asignado");

                if (ticket.Status is "pendinguser" or "closed" or "resolved")
                    reasons.Add("Estado incompatible con autoasignacion");

                var canAssign = reasons.Count == 0;

                candidates.Add(new HelpdeskRoutingCandidatePreview(
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
                    canAssign ? "Candidato operativo" : string.Join("; ", reasons)));
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

        var requester = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId
                && x.Id == ticket.RequesterUserId)
            .Select(x => new
            {
                x.IsActive,
                x.SiteId,
                x.SiteLocationId
            })
            .SingleOrDefaultAsync(cancellationToken);

        var requesterBlockReasons = new List<string>();
        if (requester is null)
        {
            requesterBlockReasons.Add(
                "El solicitante no pertenece a la organización.");
        }
        else if (!requester.IsActive)
        {
            requesterBlockReasons.Add(
                "El solicitante está desactivado.");
        }

        var effectiveSiteId = ticket.SiteId ?? requester?.SiteId;
        var effectiveLocationId = ticket.SiteId.HasValue
            ? ticket.SiteLocationId
            : requester?.SiteLocationId;

        var site = effectiveSiteId.HasValue
            ? await _db.Sites
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId
                    && x.Id == effectiveSiteId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.IsActive
                })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        if (!effectiveSiteId.HasValue)
        {
            requesterBlockReasons.Add(
                "El solicitante no tiene una localidad asignada.");
        }
        else if (site is null)
        {
            requesterBlockReasons.Add(
                "La localidad del solicitante no pertenece a la organización.");
        }
        else if (!site.IsActive)
        {
            requesterBlockReasons.Add(
                "La localidad del solicitante está desactivada.");
        }

        var location = effectiveLocationId.HasValue && site is not null
            ? await _db.SiteLocations
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId
                    && x.Id == effectiveLocationId.Value)
                .Select(x => new
                {
                    x.SiteId,
                    x.Name,
                    x.IsActive
                })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        if (effectiveLocationId.HasValue
            && (location is null
                || location.SiteId != effectiveSiteId
                || !location.IsActive))
        {
            requesterBlockReasons.Add(
                "La sublocalidad del solicitante no está activa o no pertenece a su localidad.");
        }

        return new HelpdeskRoutingDiagnosticSnapshot(
            ticket.Id,
            ticket.Number,
            ticket.Status,
            ticket.Category,
            ticket.Subject,
            ticket.ExternalRequesterEmail,
            ticket.RequesterUserId,
            routing.SiteId,
            site?.Name,
            routing.SiteLocationId,
            routing.SiteLocationId.HasValue ? location?.Name : null,
            ticket.RequestedTeamId,
            ticket.AssigneeUserId,
            ticket.AssigneeUserId.HasValue,
            string.Equals(ticket.Status, "pendinguser", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(routing.Reason)
                ? "Sin observaciones."
                : routing.Reason,
            requesterBlockReasons,
            selected,
            candidates);
    }


public async Task<HelpdeskRoutingQueueResult>
    RetryAutomaticAssignmentForOpenTicketsAsync(
        Guid organizationId,
        int maxTickets,
        bool dryRun,
        CancellationToken cancellationToken = default)
{
    maxTickets = Math.Clamp(maxTickets, 1, 200);

    var tickets = await _db.HelpdeskTickets
        .AsNoTracking()
        .Where(x =>
            x.OrganizationId == organizationId &&
            x.AssigneeUserId == null &&
            x.Status != "closed" &&
            x.Status != "resolved" &&
            x.Status != "pendinguser")
        .OrderBy(x => x.CreatedAtUtc)
        .Take(maxTickets)
        .Select(x => new
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
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (dryRun)
            {
                // La simulación no escribe en la base de datos
                // ni enriquece campos del ticket.

                var preview = await EvaluateRoutingAsync(
                    organizationId,
                    ticket.RequesterUserId,
                    ticket.Category,
                    cancellationToken,
                    ticket.RequestedTeamId,
                    ticket.SiteId,
                    ticket.SiteLocationId);

                if (preview.Candidate is null)
                {
                    skipped++;

                    items.Add(
                        new HelpdeskRoutingQueueItemResult(
                            ticket.Id,
                            ticket.Number,
                            ticket.Category,
                            ticket.Status,
                            "skipped",
                            string.IsNullOrWhiteSpace(
                                preview.Reason)
                                ? "Sin técnico elegible."
                                : preview.Reason));

                    continue;
                }

                skipped++;

                items.Add(
                    new HelpdeskRoutingQueueItemResult(
                        ticket.Id,
                        ticket.Number,
                        ticket.Category,
                        ticket.Status,
                        "dry-run",
                        $"Asignable a " +
                        $"{preview.Candidate.UserName} / " +
                        $"{preview.Candidate.TeamName}."));

                continue;
            }

            // El mismo camino que utiliza el worker:
            // validación de identidad,
            // enriquecimiento contextual y
            // asignación empresarial transaccional.

            var success =
                await RetryAutomaticAssignmentWithFallbackAsync(
                    organizationId,
                    ticket.Id,
                    cancellationToken);

            // Evitar leer datos obsoletos después de
            // ExecuteUpdateAsync y otros contextos de trabajo.
            _db.ChangeTracker.Clear();

            var current = await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.Id == ticket.Id)
                .Select(x => new
                {
                    x.AssigneeUserId,
                    x.Status,
                    x.RequesterUserId,
                    x.Category,
                    x.RequestedTeamId,
                    x.SiteId,
                    x.SiteLocationId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (success &&
                current?.AssigneeUserId is not null)
            {
                assigned++;

                items.Add(
                    new HelpdeskRoutingQueueItemResult(
                        ticket.Id,
                        ticket.Number,
                        ticket.Category,
                        ticket.Status,
                        "assigned",
                        "Asignación automática completada " +
                        "mediante el motor empresarial."));

                continue;
            }

            if (current is null ||
                current.Status is "closed" or "resolved" ||
                current.AssigneeUserId is not null)
            {
                skipped++;

                items.Add(
                    new HelpdeskRoutingQueueItemResult(
                        ticket.Id,
                        ticket.Number,
                        ticket.Category,
                        ticket.Status,
                        "skipped",
                        "El ticket cambió durante el procesamiento."));

                continue;
            }

            // Evaluar nuevamente para obtener el motivo real
            // después del enriquecimiento contextual.
            var diagnosis = await EvaluateRoutingAsync(
                organizationId,
                current.RequesterUserId,
                current.Category,
                cancellationToken,
                current.RequestedTeamId,
                current.SiteId,
                current.SiteLocationId);

            skipped++;

            items.Add(
                new HelpdeskRoutingQueueItemResult(
                    ticket.Id,
                    ticket.Number,
                    ticket.Category,
                    ticket.Status,
                    "skipped",
                    diagnosis.Candidate is null
                        ? (
                            string.IsNullOrWhiteSpace(
                                diagnosis.Reason)
                                ? "No existe técnico elegible."
                                : diagnosis.Reason
                          )
                        : "Se encontró un candidato, pero " +
                          "no se confirmó la asignación. " +
                          "Revisar capacidad, concurrencia " +
                          "y cobertura."));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
