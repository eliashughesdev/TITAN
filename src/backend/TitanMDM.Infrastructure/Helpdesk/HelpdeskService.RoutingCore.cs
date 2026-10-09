using Microsoft.EntityFrameworkCore;

namespace TitanMDM.Infrastructure.Helpdesk;

/// <summary>
/// Núcleo de evaluación del motor de routing de Helpdesk.
///
/// IMPORTANTE:
/// Este partial NO contiene helpers, records ni consultas duplicadas.
/// Toda la infraestructura reutilizable permanece en
/// HelpdeskService.Routing.cs.
///
/// Responsabilidades exclusivas:
/// - Exponer PreviewRoutingAsync.
/// - Ejecutar EvaluateRoutingAsync.
/// - Coordinar contexto, grupos, cobertura, técnicos, horarios,
///   capacidad y fairness.
/// </summary>
public sealed partial class HelpdeskService
{
    // ============================================================
    // PUBLIC PREVIEW
    // ============================================================

    public async Task<RoutingPreview> PreviewRoutingAsync(
        Guid organizationId,
        Guid requesterId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var evaluation =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken);

        return ToPreview(
            evaluation);
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
        var evaluation =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                category,
                cancellationToken,
                requestedTeamId,
                siteId,
                siteLocationId);

        return ToPreview(
            evaluation);
    }

    // ============================================================
    // CENTRAL ROUTING ENGINE
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
        // ========================================================
        // 1. REQUESTER + LOCATION CONTEXT
        // ========================================================

        var context =
            await ResolveRoutingContextAsync(
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
                ??
                "No fue posible resolver el contexto del solicitante.");
        }

        if (!context.SiteId.HasValue)
        {
            return RoutingEvaluation.Fail(
                "No fue posible determinar la localidad del solicitante.");
        }

        if (string.IsNullOrWhiteSpace(
                context.NormalizedCategory))
        {
            return RoutingEvaluation.Fail(
                "No fue posible normalizar la categoría del ticket.");
        }

        // ========================================================
        // 2. TEAMS
        // ========================================================

        var teams =
            await LoadRoutingTeamsAsync(
                organizationId,
                context.NormalizedCategory,
                requestedTeamId,
                cancellationToken);

        if (teams.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    requestedTeamId.HasValue
                        ?
                        "El grupo seleccionado no está activo o no puede atender el ticket."
                        :
                        "No existe un grupo activo compatible con el ticket.");
        }

        // ========================================================
        // 3. COVERAGE
        // ========================================================

        var coverages =
            await LoadRankedCoveragesAsync(
                organizationId,
                context.SiteId.Value,
                context.SiteLocationId,
                context.NormalizedCategory,
                teams.Keys.ToArray(),
                cancellationToken);

        if (coverages.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "No existe cobertura compatible con la localidad, " +
                    "sublocalidad y categoría del ticket.");
        }

        var usableCoverages =
            coverages;

        if (usableCoverages.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "No existe una cobertura utilizable para el ticket.");
        }

        // ========================================================
        // 4. TECHNICIAN PERMISSIONS
        // ========================================================

        var eligibleTechnicianIds = await EligibleTechnicians(organizationId)
    .ToListAsync(cancellationToken);

        if (eligibleTechnicianIds.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "No existen técnicos activos con permisos operativos " +
                    "para Mesa de Ayuda.");
        }

        // ========================================================
        // 5. COVERED TEAMS
        // ========================================================

        var coveredTeamIds =
            usableCoverages
                .Select(
                    x =>
                        x.Coverage.TeamId)
                .Distinct()
                .ToArray();

        if (coveredTeamIds.Length == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "La cobertura encontrada no contiene grupos utilizables.");
        }

        // ========================================================
        // 6. TEAM MEMBERS
        // ========================================================

        var members =
            await LoadAvailableMembersAsync(
                organizationId,
                coveredTeamIds,
                eligibleTechnicianIds,
                cancellationToken);

        if (members.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "Existe cobertura, pero no hay técnicos habilitados " +
                    "que acepten autoasignación.");
        }

        // ========================================================
        // 7. SCHEDULES
        // ========================================================

        var scheduleResult =
            await FilterMembersByScheduleAsync(
                organizationId,
                members,
                cancellationToken);

        if (scheduleResult.Members.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "Existe cobertura y técnicos habilitados, " +
                    "pero ninguno está disponible según su turno.");
        }

        // ========================================================
        // 8. BUILD CANDIDATES
        // ========================================================

        var candidates =
            await BuildRankedCandidatesAsync(
                organizationId,
                context.SiteName
                ??
                context.RequesterLocation
                ??
                "Localidad",

                teams,
                usableCoverages,
                scheduleResult.Members,
                scheduleResult.SchedulePriorities,
                cancellationToken);

        if (candidates.Count == 0)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "No existen técnicos candidatos con capacidad disponible.");
        }

        // ========================================================
        // 9. FAIR DISTRIBUTION
        // ========================================================
        //
        // Orden:
        //
        // 1. Mejor cobertura.
        // 2. Prioridad de cobertura.
        // 3. Prioridad de turno.
        // 4. Menor ocupación proporcional.
        // 5. Menor cantidad absoluta de tickets.
        // 6. Técnicos nunca utilizados primero.
        // 7. Técnico con más tiempo sin recibir ticket.
        // 8. UserId únicamente como desempate estable.
        //
        // Esto evita que 10-15 técnicos terminen recibiendo
        // siempre el ticket en el mismo usuario.
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

                .ThenBy(
                    x =>
                        x.LastAutomaticAssignmentAtUtc
                            .HasValue
                            ? 1
                            : 0)

                .ThenBy(
                    x =>
                        x.LastAutomaticAssignmentAtUtc)

                .ThenBy(
                    x =>
                        x.Candidate.UserId)

                .FirstOrDefault();

        if (chosen is null)
        {
            return new RoutingEvaluation(
                Candidate:
                    null,

                RequesterLocation:
                    context.RequesterLocation,

                SiteId:
                    context.SiteId,

                SiteLocationId:
                    context.SiteLocationId,

                Reason:
                    "Todos los técnicos elegibles alcanzaron su capacidad máxima.");
        }

        // ========================================================
        // 10. RESULT
        // ========================================================

        var selected =
            chosen.Candidate;

        return new RoutingEvaluation(
            Candidate:
                selected,

            RequesterLocation:
                context.RequesterLocation,

            SiteId:
                context.SiteId,

            SiteLocationId:
                context.SiteLocationId,

            Reason:
                BuildRoutingReason(
                    context.RequesterLocation
                    ??
                    context.SiteName
                    ??
                    "Sin localidad",

                    context.NormalizedCategory,

                    selected));
    }
}
