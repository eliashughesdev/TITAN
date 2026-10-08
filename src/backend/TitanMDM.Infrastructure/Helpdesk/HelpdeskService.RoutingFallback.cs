using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // HD-D6 + HD-D7
    //
    // ROUTING AUTOMÁTICO CON FALLBACK EMPRESARIAL
    //
    // Orden:
    //
    // 1. Routing enterprise estricto.
    // 2. Enriquecer contexto del ticket.
    // 3. Reintentar routing enterprise.
    // 4. Grupo solicitado.
    // 5. Grupo por categoría.
    // 6. Grupo inferido por contexto.
    // 7. Cualquier grupo operativo compatible.
    // 8. Fallback global al técnico menos cargado.
    //
    // IMPORTANTE:
    //
    // - Nunca duplica asignaciones.
    // - Nunca sustituye una asignación existente.
    // - Respeta RBAC.
    // - Prefiere técnicos en turno.
    // - Prefiere técnicos dentro de capacidad.
    // - Solo excede capacidad en fallback de emergencia.
    // - Un técnico con autoasignación deshabilitada nunca
    //   recibe tickets automáticamente.
    // ============================================================

    public async Task<bool>
        RetryAutomaticAssignmentWithFallbackAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        // ========================================================
        // NIVEL 1
        // MOTOR ENTERPRISE NORMAL
        // ========================================================

        if (
            await RetryAutomaticAssignmentEnterpriseAsync(
                organizationId,
                ticketId,
                cancellationToken))
        {
            return true;
        }

        var ticket =
            await GetRoutingTicketAsync(
                organizationId,
                ticketId,
                cancellationToken);

        if (!CanAutomaticallyAssign(ticket))
        {
            return false;
        }

        // ========================================================
        // NIVEL 2
        // ENRIQUECER CONTEXTO
        // ========================================================

        var contextChanged =
            await EnrichRoutingContextAsync(
                ticket!,
                cancellationToken);

        if (contextChanged)
        {
            _db.ChangeTracker.Clear();

            // ====================================================
            // NIVEL 3
            // REINTENTAR ENTERPRISE CON INFORMACIÓN MEJORADA
            // ====================================================

            if (
                await RetryAutomaticAssignmentEnterpriseAsync(
                    organizationId,
                    ticketId,
                    cancellationToken))
            {
                return true;
            }
        }

        _db.ChangeTracker.Clear();

        ticket =
            await GetRoutingTicketAsync(
                organizationId,
                ticketId,
                cancellationToken);

        if (!CanAutomaticallyAssign(ticket))
        {
            return false;
        }

        // ========================================================
        // NIVEL 4+
        // FALLBACK EMPRESARIAL
        // ========================================================

        var fallback =
            await FindFallbackCandidateAsync(
                ticket!,
                cancellationToken);

        if (fallback is null)
        {
            await RecordRoutingFallbackWaitingAsync(
                ticket!,
                "No existe ningún técnico habilitado para autoasignación.",
                cancellationToken);

            return false;
        }

        return await AssignFallbackCandidateAsync(
            ticket!,
            fallback,
            cancellationToken);
    }

    // ============================================================
    // TICKET
    // ============================================================

    private async Task<HelpdeskTicket?>
        GetRoutingTicketAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        return await _db
            .HelpdeskTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Id ==
                        ticketId,
                cancellationToken);
    }

    private static bool
        CanAutomaticallyAssign(
            HelpdeskTicket? ticket)
    {
        return
            ticket is not null
            &&
            !ticket.AssigneeUserId.HasValue
            &&
            ticket.Status
                is not (
                    "resolved"
                    or
                    "closed"
                    or
                    "pendinguser");
    }

    // ============================================================
    // CONTEXT ENRICHMENT
    // ============================================================

    private async Task<bool>
        EnrichRoutingContextAsync(
            HelpdeskTicket snapshot,
            CancellationToken cancellationToken)
    {
        var ticket =
            await _db
                .HelpdeskTickets
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            snapshot.OrganizationId
                        &&
                        x.Id ==
                            snapshot.Id
                        &&
                        x.AssigneeUserId ==
                            null,
                    cancellationToken);

        if (ticket is null)
        {
            return false;
        }

        var changed =
            false;

        var reasons =
            new List<string>();

        // ========================================================
        // DEVICE CONTEXT
        //
        // El dispositivo enrolado es una fuente de ubicación
        // más fuerte que texto libre del ticket.
        // ========================================================

        if (
            !ticket.SiteId.HasValue
            &&
            ticket.DeviceId.HasValue)
        {
            var device =
                await _db
                    .Devices
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                ticket.DeviceId.Value
                            &&
                            !x.IsDeleted)
                    .Select(
                        x =>
                            new
                            {
                                x.SiteId,
                                x.SiteLocationId,
                                x.DeviceName
                            })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (
                device?.SiteId
                is { } deviceSiteId)
            {
                ticket.AssignSite(
                    deviceSiteId,
                    device.SiteLocationId);

                changed =
                    true;

                reasons.Add(
                    $"localidad inferida desde dispositivo {device.DeviceName}");
            }
        }

        // ========================================================
        // REQUESTER CONTEXT
        //
        // Para correo solamente confiamos en el usuario cuando
        // ExternalRequesterEmail coincide con Users.Email.
        // Esto evita usar por accidente la localidad del actor
        // técnico del buzón.
        // ========================================================

        var requester =
            await _db
                .Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.Id ==
                            ticket.RequesterUserId
                        &&
                        x.IsActive)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Email,
                            x.SiteId,
                            x.SiteLocationId,
                            x.JobTitle
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        var requesterTrusted =
            requester is not null
            &&
            (
                !string.Equals(
                    ticket.Source,
                    "email",
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.IsNullOrWhiteSpace(
                    ticket.ExternalRequesterEmail)
                ||
                string.Equals(
                    requester.Email,
                    ticket.ExternalRequesterEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase)
            );

        if (
            !ticket.SiteId.HasValue
            &&
            requesterTrusted
            &&
            requester?.SiteId
                is { } requesterSiteId)
        {
            ticket.AssignSite(
                requesterSiteId,
                requester.SiteLocationId);

            changed =
                true;

            reasons.Add(
                "localidad inferida desde perfil corporativo");
        }

        // ========================================================
        // TEAM CONTEXT
        //
        // Si el ticket ya tiene grupo solicitado no lo tocamos.
        // Si no:
        //
        // 1. categoría exacta;
        // 2. asunto/descripción;
        // 3. cargo;
        // 4. información del dispositivo.
        // ========================================================

        if (!ticket.RequestedTeamId.HasValue)
        {
            var teamId =
                await InferTeamAsync(
                    ticket,
                    requester?.JobTitle,
                    cancellationToken);

            if (teamId.HasValue)
            {
                await _db
                    .HelpdeskTickets
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                ticket.Id
                            &&
                            x.AssigneeUserId ==
                                null)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                x =>
                                    x.RequestedTeamId,
                                teamId),
                        cancellationToken);

                changed =
                    true;

                reasons.Add(
                    "grupo inferido automáticamente");
            }
        }

        if (!changed)
        {
            return false;
        }

        _db
            .HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    ticket.OrganizationId,
                    ticket.Id,
                    null,
                    "routing_context_enriched",
                    TrimFallbackSummary(
                        "Contexto de routing enriquecido: "
                        +
                        string.Join(
                            ", ",
                            reasons)
                        +
                        ".")));

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return true;
    }

    // ============================================================
    // TEAM INFERENCE
    // ============================================================

    private async Task<Guid?>
        InferTeamAsync(
            HelpdeskTicket ticket,
            string? requesterJobTitle,
            CancellationToken cancellationToken)
    {
        var teams =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.IsActive)
                .ToListAsync(
                    cancellationToken);

        if (teams.Count == 0)
        {
            return null;
        }

        var category =
            NormalizeFallbackText(
                ticket.Category);

        // ========================================================
        // EXACT CATEGORY
        // ========================================================

        if (
            category.Length > 0
            &&
            category !=
                "general")
        {
            var categoryMatches =
                teams
                    .Where(
                        x =>
                            x.HandlesCategory(
                                ticket.Category))
                    .ToList();

            if (categoryMatches.Count == 1)
            {
                return categoryMatches[0].Id;
            }

            if (categoryMatches.Count > 1)
            {
                return await SelectBestTeamByLoadAsync(
                    ticket.OrganizationId,
                    categoryMatches
                        .Select(
                            x =>
                                x.Id)
                        .ToArray(),
                    cancellationToken);
            }
        }

        // ========================================================
        // TEXT CONTEXT
        // ========================================================

        string? deviceName =
            null;

        string? deviceDepartment =
            null;

        if (ticket.DeviceId.HasValue)
        {
            var device =
                await _db
                    .Devices
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                ticket.DeviceId.Value)
                    .Select(
                        x =>
                            new
                            {
                                x.DeviceName,
                                x.Department
                            })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            deviceName =
                device?.DeviceName;

            deviceDepartment =
                device?.Department;
        }

        var searchText =
            NormalizeFallbackText(
                string.Join(
                    " ",
                    ticket.Subject,
                    ticket.Description,
                    requesterJobTitle,
                    deviceName,
                    deviceDepartment));

        if (searchText.Length == 0)
        {
            return null;
        }

        var scored =
            teams
                .Select(
                    team =>
                        new
                        {
                            Team =
                                team,

                            Score =
                                ScoreTeam(
                                    team,
                                    searchText)
                        })
                .Where(
                    x =>
                        x.Score > 0)
                .OrderByDescending(
                    x =>
                        x.Score)
                .ToList();

        if (scored.Count == 0)
        {
            return null;
        }

        var bestScore =
            scored[0].Score;

        var bestTeams =
            scored
                .Where(
                    x =>
                        x.Score ==
                            bestScore)
                .Select(
                    x =>
                        x.Team.Id)
                .ToArray();

        if (bestTeams.Length == 1)
        {
            return bestTeams[0];
        }

        return await SelectBestTeamByLoadAsync(
            ticket.OrganizationId,
            bestTeams,
            cancellationToken);
    }

    private static int
        ScoreTeam(
            HelpdeskTeam team,
            string normalizedText)
    {
        var score =
            0;

        var teamName =
            NormalizeFallbackText(
                team.Name);

        if (
            teamName.Length > 3
            &&
            normalizedText.Contains(
                teamName,
                StringComparison.Ordinal))
        {
            score += 100;
        }

        var categories =
            (
                team.Categories
                ??
                string.Empty
            )
            .Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries
                |
                StringSplitOptions.TrimEntries);

        foreach (var category in categories)
        {
            var normalizedCategory =
                NormalizeFallbackText(
                    category);

            if (
                normalizedCategory.Length >
                    2
                &&
                normalizedText.Contains(
                    normalizedCategory,
                    StringComparison.Ordinal))
            {
                score += 25;
            }
        }

        return score;
    }

    // ============================================================
    // SELECT BEST TEAM BY CURRENT LOAD
    // ============================================================

    private async Task<Guid?>
        SelectBestTeamByLoadAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid> teamIds,
            CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return null;
        }

        var loads =
            await (
                from member
                    in _db.HelpdeskTeamMembers
                        .AsNoTracking()

                join ticket
                    in _db.HelpdeskTickets
                        .AsNoTracking()
                    on member.UserId
                    equals ticket.AssigneeUserId
                    into tickets

                where
                    member.OrganizationId ==
                        organizationId
                    &&
                    teamIds.Contains(
                        member.TeamId)
                    &&
                    member.IsAvailable
                    &&
                    member.AcceptsAutomaticAssignments

                select new
                {
                    member.TeamId,

                    OpenTickets =
                        tickets.Count(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed")
                }
            )
            .ToListAsync(
                cancellationToken);

        if (loads.Count == 0)
        {
            return teamIds
                .OrderBy(
                    x =>
                        x)
                .FirstOrDefault();
        }

        return loads
            .GroupBy(
                x =>
                    x.TeamId)
            .Select(
                g =>
                    new
                    {
                        TeamId =
                            g.Key,

                        Load =
                            g.Sum(
                                x =>
                                    x.OpenTickets)
                    })
            .OrderBy(
                x =>
                    x.Load)
            .ThenBy(
                x =>
                    x.TeamId)
            .Select(
                x =>
                    (Guid?)x.TeamId)
            .FirstOrDefault();
    }

    // ============================================================
    // FALLBACK CANDIDATE
    // ============================================================

    private async Task<FallbackCandidate?>
        FindFallbackCandidateAsync(
            HelpdeskTicket ticket,
            CancellationToken cancellationToken)
    {
        var eligibleIds =
            await EligibleTechnicians(
                    ticket.OrganizationId)
                .ToListAsync(
                    cancellationToken);

        if (eligibleIds.Count == 0)
        {
            return null;
        }

        var teams =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.IsActive)
                .ToListAsync(
                    cancellationToken);

        if (teams.Count == 0)
        {
            return null;
        }

        // ========================================================
        // TEAM PRIORITY
        //
        // RequestedTeamId siempre tiene prioridad.
        //
        // Después categoría.
        //
        // Después grupo general.
        //
        // Finalmente cualquier grupo con técnico operativo.
        // ========================================================

        var orderedTeamIds =
            new List<Guid>();

        if (
            ticket.RequestedTeamId.HasValue
            &&
            teams.Any(
                x =>
                    x.Id ==
                        ticket.RequestedTeamId.Value))
        {
            orderedTeamIds.Add(
                ticket.RequestedTeamId.Value);
        }

        if (
            !string.Equals(
                ticket.Category,
                "general",
                StringComparison.OrdinalIgnoreCase))
        {
            orderedTeamIds.AddRange(
                teams
                    .Where(
                        x =>
                            x.HandlesCategory(
                                ticket.Category))
                    .Select(
                        x =>
                            x.Id));
        }

        orderedTeamIds.AddRange(
            teams
                .Where(
                    x =>
                        string.IsNullOrWhiteSpace(
                            x.Categories))
                .Select(
                    x =>
                        x.Id));

        orderedTeamIds.AddRange(
            teams.Select(
                x =>
                    x.Id));

        orderedTeamIds =
            orderedTeamIds
                .Distinct()
                .ToList();

        var members =
            await _db
                .HelpdeskTeamMembers
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        orderedTeamIds.Contains(
                            x.TeamId)
                        &&
                        eligibleIds.Contains(
                            x.UserId)
                        &&
                        x.IsAvailable
                        &&
                        x.AcceptsAutomaticAssignments
                        &&
                        x.MaxOpenTickets >
                            0)
                .ToListAsync(
                    cancellationToken);

        if (members.Count == 0)
        {
            return null;
        }

        var userIds =
            members
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToArray();

        var users =
            await _db
                .Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.IsActive
                        &&
                        userIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        if (users.Count == 0)
        {
            return null;
        }

        var loads =
            await _db
                .HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.AssigneeUserId
                            .HasValue
                        &&
                        userIds.Contains(
                            x.AssigneeUserId.Value)
                        &&
                        x.Status !=
                            "resolved"
                        &&
                        x.Status !=
                            "closed")
                .GroupBy(
                    x =>
                        x.AssigneeUserId!.Value)
                .Select(
                    g =>
                        new
                        {
                            UserId =
                                g.Key,

                            Count =
                                g.Count()
                        })
                .ToDictionaryAsync(
                    x =>
                        x.UserId,
                    x =>
                        x.Count,
                    cancellationToken);

        var schedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        userIds.Contains(
                            x.UserId))
                .ToListAsync(
                    cancellationToken);

        var now =
            DateTime.UtcNow;

        // ========================================================
        // COVERAGE
        //
        // Se usa para ranking, pero en fallback final no se
        // convierte en hard-stop.
        // ========================================================

        var coverages =
            ticket.SiteId.HasValue
                ?
                await _db
                    .HelpdeskSiteCoverages
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.IsActive
                            &&
                            x.SiteId ==
                                ticket.SiteId.Value)
                    .ToListAsync(
                        cancellationToken)
                :
                new List<HelpdeskSiteCoverage>();

        var candidates =
            new List<FallbackCandidate>();

        foreach (var member in members)
        {
            if (
                !users.TryGetValue(
                    member.UserId,
                    out var user))
            {
                continue;
            }

            var currentLoad =
                loads.GetValueOrDefault(
                    member.UserId);

            var memberSchedules =
                schedules
                    .Where(
                        x =>
                            x.TeamId ==
                                member.TeamId
                            &&
                            x.UserId ==
                                member.UserId)
                    .ToList();

            var onDuty =
                memberSchedules.Any(
                    x =>
                        x.IsOnDuty(
                            now));

            var hasSchedule =
                memberSchedules.Count >
                    0;

            var withinCapacity =
                currentLoad <
                member.MaxOpenTickets;

            var teamPosition =
                orderedTeamIds.IndexOf(
                    member.TeamId);

            var coverage =
                ResolveFallbackCoverageRank(
                    member.TeamId,
                    ticket,
                    coverages);

            candidates.Add(
                new FallbackCandidate(
                    TeamId:
                        member.TeamId,

                    UserId:
                        member.UserId,

                    TechnicianName:
                        user.FullName,

                    TeamName:
                        teams
                            .First(
                                x =>
                                    x.Id ==
                                        member.TeamId)
                            .Name,

                    OpenTickets:
                        currentLoad,

                    MaxOpenTickets:
                        member.MaxOpenTickets,

                    OnDuty:
                        onDuty,

                    HasSchedule:
                        hasSchedule,

                    WithinCapacity:
                        withinCapacity,

                    TeamRank:
                        teamPosition < 0
                            ? 9999
                            : teamPosition,

                    CoverageRank:
                        coverage));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // ========================================================
        // RANKING
        //
        // 1. Grupo más apropiado.
        // 2. Cobertura.
        // 3. En turno.
        // 4. Capacidad.
        // 5. Menor porcentaje de ocupación.
        // 6. Menor carga absoluta.
        // ========================================================

        return candidates
            .OrderBy(
                x =>
                    x.TeamRank)
            .ThenBy(
                x =>
                    x.CoverageRank)
            .ThenBy(
                x =>
                    x.OnDuty
                        ? 0
                        : x.HasSchedule
                            ? 1
                            : 2)
            .ThenBy(
                x =>
                    x.WithinCapacity
                        ? 0
                        : 1)
            .ThenBy(
                x =>
                    x.MaxOpenTickets <= 0
                        ? 1d
                        :
                        (double)x.OpenTickets
                        /
                        x.MaxOpenTickets)
            .ThenBy(
                x =>
                    x.OpenTickets)
            .ThenBy(
                x =>
                    x.UserId)
            .First();
    }

    // ============================================================
    // COVERAGE RANK FOR FALLBACK
    // ============================================================

    private static int
        ResolveFallbackCoverageRank(
            Guid teamId,
            HelpdeskTicket ticket,
            IReadOnlyCollection<HelpdeskSiteCoverage> coverages)
    {
        if (!ticket.SiteId.HasValue)
        {
            return 10;
        }

        var teamCoverages =
            coverages
                .Where(
                    x =>
                        x.TeamId ==
                            teamId)
                .ToList();

        if (teamCoverages.Count == 0)
        {
            return 20;
        }

        foreach (var coverage in teamCoverages)
        {
            var locationMatches =
                !coverage.SiteLocationId.HasValue
                ||
                !ticket.SiteLocationId.HasValue
                ||
                coverage.SiteLocationId ==
                    ticket.SiteLocationId;

            var categoryMatches =
                string.IsNullOrWhiteSpace(
                    coverage.Category)
                ||
                string.Equals(
                    coverage.Category,
                    ticket.Category,
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    ticket.Category,
                    "general",
                    StringComparison.OrdinalIgnoreCase);

            if (
                locationMatches
                &&
                categoryMatches)
            {
                return 0;
            }
        }

        return 15;
    }

    // ============================================================
    // FALLBACK ASSIGNMENT
    // ============================================================

    private async Task<bool>
        AssignFallbackCandidateAsync(
            HelpdeskTicket snapshot,
            FallbackCandidate candidate,
            CancellationToken cancellationToken)
    {
        var strategy =
            _db.Database
                .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _db
                        .Database
                        .BeginTransactionAsync(
                            System.Data
                                .IsolationLevel
                                .Serializable,
                            cancellationToken);

                var current =
                    await _db
                        .HelpdeskTickets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.OrganizationId ==
                                    snapshot.OrganizationId
                                &&
                                x.Id ==
                                    snapshot.Id
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed"
                                &&
                                x.Status !=
                                    "pendinguser",
                            cancellationToken);

                if (current is null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }

                var membership =
                    await _db
                        .HelpdeskTeamMembers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.OrganizationId ==
                                    snapshot.OrganizationId
                                &&
                                x.TeamId ==
                                    candidate.TeamId
                                &&
                                x.UserId ==
                                    candidate.UserId
                                &&
                                x.IsAvailable
                                &&
                                x.AcceptsAutomaticAssignments
                                &&
                                x.MaxOpenTickets >
                                    0,
                            cancellationToken);

                if (membership is null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }

                var now =
                    DateTime.UtcNow;

                var currentLoad =
                    await _db
                        .HelpdeskTickets
                        .AsNoTracking()
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    snapshot.OrganizationId
                                &&
                                x.AssigneeUserId ==
                                    candidate.UserId
                                &&
                                x.Id !=
                                    snapshot.Id
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed",
                            cancellationToken);

                var capacityOverride =
                    currentLoad >=
                    membership.MaxOpenTickets;

                var schedules =
                    await _db
                        .Set<HelpdeskTechnicianSchedule>()
                        .AsNoTracking()
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    snapshot.OrganizationId
                                &&
                                x.TeamId ==
                                    candidate.TeamId
                                &&
                                x.UserId ==
                                    candidate.UserId)
                        .ToListAsync(
                            cancellationToken);

                var onDuty =
                    schedules.Any(
                        x =>
                            x.IsOnDuty(
                                now));

                var changed =
                    await _db
                        .HelpdeskTickets
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    snapshot.OrganizationId
                                &&
                                x.Id ==
                                    snapshot.Id
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed"
                                &&
                                x.Status !=
                                    "pendinguser")
                        .ExecuteUpdateAsync(
                            setters =>
                                setters
                                    .SetProperty(
                                        x =>
                                            x.AssigneeUserId,
                                        (Guid?)
                                            candidate.UserId)

                                    .SetProperty(
                                        x =>
                                            x.RequestedTeamId,
                                        (Guid?)
                                            candidate.TeamId)

                                    .SetProperty(
                                        x =>
                                            x.Status,
                                        x =>
                                            x.Status ==
                                                "new"
                                                ? "open"
                                                : x.Status)

                                    .SetProperty(
                                        x =>
                                            x.UpdatedAtUtc,
                                        now),
                            cancellationToken);

                if (changed != 1)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }

                var mode =
                    onDuty
                        ? "fallback"
                        : "fallback-emergencia-fuera-turno";

                var summary =
                    $"Autoasignación {mode}: " +
                    $"{candidate.TechnicianName} / " +
                    $"{candidate.TeamName}. " +
                    $"Categoría {current.Category}. " +
                    $"Carga previa {currentLoad}/{membership.MaxOpenTickets}.";

                if (capacityOverride)
                {
                    summary +=
                        " Capacidad máxima excedida por política de continuidad.";
                }

                _db
                    .HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            snapshot.OrganizationId,
                            snapshot.Id,
                            null,
                            "auto_assigned",
                            TrimFallbackSummary(
                                summary)));

                _db
                    .HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            snapshot.OrganizationId,
                            snapshot.Id,
                            null,
                            "routing_fallback_used",
                            TrimFallbackSummary(
                                "Se utilizó la cadena de fallback porque el routing enterprise no encontró un candidato estricto.")));

                await _db
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                _db.ChangeTracker.Clear();

                return true;
            });
    }

    // ============================================================
    // FALLBACK WAITING
    // ============================================================

    private async Task
        RecordRoutingFallbackWaitingAsync(
            HelpdeskTicket ticket,
            string reason,
            CancellationToken cancellationToken)
    {
        var since =
            DateTime.UtcNow
                .AddMinutes(
                    -30);

        var exists =
            await _db
                .HelpdeskTicketEvents
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.TicketId ==
                            ticket.Id
                        &&
                        x.EventType ==
                            "routing_fallback_waiting"
                        &&
                        x.CreatedAtUtc >=
                            since,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        _db
            .HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    ticket.OrganizationId,
                    ticket.Id,
                    null,
                    "routing_fallback_waiting",
                    TrimFallbackSummary(
                        "Fallback de routing pendiente: "
                        +
                        reason)));

        await _db
            .SaveChangesAsync(
                cancellationToken);
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

    private static string
        NormalizeFallbackText(
            string? value)
    {
        return (
            value
            ??
            string.Empty
        )
        .Trim()
        .ToLowerInvariant()
        .Replace("á", "a")
        .Replace("é", "e")
        .Replace("í", "i")
        .Replace("ó", "o")
        .Replace("ú", "u")
        .Replace("ü", "u")
        .Replace("ñ", "n");
    }

    private static string
        TrimFallbackSummary(
            string value)
    {
        var clean =
            value.Trim();

        return clean.Length <= 500
            ? clean
            : clean[..500];
    }

    // ============================================================
    // INTERNAL MODEL
    // ============================================================

    private sealed record FallbackCandidate(
        Guid TeamId,
        Guid UserId,
        string TechnicianName,
        string TeamName,
        int OpenTickets,
        int MaxOpenTickets,
        bool OnDuty,
        bool HasSchedule,
        bool WithinCapacity,
        int TeamRank,
        int CoverageRank);
}