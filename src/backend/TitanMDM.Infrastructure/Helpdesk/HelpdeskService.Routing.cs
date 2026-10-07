using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // HELPDESK TECHNICIAN PERMISSIONS
    // ============================================================

    private static readonly string[] TechnicianPermissions =
    [
        // Nuevo RBAC.
        "helpdesk.agent.access",
        "helpdesk.ticket.details.view",
        "helpdesk.ticket.comment",
        "helpdesk.ticket.take",
        "helpdesk.ticket.assign",
        "helpdesk.ticket.transition",
        "helpdesk.ticket.resolve",
        "helpdesk.ticket.close",

        // Compatibilidad legacy temporal.
        "tickets.comment",
        "tickets.assign",
        "tickets.close",
        "helpdesk.view",
        "helpdesk.manage"
    ];

    // ============================================================
    // ELIGIBLE TECHNICIANS
    // ============================================================

    private IQueryable<Guid> EligibleTechnicians(
        Guid organizationId)
    {
        return
            (
                from userRole
                    in _db.UserRoles.AsNoTracking()

                join role
                    in _db.Roles.AsNoTracking()
                    on userRole.RoleId
                    equals role.Id

                join rolePermission
                    in _db.RolePermissions.AsNoTracking()
                    on role.Id
                    equals rolePermission.RoleId

                join permission
                    in _db.Permissions.AsNoTracking()
                    on rolePermission.PermissionId
                    equals permission.Id

                join user
                    in _db.Users.AsNoTracking()
                    on userRole.UserId
                    equals user.Id

                where
                    user.OrganizationId == organizationId
                    &&
                    role.OrganizationId == organizationId
                    &&
                    user.IsActive
                    &&
                    role.IsActive
                    &&
                    permission.IsActive
                    &&
                    TechnicianPermissions.Contains(
                        permission.Code)

                select user.Id
            )
            .Distinct();
    }

    // ============================================================
    // PUBLIC ROUTING PREVIEW
    // ============================================================

    public async Task<RoutingPreview> PreviewRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var result =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken);

        return ToPreview(
            result);
    }

    // ============================================================
    // EXTENDED ROUTING PREVIEW
    //
    // Allows administration/testing screens to validate an
    // explicit Site/SiteLocation without modifying the requester.
    // ============================================================

    public async Task<RoutingPreview> PreviewRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        Guid? siteId,
        Guid? siteLocationId,
        Guid? requestedTeamId,
        CancellationToken cancellationToken = default)
    {
        var result =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken,
                requestedTeamId,
                siteId,
                siteLocationId);

        return ToPreview(
            result);
    }

    // ============================================================
    // AUTO ASSIGN - COMPATIBLE SIGNATURE
    // ============================================================

    private async Task<RoutingCandidate?>
        FindAutomaticAssigneeAsync(
            Guid organizationId,
            Guid requesterId,
            string category,
            CancellationToken cancellationToken)
    {
        var result =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken);

        return result.Candidate;
    }

    // ============================================================
    // AUTO ASSIGN - TICKET SITE AWARE
    // ============================================================

    private async Task<RoutingCandidate?>
        FindAutomaticAssigneeAsync(
            Guid organizationId,
            Guid requesterId,
            string category,
            Guid? siteId,
            Guid? siteLocationId,
            Guid? requestedTeamId,
            CancellationToken cancellationToken)
    {
        var result =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken,
                requestedTeamId,
                siteId,
                siteLocationId);

        return result.Candidate;
    }

    // ============================================================
    // ROUTING ENGINE
    // ============================================================

    private async Task<RoutingEvaluation>
        EvaluateRoutingAsync(
            Guid organizationId,
            Guid requesterId,
            string category,
            CancellationToken cancellationToken,
            Guid? requestedTeamId = null,
            Guid? explicitSiteId = null,
            Guid? explicitSiteLocationId = null)
    {
        // ========================================================
        // REQUESTER
        // ========================================================

        var requester =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == requesterId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (requester is null)
        {
            return RoutingEvaluation.Fail(
                "El solicitante no está activo en esta organización.");
        }

        // ========================================================
        // SITE RESOLUTION
        //
        // Priority:
        // 1. Explicit ticket/device Site.
        // 2. Requester Site.
        // ========================================================

        var effectiveSiteId =
            explicitSiteId
            ??
            requester.SiteId;

        var effectiveLocationId =
            explicitSiteId.HasValue
                ? explicitSiteLocationId
                : requester.SiteLocationId;

        if (!effectiveSiteId.HasValue)
        {
            return RoutingEvaluation.Fail(
                "El solicitante no tiene una localidad asignada.");
        }

        var site =
            await _db.Sites
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            effectiveSiteId.Value
                        &&
                        x.IsActive,
                    cancellationToken);

        if (site is null)
        {
            return RoutingEvaluation.Fail(
                "La localidad del ticket no existe o está desactivada.");
        }

        SiteLocation? siteLocation =
            null;

        if (effectiveLocationId.HasValue)
        {
            siteLocation =
                await _db.SiteLocations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.SiteId ==
                                site.Id
                            &&
                            x.Id ==
                                effectiveLocationId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            /*
             * Never use a SiteLocation belonging to another Site.
             */
            if (siteLocation is null)
            {
                effectiveLocationId =
                    null;
            }
        }

        var requesterLocation =
            siteLocation is null
                ? site.Name
                : $"{site.Name} / {siteLocation.Name}";

        // ========================================================
        // CATEGORY
        // ========================================================

        var normalizedCategory =
            NormalizeRoutingCategory(
                category);

        // ========================================================
        // TEAMS
        // ========================================================

        var teamQuery =
            _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive);

        if (requestedTeamId.HasValue)
        {
            teamQuery =
                teamQuery.Where(
                    x =>
                        x.Id ==
                            requestedTeamId.Value);
        }

        var allTeams =
            await teamQuery
                .ToListAsync(
                    cancellationToken);

        var teams =
            allTeams
                .Where(
                    x =>
                        x.HandlesCategory(
                            normalizedCategory)
                        ||
                        (
                            normalizedCategory ==
                                "general"
                            &&
                            string.IsNullOrWhiteSpace(
                                x.Categories)
                        ))
                .ToDictionary(
                    x => x.Id);

        if (teams.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate: null,
                RequesterLocation:
                    requesterLocation,
                SiteId:
                    site.Id,
                SiteLocationId:
                    effectiveLocationId,
                Reason:
                    requestedTeamId.HasValue
                        ? "El grupo seleccionado no atiende esta categoría."
                        : "No existe un grupo activo que atienda esta categoría.");
        }

        var teamIds =
            teams.Keys
                .ToArray();

        // ========================================================
        // SITE COVERAGE
        // ========================================================

        var coverages =
            await _db.HelpdeskSiteCoverages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        x.SiteId ==
                            site.Id
                        &&
                        teamIds.Contains(
                            x.TeamId))
                .ToListAsync(
                    cancellationToken);

        if (coverages.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "Ningún grupo posee cobertura activa para esta localidad.");
        }

        // ========================================================
        // COVERAGE RANK
        //
        // 0 exact location + exact category
        // 1 exact location + all categories
        // 2 entire site    + exact category
        // 3 entire site    + all categories
        // ========================================================

        var rankedCoverages =
            coverages
                .Select(
                    coverage =>
                    {
                        var exactCategory =
                            string.Equals(
                                coverage.Category,
                                normalizedCategory,
                                StringComparison
                                    .OrdinalIgnoreCase);

                        var anyCategory =
                            string.IsNullOrWhiteSpace(
                                coverage.Category);

                        var exactLocation =
                            effectiveLocationId.HasValue
                            &&
                            coverage.SiteLocationId ==
                                effectiveLocationId;

                        var entireSite =
                            !coverage
                                .SiteLocationId
                                .HasValue;

                        var rank =
                            exactLocation &&
                            exactCategory
                                ? 0
                                :
                            exactLocation &&
                            anyCategory
                                ? 1
                                :
                            entireSite &&
                            exactCategory
                                ? 2
                                :
                            entireSite &&
                            anyCategory
                                ? 3
                                :
                            int.MaxValue;

                        return new RankedCoverage(
                            coverage,
                            rank);
                    })
                .Where(
                    x =>
                        x.Rank !=
                            int.MaxValue)
                .OrderBy(
                    x =>
                        x.Rank)
                .ThenBy(
                    x =>
                        x.Coverage.Priority)
                .ToList();

        if (rankedCoverages.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "Existe cobertura para la localidad, pero no coincide con la ubicación o categoría del ticket.");
        }

        /*
         * Only use the most specific coverage tier.
         *
         * Within that tier, Priority decides which groups win.
         */
        var bestCoverageRank =
            rankedCoverages[0]
                .Rank;

        var bestPriority =
            rankedCoverages
                .Where(
                    x =>
                        x.Rank ==
                            bestCoverageRank)
                .Min(
                    x =>
                        x.Coverage.Priority);

        var usableCoverages =
            rankedCoverages
                .Where(
                    x =>
                        x.Rank ==
                            bestCoverageRank
                        &&
                        x.Coverage.Priority ==
                            bestPriority)
                .ToList();

        var coveredTeamIds =
            usableCoverages
                .Select(
                    x =>
                        x.Coverage.TeamId)
                .Distinct()
                .ToArray();

        // ========================================================
        // ELIGIBLE TECHNICIANS BY RBAC
        // ========================================================

        var eligibleTechnicianIds =
            await EligibleTechnicians(
                    organizationId)
                .ToListAsync(
                    cancellationToken);

        if (eligibleTechnicianIds.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "No existen técnicos con permisos operativos de Mesa de Ayuda.");
        }

        // ========================================================
        // TEAM MEMBERS
        // ========================================================

        var members =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        coveredTeamIds.Contains(
                            x.TeamId)
                        &&
                        eligibleTechnicianIds.Contains(
                            x.UserId)
                        &&
                        x.IsAvailable
                        &&
                        x.AcceptsAutomaticAssignments
                        &&
                        x.MaxOpenTickets > 0)
                .ToListAsync(
                    cancellationToken);

        if (members.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "Existe cobertura, pero no hay técnicos habilitados que acepten autoasignación.");
        }

        // ========================================================
        // TECHNICIAN SCHEDULES
        //
        // Rule:
        // - No schedule configured: IsAvailable controls state.
        // - Schedule configured: at least one active slot required.
        // ========================================================

        var memberTeamIds =
            members
                .Select(
                    x =>
                        x.TeamId)
                .Distinct()
                .ToArray();

        var memberUserIds =
            members
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToArray();

        var schedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        memberTeamIds.Contains(
                            x.TeamId)
                        &&
                        memberUserIds.Contains(
                            x.UserId))
                .ToListAsync(
                    cancellationToken);

        var now =
            DateTime.UtcNow;

        var schedulePriorities =
            new Dictionary<
                (Guid TeamId, Guid UserId),
                int>();

        var availableMembers =
            new List<HelpdeskTeamMember>();

        foreach (var member in members)
        {
            var technicianSchedules =
                schedules
                    .Where(
                        x =>
                            x.TeamId ==
                                member.TeamId
                            &&
                            x.UserId ==
                                member.UserId)
                    .ToList();

           /*
 * Enterprise mode:
 *
 * Un técnico sin horario configurado NO puede
 * recibir asignaciones automáticas.
 *
 * Sigue pudiendo recibir asignaciones manuales.
 */
if (technicianSchedules.Count == 0)
{
    continue;
}

            var activeSchedule =
                technicianSchedules
                    .Where(
                        x =>
                            x.IsOnDuty(
                                now))
                    .OrderBy(
                        x =>
                            x.Priority)
                    .FirstOrDefault();

            if (activeSchedule is null)
            {
                continue;
            }

            availableMembers.Add(
                member);

            schedulePriorities[
                (
                    member.TeamId,
                    member.UserId
                )] =
                activeSchedule.Priority;
        }

        members =
            availableMembers;

        if (members.Count == 0)
{
    return new RoutingEvaluation(
        null,
        requesterLocation,
        site.Id,
        effectiveLocationId,
        "Existe cobertura y técnicos habilitados, pero ninguno posee un turno activo para este momento.");
}

        // ========================================================
        // TECHNICIAN USERS
        // ========================================================

        var technicianIds =
            members
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToArray();

        var technicians =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        technicianIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        if (technicians.Count == 0)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "No existen cuentas activas para los técnicos configurados.");
        }

        // ========================================================
        // CURRENT WORKLOAD
        // ========================================================

        var loads =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.AssigneeUserId
                            .HasValue
                        &&
                        technicianIds.Contains(
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
                    group =>
                        new
                        {
                            UserId =
                                group.Key,

                            Count =
                                group.Count()
                        })
                .ToDictionaryAsync(
                    x =>
                        x.UserId,
                    x =>
                        x.Count,
                    cancellationToken);

        // ========================================================
        // TECHNICIAN LOCATION NAMES
        // ========================================================

        var technicianSiteIds =
            technicians.Values
                .Where(
                    x =>
                        x.SiteId.HasValue)
                .Select(
                    x =>
                        x.SiteId!.Value)
                .Distinct()
                .ToArray();

        var technicianLocationIds =
            technicians.Values
                .Where(
                    x =>
                        x.SiteLocationId
                            .HasValue)
                .Select(
                    x =>
                        x.SiteLocationId!.Value)
                .Distinct()
                .ToArray();

        var technicianSiteNames =
            technicianSiteIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.Sites
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            technicianSiteIds.Contains(
                                x.Id))
                    .ToDictionaryAsync(
                        x =>
                            x.Id,
                        x =>
                            x.Name,
                        cancellationToken);

        var technicianLocationNames =
            technicianLocationIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            technicianLocationIds.Contains(
                                x.Id))
                    .ToDictionaryAsync(
                        x =>
                            x.Id,
                        x =>
                            x.Name,
                        cancellationToken);

        // ========================================================
        // COVERAGE LOCATION NAMES
        // ========================================================

        var coverageLocationIds =
            usableCoverages
                .Where(
                    x =>
                        x.Coverage
                            .SiteLocationId
                            .HasValue)
                .Select(
                    x =>
                        x.Coverage
                            .SiteLocationId!
                            .Value)
                .Distinct()
                .ToArray();

        var coverageLocationNames =
            coverageLocationIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            coverageLocationIds.Contains(
                                x.Id))
                    .ToDictionaryAsync(
                        x =>
                            x.Id,
                        x =>
                            x.Name,
                        cancellationToken);
        
        // ========================================================
