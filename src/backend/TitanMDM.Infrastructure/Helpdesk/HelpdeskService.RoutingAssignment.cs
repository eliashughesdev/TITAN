using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
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
                            System.Data.IsolationLevel.Serializable,
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
                 * IMPORTANTE:
                 *
                 * Ya NO rechazamos automáticamente tickets
                 * provenientes de correo sólo porque no tengan
                 * SiteId.
                 *
                 * EvaluateRoutingAsync podrá:
                 *
                 * - usar Site del ticket;
                 * - usar Site del solicitante;
                 * - inferir Site de un grupo explícito;
                 * - utilizar un grupo de cobertura global.
                 */
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

                // =================================================
                // REVALIDATE MEMBERSHIP
                // =================================================

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

                // =================================================
                // REVALIDATE SCHEDULE
                // =================================================

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

                if (
                    schedules.Count == 0
                    ||
                    !schedules.Any(
                        x =>
                            x.IsOnDuty(
                                DateTime.UtcNow)))
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // REVALIDATE CAPACITY
                // =================================================

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

                if (
                    currentLoad >=
                    selectedMembership.MaxOpenTickets)
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

                    return false;
                }

                // =================================================
                // ATOMIC ASSIGNMENT
                // =================================================

                var now =
                    DateTime.UtcNow;

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
                                            x.SiteId,
                                        result.SiteId)

                                    .SetProperty(
                                        x =>
                                            x.SiteLocationId,
                                        result.SiteLocationId)

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

                // =================================================
                // AUDIT
                // =================================================

                var summary =
                    "Reintento automático: " +
                    BuildRoutingReason(
                        result.RequesterLocation
                        ??
                        "Sin localidad",
                        ticket.Category,
                        candidate);

                _db.HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticketId,
                            null,
                            "auto_assigned",
                            TrimSummary(
                                summary)));

                if (
                    !ticket.SiteId.HasValue
                    &&
                    result.SiteId.HasValue)
                {
                    _db.HelpdeskTicketEvents
                        .Add(
                            new HelpdeskTicketEvent(
                                organizationId,
                                ticketId,
                                null,
                                "site_inferred",
                                TrimSummary(
                                    $"Localidad inferida por routing: " +
                                    $"{result.RequesterLocation}.")));
                }

                await _db
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction
                    .CommitAsync(
                        cancellationToken);

                _db.ChangeTracker
                    .Clear();

                return true;
            });
    }
}