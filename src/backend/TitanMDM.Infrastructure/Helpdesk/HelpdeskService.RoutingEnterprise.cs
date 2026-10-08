using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // HD-C3 / HD-C4
    //
    // ENTERPRISE AUTOMATIC ASSIGNMENT
    //
    // Esta capa NO duplica EvaluateRoutingAsync().
    //
    // Reutiliza el motor existente que ya evalúa:
    //
    // - categoría
    // - grupo
    // - Site
    // - SiteLocation
    // - cobertura
    // - prioridad de cobertura
    // - RBAC
    // - disponibilidad
    // - horario
    // - prioridad de turno
    // - capacidad
    // - carga
    // - ocupación
    // - última asignación automática
    //
    // Aquí reforzamos:
    //
    // - identidad del remitente de email;
    // - revalidación antes de asignar;
    // - concurrencia;
    // - capacidad;
    // - turno;
    // - auditoría;
    // - motivos de espera.
    // ============================================================

    public async Task<bool>
        RetryAutomaticAssignmentEnterpriseAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        // ========================================================
        // TICKET SNAPSHOT
        // ========================================================

        var ticket =
            await _db
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

        if (
            ticket is null
            ||
            ticket.AssigneeUserId.HasValue
            ||
            ticket.Status
                is "resolved"
                or "closed"
                or "pendinguser")
        {
            return false;
        }

        // ========================================================
        // EMAIL REQUESTER SAFETY
        //
        // HelpdeskEmailImportService ya intenta resolver:
        //
        // FromEmail -> TitanMDM User
        //
        // Si no lo encuentra utiliza el usuario técnico del buzón.
        //
        // Nunca debemos utilizar la localidad del actor del buzón
        // como si fuera la localidad real del remitente.
        // ========================================================

        if (
            string.Equals(
                ticket.Source,
                "email",
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.IsNullOrWhiteSpace(
                ticket.ExternalRequesterEmail)
            &&
            !ticket.SiteId.HasValue)
        {
            var requester =
                await _db
                    .Users
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
                                x.Id,
                                x.Email,
                                x.SiteId
                            })
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (requester is null)
            {
                await RecordRoutingWaitingEnterpriseAsync(
                    organizationId,
                    ticketId,
                    "El remitente del correo no está vinculado a un usuario activo de TitanMDM.",
                    cancellationToken);

                return false;
            }

            if (
                !string.Equals(
                    requester.Email?.Trim(),
                    ticket.ExternalRequesterEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                await RecordRoutingWaitingEnterpriseAsync(
                    organizationId,
                    ticketId,
                    "El remitente del correo todavía no está vinculado con su identidad corporativa en TitanMDM.",
                    cancellationToken);

                return false;
            }

            if (!requester.SiteId.HasValue)
            {
                await RecordRoutingWaitingEnterpriseAsync(
                    organizationId,
                    ticketId,
                    "El usuario remitente existe, pero no tiene una localidad asignada.",
                    cancellationToken);

                return false;
            }
        }

        // ========================================================
        // ROUTING EVALUATION
        //
        // HD-C4:
        //
        // EvaluateRoutingAsync ya selecciona usando:
        //
        // 1 cobertura más específica
        // 2 prioridad de cobertura
        // 3 prioridad de turno
        // 4 menor porcentaje de ocupación
        // 5 menor carga absoluta
        // 6 quien nunca recibió autoasignación
        // 7 quien lleva más tiempo sin recibir una
        // 8 UserId como desempate determinístico final
        // ========================================================

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

            return false;
        }

        // ========================================================
        // SERIALIZABLE ASSIGNMENT
        //
        // El candidato fue calculado fuera de la transacción.
        //
        // Antes del UPDATE volvemos a comprobar:
        //
        // - ticket sigue sin asignar;
        // - membership existe;
        // - disponible;
        // - acepta autoasignación;
        // - capacidad;
        // - turno activo.
        //
        // Esto evita depender de una decisión stale.
        // ========================================================

        var strategy =
            _db
                .Database
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

                var currentTicket =
                    await _db
                        .HelpdeskTickets
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

                if (currentTicket is null)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // MEMBERSHIP RECHECK
                // =================================================

                var membership =
                    await _db
                        .HelpdeskTeamMembers
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
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // CAPACITY RECHECK
                // =================================================

                var currentLoad =
                    await _db
                        .HelpdeskTickets
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

                if (
                    currentLoad >=
                    membership.MaxOpenTickets)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // SCHEDULE RECHECK
                //
                // Enterprise policy:
                //
                // sin horario = no autoasignación.
                // =================================================

                var schedules =
                    await _db
                        .Set<
                            HelpdeskTechnicianSchedule>()
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

                if (
                    schedules.Count ==
                    0)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                var now =
                    DateTime.UtcNow;

                var onDuty =
                    schedules.Any(
                        x =>
                            x.IsOnDuty(
                                now));

                if (!onDuty)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // ATOMIC UPDATE
                // =================================================

                var changed =
                    await _db
                        .HelpdeskTickets
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
                                        now),
                            cancellationToken);

                if (
                    changed !=
                    1)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // AUDIT
                // =================================================

                var occupancy =
                    membership.MaxOpenTickets <=
                    0
                        ? 100d
                        :
                        Math.Round(
                            (
                                (double)currentLoad
                                /
                                membership.MaxOpenTickets
                            )
                            *
                            100d,
                            2);

                var summary =
                    "Autoasignación enterprise: " +
                    BuildRoutingReason(
                        routing.RequesterLocation
                        ??
                        "Localidad no disponible",
                        currentTicket.Category,
                        candidate)
                    +
                    $" Ocupación previa {occupancy:0.##}%.";

                _db
                    .HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticketId,
                            null,
                            "auto_assigned",
                            TrimSummary(
                                summary)));

                await _db
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction
                    .CommitAsync(
                        cancellationToken);

                /*
                 * ExecuteUpdate no sincroniza el ChangeTracker.
                 */
                _db
                    .ChangeTracker
                    .Clear();

                return true;
            });
    }

    // ============================================================
    // ROUTING WAITING AUDIT
    //
    // No escribimos un evento cada minuto.
    // ============================================================

    private async Task
        RecordRoutingWaitingEnterpriseAsync(
            Guid organizationId,
            Guid ticketId,
            string reason,
            CancellationToken cancellationToken)
    {
        var since =
            DateTime.UtcNow
                .AddMinutes(
                    -30);

        var alreadyRecorded =
            await _db
                .HelpdeskTicketEvents
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
                ? "Autoasignación pendiente: no existe un candidato válido."
                : "Autoasignación pendiente: " +
                  reason.Trim();

        _db
            .HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticketId,
                    null,
                    "routing_waiting",
                    TrimSummary(
                        summary)));

        await _db
            .SaveChangesAsync(
                cancellationToken);
    }
}