// LAST AUTOMATIC ASSIGNMENT
//
// Used as a fairness tie breaker.
// The technician who has gone the longest without
// receiving an automatic ticket is preferred.
// ========================================================

var lastAssignments =
    await (
        from ticketEvent
            in _db.HelpdeskTicketEvents
                .AsNoTracking()

        join ticket
            in _db.HelpdeskTickets
                .AsNoTracking()
            on ticketEvent.TicketId
            equals ticket.Id

        where
            ticketEvent.OrganizationId ==
                organizationId
            &&
            ticket.OrganizationId ==
                organizationId
            &&
            ticketEvent.EventType ==
                "auto_assigned"
            &&
            ticket.AssigneeUserId.HasValue
            &&
            technicianIds.Contains(
                ticket.AssigneeUserId.Value)

        group ticketEvent
            by ticket.AssigneeUserId!.Value
            into technicianGroup

        select new
        {
            UserId =
                technicianGroup.Key,

            LastAssignedAtUtc =
                technicianGroup.Max(
                    x =>
                        x.CreatedAtUtc)
        }
    )
    .ToDictionaryAsync(
        x =>
            x.UserId,

        x =>
            x.LastAssignedAtUtc,

        cancellationToken);

        // ========================================================
        // CANDIDATE RANKING
        // ========================================================

        var candidates =
            new List<RankedCandidate>();

        foreach (var member in members)
        {
            if (
                !technicians.TryGetValue(
                    member.UserId,
                    out var user))
            {
                continue;
            }

            var openTickets =
                loads.GetValueOrDefault(
                    member.UserId);

            /*
             * Hard capacity guard.
             */
            if (
                openTickets >=
                    member.MaxOpenTickets)
            {
                continue;
            }

            var coverage =
                usableCoverages
                    .Where(
                        x =>
                            x.Coverage.TeamId ==
                                member.TeamId)
                    .OrderBy(
                        x =>
                            x.Coverage.Priority)
                    .FirstOrDefault();

            if (coverage is null)
            {
                continue;
            }

            var coverageLocation =
                ResolveCoverageLocationName(
                    site.Name,
                    coverage.Coverage,
                    coverageLocationNames);

            var technicianLocation =
                ResolveUserLocationName(
                    user,
                    technicianSiteNames,
                    technicianLocationNames);

            var occupancy =
                member.MaxOpenTickets <= 0
                    ? 1d
                    : (double)openTickets /
                      member.MaxOpenTickets;

            var schedulePriority =
                schedulePriorities
                    .GetValueOrDefault(
                        (
                            member.TeamId,
                            member.UserId
                        ),
                        1000);

            var team =
                teams[
                    member.TeamId];

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
                        coverageLocation,

                    TechnicianLocation:
                        technicianLocation,

                    OpenTickets:
                        openTickets,

                    Capacity:
                        member.MaxOpenTickets,

                    SchedulePriority:
                        schedulePriority,

                    CoveragePriority:
                        coverage.Coverage.Priority);

            candidates.Add(
                new RankedCandidate(
                    candidate,
                    coverage.Rank,
                    coverage.Coverage.Priority,
                    schedulePriority,
                    occupancy,
                    lastAssignments
                        .GetValueOrDefault(
                            member.UserId)));
        }

        // ========================================================
        // SELECT BEST CANDIDATE
        // ========================================================

        var chosen =
    candidates
        .OrderBy(
            x =>
                x.CoverageRank)

        .ThenBy(
            x =>
                x.CoveragePriority)

        .ThenBy(
            x =>
                x.SchedulePriority)

        .ThenBy(
            x =>
                x.Occupancy)

        .ThenBy(
            x =>
                x.Candidate.OpenTickets)

        /*
         * Null means the technician has never received
         * an automatic assignment, so prefer them first.
         */
        .ThenBy(
            x =>
                x.LastAutomaticAssignmentAtUtc
                    .HasValue
                    ? 1
                    : 0)

        .ThenBy(
            x =>
                x.LastAutomaticAssignmentAtUtc)

        /*
         * Final deterministic fallback only.
         */
        .ThenBy(
            x =>
                x.Candidate.UserId)

        .FirstOrDefault();
              

        if (chosen is null)
        {
            return new RoutingEvaluation(
                null,
                requesterLocation,
                site.Id,
                effectiveLocationId,
                "Todos los técnicos elegibles alcanzaron su capacidad máxima.");
        }

        var selected =
            chosen.Candidate;

        var reason =
            BuildRoutingReason(
                requesterLocation,
                normalizedCategory,
                selected);

        return new RoutingEvaluation(
            Candidate:
                selected,
            RequesterLocation:
                requesterLocation,
            SiteId:
                site.Id,
            SiteLocationId:
                effectiveLocationId,
            Reason:
                reason);
    }

    // ============================================================
    // RETRY AUTOMATIC ASSIGNMENT
    // ============================================================

    public async Task<bool>
        RetryAutomaticAssignmentAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        var strategy =
            _db.Database
                .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _db.Database
                        .BeginTransactionAsync(
                            System.Data
                                .IsolationLevel
                                .Serializable,
                            cancellationToken);

                var ticket =
                    await _db.HelpdeskTickets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Id ==
                                    ticketId
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed",
                            cancellationToken);

                if (ticket is null)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                /*
                 * External email requesters currently do not have
                 * a trustworthy corporate location. We keep them
                 * pending instead of assigning to an arbitrary Site.
                 */
                if (
                    ticket.Source ==
                        "email"
                    &&
                    !string.IsNullOrWhiteSpace(
                        ticket.ExternalRequesterEmail)
                    &&
                    !ticket.SiteId.HasValue)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                var result =
                    await EvaluateRoutingAsync(
                        organizationId,
                        ticket.RequesterUserId,
                        ticket.Category,
                        cancellationToken,
                        ticket.RequestedTeamId,
                        ticket.SiteId,
                        ticket.SiteLocationId);

                if (
                    result.Candidate
                    is not { } candidate)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                var now =
                    DateTime.UtcNow;

                var selectedMembership =
    await _db.HelpdeskTeamMembers
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x =>
                x.OrganizationId ==
                    organizationId
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

