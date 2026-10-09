using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // ENTERPRISE AUTOMATIC ASSIGNMENT
    //
    // SINGLE WRITE PATH
    //
    // Este archivo es responsable exclusivamente de:
    //
    // - concurrencia;
    // - revalidación;
    // - actualización;
    // - auditoría.
    //
    // No vuelve a implementar el algoritmo de selección.
    // ============================================================

    public async Task<bool>
        RetryAutomaticAssignmentEnterpriseAsync(
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

                await AcquireAutomaticAssignmentLockAsync(organizationId, cancellationToken);

                // =================================================
                // 1. FRESH TICKET
                // =================================================

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
                                    "closed"
                                &&
                                x.Status !=
                                    "pendinguser",
                            cancellationToken);

                if (ticket is null)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // 2. REQUESTER IDENTITY
                // =================================================

                var requesterValid =
                    await ValidateEmailRequesterIdentityAsync(
                        organizationId,
                        ticket,
                        cancellationToken);

                if (!requesterValid)
                {
                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "La identidad corporativa del remitente todavía no es confiable para autoasignación.",
                        cancellationToken);

                    /*
                     * Conservamos el evento de diagnóstico.
                     */
                    await transaction
                        .CommitAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // 3. ROUTING
                //
                // CRÍTICO:
                //
                // EvaluateRoutingAsync se ejecuta DENTRO de la
                // transacción SERIALIZABLE.
                //
                // Cada ticket ve la carga resultante del ticket
                // anterior.
                // =================================================

                var routing =
                    await EvaluateRoutingAsync(
                        organizationId,
                        ticket.RequesterUserId,
                        ticket.Category,
                        cancellationToken,
                        ticket.RequestedTeamId,
                        ticket.SiteId,
                        ticket.SiteLocationId);

                if (
                    routing.Candidate
                    is not { } candidate)
                {
                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        routing.Reason,
                        cancellationToken);

                    await transaction
                        .CommitAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // 4. FINAL REVALIDATION
                // =================================================

                var validation =
                    await RevalidateCandidateAsync(
                        organizationId,
                        ticketId,
                        candidate,
                        cancellationToken);

                if (!validation.CanAssign)
                {
                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        validation.Reason,
                        cancellationToken);

                    await transaction
                        .CommitAsync(
                            cancellationToken);

                    return false;
                }

                var now =
                    DateTime.UtcNow;

                // =================================================
                // 5. ATOMIC UPDATE
                // =================================================

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
                                        now)
                                    .SetProperty(x => x.SiteId, routing.SiteId)
                                    .SetProperty(x => x.SiteLocationId, routing.SiteLocationId),
                            cancellationToken);

                if (changed != 1)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // 6. FAIRNESS / AUDIT
                // =================================================

                var occupancy =
                    validation.Capacity <=
                        0
                        ? 100d
                        :
                        Math.Round(
                            (
                                (double)
                                    validation.CurrentLoad
                                /
                                validation.Capacity
                            )
                            *
                            100d,
                            2);

                var summary =
                    "Autoasignación enterprise: "
                    +
                    BuildRoutingReason(
                        routing.RequesterLocation
                        ??
                        "Localidad no disponible",
                        ticket.Category,
                        candidate)
                    +
                    $" Ocupación previa {occupancy:0.##}%.";

                _db.HelpdeskTicketEvents.Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticketId,
                        null,
                        "auto_assigned",
                        TrimSummary(
                            summary),
                        candidate.UserId));

                await _db.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                /*
                 * ExecuteUpdateAsync no sincroniza automáticamente
                 * entidades previamente cargadas en ChangeTracker.
                 */
                _db.ChangeTracker.Clear();

                return true;
            });
    }

    // ============================================================
    // EMAIL REQUESTER IDENTITY
    // ============================================================

    private async Task<bool>
        ValidateEmailRequesterIdentityAsync(
            Guid organizationId,
            HelpdeskTicket ticket,
            CancellationToken cancellationToken)
    {
        /*
         * Tickets creados directamente desde TitanMDM
         * no requieren esta comprobación.
         */
        if (
            !string.Equals(
                ticket.Source,
                "email",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.IsNullOrWhiteSpace(
                ticket.ExternalRequesterEmail))
        {
            return true;
        }

        var requester =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticket.RequesterUserId
                        &&
                        x.IsActive)
                .Select(
                    x =>
                        new
                        {
                            x.Email
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (requester is null)
        {
            return false;
        }

        /*
         * Impide confundir la cuenta técnica del buzón
         * con el remitente real.
         */
        if (string.Equals(
            requester.Email?
                .Trim(),
            ticket.ExternalRequesterEmail
                .Trim(),
            StringComparison.OrdinalIgnoreCase)) return true;

        var email = ticket.ExternalRequesterEmail.Trim().ToLowerInvariant();
        return await _db.EntraDirectoryUsers.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == organizationId && x.IsActive &&
            x.LinkedTitanUserId == ticket.RequesterUserId &&
            (x.Mail == email || x.UserPrincipalName == email), cancellationToken);
    }

    // ============================================================
    // FINAL CANDIDATE VALIDATION
    // ============================================================

    private async Task<CandidateValidation>
        RevalidateCandidateAsync(
            Guid organizationId,
            Guid ticketId,
            RoutingCandidate candidate,
            CancellationToken cancellationToken)
    {
        // --------------------------------------------------------
        // Active account
        // --------------------------------------------------------

        var technicianActive = await EligibleTechnicians(organizationId)
            .AnyAsync(id => id == candidate.UserId, cancellationToken);

        if (!technicianActive)
        {
            return CandidateValidation.Fail(
                "El técnico seleccionado dejó de estar activo.");
        }

        // --------------------------------------------------------
        // Membership
        // --------------------------------------------------------

        var membership =
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

        if (membership is null)
        {
            return CandidateValidation.Fail(
                "El técnico seleccionado ya no está habilitado para autoasignación en este grupo.");
        }

        // --------------------------------------------------------
        // Current workload
        // --------------------------------------------------------

        var currentLoad =
            await GetTechnicianCurrentLoadAsync(
                organizationId,
                ticketId,
                candidate.UserId,
                cancellationToken);

        if (
            currentLoad >=
                membership.MaxOpenTickets)
        {
            return CandidateValidation.Fail(
                "El técnico seleccionado alcanzó su capacidad máxima.");
        }

        // --------------------------------------------------------
        // Schedule
        // --------------------------------------------------------

        var schedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TeamId ==
                            candidate.TeamId
                        &&
                        x.UserId ==
                            candidate.UserId)
                .ToListAsync(
                    cancellationToken);

        /*
         * Misma regla utilizada por RoutingCore:
         *
         * sin schedule:
         *     IsAvailable gobierna.
         *
         * con schedule:
         *     uno debe estar activo.
         */
        if (
            schedules.Count >
                0
            &&
            !schedules.Any(
                x =>
                    x.IsOnDuty(
                        DateTime.UtcNow)))
        {
            return CandidateValidation.Fail(
                "El técnico seleccionado se encuentra fuera de turno.");
        }

        // --------------------------------------------------------
        // Final capacity read
        // --------------------------------------------------------

        currentLoad =
            await GetTechnicianCurrentLoadAsync(
                organizationId,
                ticketId,
                candidate.UserId,
                cancellationToken);

        if (
            currentLoad >=
                membership.MaxOpenTickets)
        {
            return CandidateValidation.Fail(
                "La capacidad del técnico cambió antes de confirmar la asignación.");
        }

        return CandidateValidation.Ok(
            currentLoad,
            membership.MaxOpenTickets);
    }

    // ============================================================
    // CURRENT LOAD
    // ============================================================

    private async Task<int>
        GetTechnicianCurrentLoadAsync(
            Guid organizationId,
            Guid currentTicketId,
            Guid technicianId,
            CancellationToken cancellationToken)
    {
        return await _db.HelpdeskTickets
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.AssigneeUserId ==
                        technicianId
                    &&
                    x.Id !=
                        currentTicketId
                    &&
                    x.Status !=
                        "resolved"
                    &&
                    x.Status !=
                        "closed",
                cancellationToken);
    }

    // ============================================================
    // ROUTING WAITING AUDIT
    // ============================================================

    private async Task
        RecordRoutingWaitingEnterpriseAsync(
            Guid organizationId,
            Guid ticketId,
            string reason,
            CancellationToken cancellationToken)
    {
        /*
         * Evitamos insertar un evento idéntico cada ciclo
         * del background worker.
         */
        var since =
            DateTime.UtcNow
                .AddMinutes(
                    -30);

        var alreadyRecorded =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TicketId ==
                            ticketId
                        &&
                        x.EventType ==
                            "routing_waiting"
                        &&
                        x.CreatedAtUtc >=
                            since,
                    cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        var summary =
            string.IsNullOrWhiteSpace(
                reason)
                ?
                "Autoasignación pendiente: no existe un candidato válido."
                :
                "Autoasignación pendiente: "
                +
                reason.Trim();

        _db.HelpdeskTicketEvents.Add(
            new HelpdeskTicketEvent(
                organizationId,
                ticketId,
                null,
                "routing_waiting",
                TrimSummary(
                    summary)));

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // RESULT
    // ============================================================

    private sealed record CandidateValidation(
        bool CanAssign,
        string Reason,
        int CurrentLoad,
        int Capacity)
    {
        public static CandidateValidation Ok(
            int currentLoad,
            int capacity)
        {
            return new CandidateValidation(
                true,
                string.Empty,
                currentLoad,
                capacity);
        }

        public static CandidateValidation Fail(
            string reason)
        {
            return new CandidateValidation(
                false,
                reason,
                0,
                0);
        }
    }
}
