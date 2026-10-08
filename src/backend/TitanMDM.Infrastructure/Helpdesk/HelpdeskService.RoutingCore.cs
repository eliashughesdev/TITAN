using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // PUBLIC ROUTING PREVIEW
    // ============================================================

    public async Task<RoutingPreview> PreviewRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var result = await EvaluateRoutingAsync(
            organizationId,
            requesterId,
            category,
            cancellationToken);

        return ToPreview(result);
    }

    public async Task<RoutingPreview> PreviewRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        Guid? siteId,
        Guid? siteLocationId,
        Guid? requestedTeamId,
        CancellationToken cancellationToken = default)
    {
        var result = await EvaluateRoutingAsync(
            organizationId,
            requesterId,
            category,
            cancellationToken,
            requestedTeamId,
            siteId,
            siteLocationId);

        return ToPreview(result);
    }

    // ============================================================
    // ROUTING ENGINE
    // ============================================================

    private async Task<RoutingEvaluation> EvaluateRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        CancellationToken cancellationToken,
        Guid? requestedTeamId = null,
        Guid? explicitSiteId = null,
        Guid? explicitSiteLocationId = null)
    {
        var context = await ResolveRoutingContextAsync(
            organizationId,
            requesterId,
            category,
            explicitSiteId,
            explicitSiteLocationId,
            cancellationToken);

        if (!context.Success)
        {
            return RoutingEvaluation.Fail(
                context.FailureReason
                ?? "No fue posible resolver el contexto del solicitante.");
        }

        var teams = await LoadRoutingTeamsAsync(
            organizationId,
            context.NormalizedCategory!,
            requestedTeamId,
            cancellationToken);

        if (teams.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate: null,
                RequesterLocation: context.RequesterLocation,
                SiteId: context.SiteId,
                SiteLocationId: context.SiteLocationId,
                Reason:
                    requestedTeamId.HasValue
                        ? "El grupo seleccionado no puede atender el ticket."
                        : "No existe un grupo activo compatible con el ticket.");
        }

        var coverages = await LoadRankedCoveragesAsync(
            organizationId,
            context.SiteId!.Value,
            context.SiteLocationId,
            context.NormalizedCategory!,
            teams.Keys.ToArray(),
            cancellationToken);

        if (coverages.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                context.RequesterLocation,
                context.SiteId,
                context.SiteLocationId,
                "No existe cobertura compatible con la localidad, sublocalidad y categoría del ticket.");
        }

        var usableCoverages =
            SelectBestCoverageTier(coverages);

        var eligibleTechnicianIds =
            await EligibleTechnicians(organizationId)
                .ToListAsync(cancellationToken);

        if (eligibleTechnicianIds.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                context.RequesterLocation,
                context.SiteId,
                context.SiteLocationId,
                "No existen técnicos con permisos operativos de Mesa de Ayuda.");
        }

        var coveredTeamIds =
            usableCoverages
                .Select(x => x.Coverage.TeamId)
                .Distinct()
                .ToArray();

        var members = await LoadAvailableMembersAsync(
            organizationId,
            coveredTeamIds,
            eligibleTechnicianIds,
            cancellationToken);

        if (members.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                context.RequesterLocation,
                context.SiteId,
                context.SiteLocationId,
                "Existe cobertura, pero no hay técnicos habilitados que acepten autoasignación.");
        }

        var scheduleResult =
            await FilterMembersByScheduleAsync(
                organizationId,
                members,
                cancellationToken);

        if (scheduleResult.Members.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                context.RequesterLocation,
                context.SiteId,
                context.SiteLocationId,
                "Existe cobertura y técnicos habilitados, pero ninguno está disponible según su turno.");
        }

        var candidates =
            await BuildRankedCandidatesAsync(
                organizationId,
                context.SiteName!,
                teams,
                usableCoverages,
                scheduleResult.Members,
                scheduleResult.SchedulePriorities,
                cancellationToken);

        var chosen =
            candidates
                .OrderBy(x => x.CoverageRank)
                .ThenBy(x => x.CoveragePriority)
                .ThenBy(x => x.SchedulePriority)
                .ThenBy(x => x.Occupancy)
                .ThenBy(x => x.Candidate.OpenTickets)

                // Técnico nunca utilizado primero.
                .ThenBy(
                    x =>
                        x.LastAutomaticAssignmentAtUtc.HasValue
                            ? 1
                            : 0)

                // Después quien lleve más tiempo sin recibir ticket.
                .ThenBy(x => x.LastAutomaticAssignmentAtUtc)

                // Desempate estable.
                .ThenBy(x => x.Candidate.UserId)

                .FirstOrDefault();

        if (chosen is null)
        {
            return new RoutingEvaluation(
                null,
                context.RequesterLocation,
                context.SiteId,
                context.SiteLocationId,
                "Todos los técnicos elegibles alcanzaron su capacidad máxima.");
        }

        var selected = chosen.Candidate;

        return new RoutingEvaluation(
            Candidate: selected,
            RequesterLocation: context.RequesterLocation,
            SiteId: context.SiteId,
            SiteLocationId: context.SiteLocationId,
            Reason:
                BuildRoutingReason(
                    context.RequesterLocation!,
                    context.NormalizedCategory!,
                    selected));
    }

    // ============================================================
    // REQUESTER / LOCATION CONTEXT
    // ============================================================

    private async Task<RoutingContext> ResolveRoutingContextAsync(
        Guid organizationId,
        Guid requesterId,
        string? category,
        Guid? explicitSiteId,
        Guid? explicitSiteLocationId,
        CancellationToken cancellationToken)
    {
        var requester =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == requesterId
                        && x.OrganizationId == organizationId
                        && x.IsActive,
                    cancellationToken);

        if (requester is null)
        {
            return RoutingContext.Fail(
                "El solicitante no está activo en esta organización.");
        }

        /*
         * Prioridad:
         *
         * 1. Site inferido/asignado directamente al ticket.
         * 2. Site del solicitante.
         */
        var effectiveSiteId =
            explicitSiteId
            ?? requester.SiteId;

        var effectiveLocationId =
            explicitSiteId.HasValue
                ? explicitSiteLocationId
                : requester.SiteLocationId;

        if (!effectiveSiteId.HasValue)
        {
            return RoutingContext.Fail(
                "El solicitante no tiene una localidad asignada.");
        }

        var site =
            await _db.Sites
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        && x.Id == effectiveSiteId.Value
                        && x.IsActive,
                    cancellationToken);

        if (site is null)
        {
            return RoutingContext.Fail(
                "La localidad del ticket no existe o está desactivada.");
        }

        SiteLocation? siteLocation = null;

        if (effectiveLocationId.HasValue)
        {
            siteLocation =
                await _db.SiteLocations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId == organizationId
                            && x.SiteId == site.Id
                            && x.Id == effectiveLocationId.Value
                            && x.IsActive,
                        cancellationToken);

            if (siteLocation is null)
            {
                effectiveLocationId = null;
            }
        }

        var requesterLocation =
            siteLocation is null
                ? site.Name
                : $"{site.Name} / {siteLocation.Name}";

        return RoutingContext.Ok(
            site.Id,
            effectiveLocationId,
            site.Name,
            requesterLocation,
            NormalizeRoutingCategory(category));
    }

    // ============================================================
    // TEAMS
    // ============================================================

    private async Task<Dictionary<Guid, HelpdeskTeam>>
        LoadRoutingTeamsAsync(
            Guid organizationId,
            string normalizedCategory,
            Guid? requestedTeamId,
            CancellationToken cancellationToken)
    {
        var query =
            _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId
                        && x.IsActive);

        if (requestedTeamId.HasValue)
        {
            query =
                query.Where(
                    x =>
                        x.Id == requestedTeamId.Value);
        }

        var teams =
            await query.ToListAsync(cancellationToken);

        /*
         * GENERAL significa:
         *
         * "todavía no clasificado con suficiente precisión".
         *
         * No debe excluir grupos especializados.
         */
        return teams
            .Where(
                x =>
                    normalizedCategory == "general"
                    || string.IsNullOrWhiteSpace(x.Categories)
                    || x.HandlesCategory(normalizedCategory))
            .ToDictionary(x => x.Id);
    }

    // ============================================================
    // COVERAGE
    // ============================================================

    private async Task<List<RankedCoverage>>
        LoadRankedCoveragesAsync(
            Guid organizationId,
            Guid siteId,
            Guid? siteLocationId,
            string normalizedCategory,
            Guid[] teamIds,
            CancellationToken cancellationToken)
    {
        var coverages =
            await _db.HelpdeskSiteCoverages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId
                        && x.IsActive
                        && x.SiteId == siteId
                        && teamIds.Contains(x.TeamId))
                .ToListAsync(cancellationToken);

        var isGeneral =
            normalizedCategory == "general";

        return coverages
            .Select(
                coverage =>
                {
                    var exactCategory =
                        !isGeneral
                        && string.Equals(
                            coverage.Category,
                            normalizedCategory,
                            StringComparison.OrdinalIgnoreCase);

                    /*
                     * GENERAL todavía no conoce suficientemente
                     * la categoría. No eliminamos coberturas
                     * especializadas en esta etapa.
                     */
                    var anyCategory =
                        isGeneral
                        || string.IsNullOrWhiteSpace(
                            coverage.Category);

                    var exactLocation =
                        siteLocationId.HasValue
                        && coverage.SiteLocationId
                            == siteLocationId.Value;

                    var entireSite =
                        !coverage.SiteLocationId.HasValue;

                    var rank =
                        exactLocation && exactCategory
                            ? 0
                            : exactLocation && anyCategory
                                ? 1
                                : entireSite && exactCategory
                                    ? 2
                                    : entireSite && anyCategory
                                        ? 3
                                        : int.MaxValue;

                    return new RankedCoverage(
                        coverage,
                        rank);
                })
            .Where(x => x.Rank != int.MaxValue)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Coverage.Priority)
            .ToList();
    }

    private static List<RankedCoverage>
        SelectBestCoverageTier(
            IReadOnlyList<RankedCoverage> coverages)
    {
        if (coverages.Count == 0)
        {
            return [];
        }

        var bestRank =
            coverages[0].Rank;

        var bestPriority =
            coverages
                .Where(x => x.Rank == bestRank)
                .Min(x => x.Coverage.Priority);

        return coverages
            .Where(
                x =>
                    x.Rank == bestRank
                    && x.Coverage.Priority == bestPriority)
            .ToList();
    }

    // ============================================================
    // MEMBERS
    // ============================================================

    private async Task<List<HelpdeskTeamMember>>
        LoadAvailableMembersAsync(
            Guid organizationId,
            Guid[] coveredTeamIds,
            IReadOnlyCollection<Guid> eligibleTechnicianIds,
            CancellationToken cancellationToken)
    {
        return await _db.HelpdeskTeamMembers
            .AsNoTracking()
            .Where(
                x =>
                    x.OrganizationId == organizationId
                    && coveredTeamIds.Contains(x.TeamId)
                    && eligibleTechnicianIds.Contains(x.UserId)
                    && x.IsAvailable
                    && x.AcceptsAutomaticAssignments
                    && x.MaxOpenTickets > 0)
            .ToListAsync(cancellationToken);
    }

    // ============================================================
    // SCHEDULES
    // ============================================================

    private async Task<ScheduleFilterResult>
        FilterMembersByScheduleAsync(
            Guid organizationId,
            IReadOnlyList<HelpdeskTeamMember> members,
            CancellationToken cancellationToken)
    {
        var teamIds =
            members
                .Select(x => x.TeamId)
                .Distinct()
                .ToArray();

        var userIds =
            members
                .Select(x => x.UserId)
                .Distinct()
                .ToArray();

        var schedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId
                        && teamIds.Contains(x.TeamId)
                        && userIds.Contains(x.UserId))
                .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var availableMembers =
            new List<HelpdeskTeamMember>();

        var priorities =
            new Dictionary<(Guid TeamId, Guid UserId), int>();

        foreach (var member in members)
        {
            var technicianSchedules =
                schedules
                    .Where(
                        x =>
                            x.TeamId == member.TeamId
                            && x.UserId == member.UserId)
                    .ToList();

            /*
             * Si no existe horario:
             * IsAvailable gobierna.
             *
             * Si existe horario:
             * debe estar dentro de un slot activo.
             */
            if (technicianSchedules.Count == 0)
            {
                availableMembers.Add(member);

                priorities[
                    (
                        member.TeamId,
                        member.UserId
                    )] = 1000;

                continue;
            }

            var activeSchedule =
                technicianSchedules
                    .Where(x => x.IsOnDuty(now))
                    .OrderBy(x => x.Priority)
                    .FirstOrDefault();

            if (activeSchedule is null)
            {
                continue;
            }

            availableMembers.Add(member);

            priorities[
                (
                    member.TeamId,
                    member.UserId
                )] =
                activeSchedule.Priority;
        }

        return new ScheduleFilterResult(
            availableMembers,
            priorities);
    }

    // ============================================================
    // CANDIDATES + FAIRNESS
    // ============================================================

    private async Task<List<RankedCandidate>>
        BuildRankedCandidatesAsync(
            Guid organizationId,
            string requesterSiteName,
            IReadOnlyDictionary<Guid, HelpdeskTeam> teams,
            IReadOnlyList<RankedCoverage> usableCoverages,
            IReadOnlyList<HelpdeskTeamMember> members,
            IReadOnlyDictionary<
                (Guid TeamId, Guid UserId),
                int> schedulePriorities,
            CancellationToken cancellationToken)
    {
        var technicianIds =
            members
                .Select(x => x.UserId)
                .Distinct()
                .ToArray();

        var technicians =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId
                        && x.IsActive
                        && technicianIds.Contains(x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    cancellationToken);

        if (technicians.Count == 0)
        {
            return [];
        }

        var loads =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId
                        && x.AssigneeUserId.HasValue
                        && technicianIds.Contains(
                            x.AssigneeUserId.Value)
                        && x.Status != "resolved"
                        && x.Status != "closed")
                .GroupBy(
                    x =>
                        x.AssigneeUserId!.Value)
                .Select(
                    group =>
                        new
                        {
                            UserId = group.Key,
                            Count = group.Count()
                        })
                .ToDictionaryAsync(
                    x => x.UserId,
                    x => x.Count,
                    cancellationToken);

        /*
         * La última autoasignación permite round-robin/fairness
         * cuando varios técnicos tienen igual carga.
         */
        var lastAssignments =
            await (
                from ticketEvent
                    in _db.HelpdeskTicketEvents.AsNoTracking()

                join ticket
                    in _db.HelpdeskTickets.AsNoTracking()
                    on ticketEvent.TicketId
                    equals ticket.Id

                where
                    ticketEvent.OrganizationId == organizationId
                    && ticket.OrganizationId == organizationId
                    && ticketEvent.EventType == "auto_assigned"
                    && ticket.AssigneeUserId.HasValue
                    && technicianIds.Contains(
                        ticket.AssigneeUserId.Value)

                group ticketEvent
                    by ticket.AssigneeUserId!.Value
                    into technicianGroup

                select new
                {
                    UserId = technicianGroup.Key,

                    LastAssignedAtUtc =
                        technicianGroup.Max(
                            x =>
                                x.CreatedAtUtc)
                }
            )
            .ToDictionaryAsync(
                x => x.UserId,
                x => x.LastAssignedAtUtc,
                cancellationToken);

        var technicianSiteIds =
            technicians.Values
                .Where(x => x.SiteId.HasValue)
                .Select(x => x.SiteId!.Value)
                .Distinct()
                .ToArray();

        var technicianLocationIds =
            technicians.Values
                .Where(x => x.SiteLocationId.HasValue)
                .Select(x => x.SiteLocationId!.Value)
                .Distinct()
                .ToArray();

        var coverageLocationIds =
            usableCoverages
                .Where(
                    x =>
                        x.Coverage.SiteLocationId
                            .HasValue)
                .Select(
                    x =>
                        x.Coverage
                            .SiteLocationId!
                            .Value)
                .Distinct()
                .ToArray();

        var technicianSiteNames =
            technicianSiteIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.Sites
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId == organizationId
                            && technicianSiteIds.Contains(x.Id))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x.Name,
                        cancellationToken);

        var technicianLocationNames =
            technicianLocationIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId == organizationId
                            && technicianLocationIds.Contains(x.Id))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x.Name,
                        cancellationToken);

        var coverageLocationNames =
            coverageLocationIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId == organizationId
                            && coverageLocationIds.Contains(x.Id))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x.Name,
                        cancellationToken);

        var candidates =
            new List<RankedCandidate>();

        foreach (var member in members)
        {
            if (!technicians.TryGetValue(
                    member.UserId,
                    out var user))
            {
                continue;
            }

            var openTickets =
                loads.GetValueOrDefault(
                    member.UserId);

            if (openTickets >= member.MaxOpenTickets)
            {
                continue;
            }

            var coverage =
                usableCoverages
                    .Where(
                        x =>
                            x.Coverage.TeamId
                            == member.TeamId)
                    .OrderBy(
                        x =>
                            x.Coverage.Priority)
                    .FirstOrDefault();

            if (coverage is null)
            {
                continue;
            }

            if (!teams.TryGetValue(
                    member.TeamId,
                    out var team))
            {
                continue;
            }

            var schedulePriority =
                schedulePriorities
                    .GetValueOrDefault(
                        (
                            member.TeamId,
                            member.UserId
                        ),
                        1000);

            var candidate =
                new RoutingCandidate(
                    TeamId:
                        member.TeamId,

                    UserId:
                        member.UserId,

                    UserName:
                        user.FullName,

                    TeamName:
                        team.Name,

                    CoverageLocation:
                        ResolveCoverageLocationName(
                            requesterSiteName,
                            coverage.Coverage,
                            coverageLocationNames),

                    TechnicianLocation:
                        ResolveUserLocationName(
                            user,
                            technicianSiteNames,
                            technicianLocationNames),

                    OpenTickets:
                        openTickets,

                    Capacity:
                        member.MaxOpenTickets,

                    SchedulePriority:
                        schedulePriority,

                    CoveragePriority:
                        coverage.Coverage.Priority);

            var occupancy =
                member.MaxOpenTickets <= 0
                    ? 1d
                    : (double)openTickets
                      / member.MaxOpenTickets;

            candidates.Add(
                new RankedCandidate(
                    Candidate:
                        candidate,

                    CoverageRank:
                        coverage.Rank,

                    CoveragePriority:
                        coverage.Coverage.Priority,

                    SchedulePriority:
                        schedulePriority,

                    Occupancy:
                        occupancy,

                    LastAutomaticAssignmentAtUtc:
                        lastAssignments.GetValueOrDefault(
                            member.UserId)));
        }

        return candidates;
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string NormalizeRoutingCategory(
        string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "general";
        }

        return category
            .Trim()
            .ToLowerInvariant();
    }

    private static string ResolveCoverageLocationName(
        string siteName,
        HelpdeskSiteCoverage coverage,
        IReadOnlyDictionary<Guid, string> locations)
    {
        if (!coverage.SiteLocationId.HasValue)
        {
            return siteName;
        }

        return locations.TryGetValue(
            coverage.SiteLocationId.Value,
            out var locationName)
                ? $"{siteName} / {locationName}"
                : siteName;
    }

    private static string ResolveUserLocationName(
        User user,
        IReadOnlyDictionary<Guid, string> sites,
        IReadOnlyDictionary<Guid, string> locations)
    {
        if (!user.SiteId.HasValue)
        {
            return "Sin localidad";
        }

        if (!sites.TryGetValue(
                user.SiteId.Value,
                out var siteName))
        {
            siteName =
                "Localidad desconocida";
        }

        if (!user.SiteLocationId.HasValue)
        {
            return siteName;
        }

        return locations.TryGetValue(
            user.SiteLocationId.Value,
            out var locationName)
                ? $"{siteName} / {locationName}"
                : siteName;
    }

    private static string BuildRoutingReason(
        string requesterLocation,
        string category,
        RoutingCandidate candidate)
    {
        return
            $"Localidad {requesterLocation}; " +
            $"categoría {category}; " +
            $"grupo {candidate.TeamName}; " +
            $"cobertura {candidate.CoverageLocation}; " +
            $"técnico {candidate.UserName}; " +
            $"ubicación técnico {candidate.TechnicianLocation}; " +
            $"carga {candidate.OpenTickets}/{candidate.Capacity}; " +
            $"prioridad cobertura {candidate.CoveragePriority}; " +
            $"prioridad turno {candidate.SchedulePriority}. " +
            "Cobertura, permisos, disponibilidad, turno y capacidad validados.";
    }

    private static string TrimSummary(
        string value)
    {
        var clean =
            value.Trim();

        return clean[
            ..Math.Min(
                clean.Length,
                500)];
    }

    private static RoutingPreview ToPreview(
        RoutingEvaluation result)
    {
        var candidate =
            result.Candidate;

        return new RoutingPreview(
            CanAssign:
                candidate is not null,

            Reason:
                result.Reason,

            RequesterZone:
                result.RequesterLocation,

            SiteId:
                result.SiteId,

            SiteLocationId:
                result.SiteLocationId,

            TeamId:
                candidate?.TeamId,

            TechnicianId:
                candidate?.UserId,

            TechnicianName:
                candidate?.UserName,

            TeamName:
                candidate?.TeamName,

            CoverageZone:
                candidate?.CoverageLocation,

            TechnicianZone:
                candidate?.TechnicianLocation,

            OpenTickets:
                candidate?.OpenTickets,

            Capacity:
                candidate?.Capacity);
    }

    // ============================================================
    // INTERNAL TYPES
    // ============================================================

    private sealed record RoutingContext(
        bool Success,
        Guid? SiteId,
        Guid? SiteLocationId,
        string? SiteName,
        string? RequesterLocation,
        string? NormalizedCategory,
        string? FailureReason)
    {
        public static RoutingContext Ok(
            Guid siteId,
            Guid? siteLocationId,
            string siteName,
            string requesterLocation,
            string normalizedCategory)
        {
            return new RoutingContext(
                true,
                siteId,
                siteLocationId,
                siteName,
                requesterLocation,
                normalizedCategory,
                null);
        }

        public static RoutingContext Fail(
            string reason)
        {
            return new RoutingContext(
                false,
                null,
                null,
                null,
                null,
                null,
                reason);
        }
    }

    private sealed record ScheduleFilterResult(
        List<HelpdeskTeamMember> Members,
        Dictionary<
            (Guid TeamId, Guid UserId),
            int> SchedulePriorities);

    private sealed record RoutingCandidate(
        Guid TeamId,
        Guid UserId,
        string UserName,
        string TeamName,
        string CoverageLocation,
        string TechnicianLocation,
        int OpenTickets,
        int Capacity,
        int SchedulePriority,
        int CoveragePriority);

    private sealed record RoutingEvaluation(
        RoutingCandidate? Candidate,
        string? RequesterLocation,
        Guid? SiteId,
        Guid? SiteLocationId,
        string Reason)
    {
        public static RoutingEvaluation Fail(
            string reason)
        {
            return new RoutingEvaluation(
                null,
                null,
                null,
                null,
                reason);
        }
    }

    private sealed record RankedCoverage(
        HelpdeskSiteCoverage Coverage,
        int Rank);

    private sealed record RankedCandidate(
        RoutingCandidate Candidate,
        int CoverageRank,
        int CoveragePriority,
        int SchedulePriority,
        double Occupancy,
        DateTime? LastAutomaticAssignmentAtUtc);

    public sealed record RoutingPreview(
        bool CanAssign,
        string Reason,
        string? RequesterZone,
        Guid? SiteId,
        Guid? SiteLocationId,
        Guid? TeamId,
        Guid? TechnicianId,
        string? TechnicianName,
        string? TeamName,
        string? CoverageZone,
        string? TechnicianZone,
        int? OpenTickets,
        int? Capacity);
}