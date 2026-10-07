using TitanMDM.Application.Helpdesk;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // ROUTING DIAGNOSTIC
    //
    // IMPORTANTE:
    // Este método NO implementa un segundo motor de routing.
    //
    // Utiliza PreviewRoutingAsync(), que a su vez consume
    // EvaluateRoutingAsync() del motor real de Helpdesk.
    //
    // Por tanto, lo que muestra la UI es exactamente lo que
    // TitanMDM intentaría utilizar para una autoasignación real.
    // ============================================================

    public async Task<HelpdeskRoutingPreviewDto>
        PreviewRoutingDiagnosticAsync(
            Guid organizationId,
            Guid requesterUserId,
            HelpdeskRoutingPreviewRequest request,
            CancellationToken cancellationToken = default)
    {
        // ========================================================
        // BASIC VALIDATION
        // ========================================================

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "La organización es obligatoria.",
                nameof(organizationId));
        }

        if (requesterUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "El solicitante es obligatorio.",
                nameof(requesterUserId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (
            request.SiteLocationId.HasValue
            &&
            !request.SiteId.HasValue)
        {
            throw new ArgumentException(
                "No puedes indicar una sublocalidad sin indicar la localidad.");
        }

        // ========================================================
        // NORMALIZE INPUT
        // ========================================================

        var category =
            NormalizeDiagnosticCategory(
                request.Category);

        var priority =
            NormalizeDiagnosticPriority(
                request.Priority);

        // ========================================================
        // REAL ROUTING ENGINE
        // ========================================================

        var preview =
            await PreviewRoutingAsync(
                organizationId,
                requesterUserId,
                category,
                request.SiteId,
                request.SiteLocationId,
                request.RequestedTeamId,
                cancellationToken);

        // ========================================================
        // CAPACITY
        //
        // int? is intentional.
        //
        // Without an eligible technician there may be no capacity
        // to report, so null is semantically correct.
        // ========================================================

        int? remainingCapacity =
            preview.Capacity.HasValue
            &&
            preview.OpenTickets.HasValue
                ? Math.Max(
                    0,
                    preview.Capacity.Value
                    -
                    preview.OpenTickets.Value)
                : null;

        // ========================================================
        // RESULT
        // ========================================================

        return new HelpdeskRoutingPreviewDto(
            CanAssign:
                preview.CanAssign,

            Reason:
                preview.Reason,

            RequesterLocation:
                preview.RequesterZone,

            SiteId:
                preview.SiteId,

            SiteLocationId:
                preview.SiteLocationId,

            TeamId:
                preview.TeamId,

            TechnicianId:
                preview.TechnicianId,

            TechnicianName:
                preview.TechnicianName,

            TeamName:
                preview.TeamName,

            CoverageLocation:
                preview.CoverageZone,

            TechnicianLocation:
                preview.TechnicianZone,

            OpenTickets:
                preview.OpenTickets,

            Capacity:
                preview.Capacity,

            RemainingCapacity:
                remainingCapacity,

            Category:
                category,

            Priority:
                priority);
    }

    // ============================================================
    // CATEGORY NORMALIZATION
    // ============================================================

    private static string
        NormalizeDiagnosticCategory(
            string? category)
    {
        if (string.IsNullOrWhiteSpace(
                category))
        {
            return "general";
        }

        var normalized =
            category
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length > 80)
        {
            throw new ArgumentException(
                "La categoría no puede exceder 80 caracteres.");
        }

        if (normalized.Contains(
                '|'))
        {
            throw new ArgumentException(
                "La categoría no puede contener el carácter '|'.");
        }

        return normalized;
    }

    // ============================================================
    // PRIORITY NORMALIZATION
    // ============================================================

    private static string
        NormalizeDiagnosticPriority(
            string? priority)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                priority)
                ? "medium"
                : priority
                    .Trim()
                    .ToLowerInvariant();

        if (normalized is not (
                "low"
                or
                "medium"
                or
                "high"
                or
                "critical"))
        {
            throw new ArgumentException(
                "La prioridad debe ser low, medium, high o critical.");
        }

        /*
         * La prioridad del ticket actualmente participa en SLA.
         *
         * El motor de routing utiliza principalmente:
         *
         * - Site
         * - SiteLocation
         * - Category
         * - RequestedTeam
         * - Coverage Priority
         * - Technician RBAC
         * - Availability
         * - Automatic Assignment flag
         * - Schedule
         * - Technician Priority
         * - Current workload / capacity
         *
         * La dejamos en el diagnóstico porque forma parte
         * del ticket que estamos simulando.
         */

        return normalized;
    }
}