if (selectedMembership is null)
{
    await transaction
        .RollbackAsync(
            cancellationToken);

    return false;
}

var currentLoad =
    await _db.HelpdeskTickets
        .AsNoTracking()
        .CountAsync(
            x =>
                x.OrganizationId ==
                    organizationId
                &&
                x.AssigneeUserId ==
                    candidate.UserId
                &&
                x.Id !=
                    ticketId
                &&
                x.Status !=
                    "resolved"
                &&
                x.Status !=
                    "closed",
            cancellationToken);

if (currentLoad >=
    selectedMembership.MaxOpenTickets)
{
    await transaction
        .RollbackAsync(
            cancellationToken);

    return false;
}

                var changed =
                    await _db.HelpdeskTickets
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Id ==
                                    ticketId
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed")
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
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                var summary =
                    "Reintento automático: " +
                    BuildRoutingReason(
                        result.RequesterLocation
                            ??
                            "Localidad no disponible",
                        ticket.Category,
                        candidate);

                var audit =
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticketId,
                        null,
                        "auto_assigned",
                        TrimSummary(
                            summary));

                _db.HelpdeskTicketEvents
                    .Add(
                        audit);

                await _db
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction
                    .CommitAsync(
                        cancellationToken);

                /*
                 * ExecuteUpdate bypasses tracking, so avoid stale
                 * state if this DbContext remains alive.
                 */
                _db.ChangeTracker.Clear();

                return true;
            });
    }

    // ============================================================
    // ROUTING DIAGNOSTIC FOR AN EXISTING TICKET
    // ============================================================

    public async Task<RoutingPreview?>
        PreviewTicketRoutingAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var result =
            await EvaluateRoutingAsync(
                organizationId,
                ticket.RequesterUserId,
                ticket.Category,
                cancellationToken,
                ticket.RequestedTeamId,
                ticket.SiteId,
                ticket.SiteLocationId);

        return ToPreview(
            result);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string NormalizeRoutingCategory(
        string? category)
    {
        if (string.IsNullOrWhiteSpace(
                category))
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
        IReadOnlyDictionary<Guid, string>
            locations)
    {
        if (
            !coverage.SiteLocationId
                .HasValue)
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

        if (
            !sites.TryGetValue(
                user.SiteId.Value,
                out var siteName))
        {
            siteName =
                "Localidad desconocida";
        }

        if (
            !user.SiteLocationId
                .HasValue)
        {
            return siteName;
        }

        if (
            !locations.TryGetValue(
                user.SiteLocationId.Value,
                out var locationName))
        {
            return siteName;
        }

        return $"{siteName} / {locationName}";
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