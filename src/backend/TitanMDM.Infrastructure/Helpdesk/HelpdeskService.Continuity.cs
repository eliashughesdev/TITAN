using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    public async Task<bool> TryAutomaticHandoverAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Database.CreateExecutionStrategy()
            .ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

            var ticket = await _db.HelpdeskTickets.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.OrganizationId == organizationId &&
                    x.Id == ticketId &&
                    x.AssigneeUserId != null &&
                    x.FirstRespondedAtUtc == null &&
                    (x.Status == "new" || x.Status == "open"),
                    cancellationToken);

            if (ticket is null ||
                !string.IsNullOrWhiteSpace(ticket.ExternalRequesterEmail))
            {
                return false;
            }

            var last = await _db.HelpdeskTicketEvents.AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.TicketId == ticketId &&
                    (x.EventType == "assigned" ||
                     x.EventType == "auto_assigned" ||
                     x.EventType == "auto_handover"))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.UtcNow;

            if (last is null ||
                last.EventType == "assigned" ||
                last.CreatedAtUtc > now.AddMinutes(-15))
            {
                return false;
            }

            var previousId = ticket.AssigneeUserId!.Value;

            if (await TechnicianHasActiveShiftAsync(
                organizationId,
                previousId,
                ticket.Category,
                now,
                cancellationToken,
                ticket.RequestedTeamId))
            {
                return false;
            }

            var result = await EvaluateRoutingAsync(
                organizationId,
                ticket.RequesterUserId,
                ticket.Category,
                cancellationToken,
                ticket.RequestedTeamId);

            if (result.Candidate is not { } next ||
                next.UserId == previousId)
            {
                return false;
            }

            var changed = await _db.HelpdeskTickets
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.Id == ticketId &&
                    x.AssigneeUserId == previousId &&
                    x.UpdatedAtUtc == ticket.UpdatedAtUtc &&
                    x.FirstRespondedAtUtc == null &&
                    (x.Status == "new" || x.Status == "open"))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AssigneeUserId, (Guid?)next.UserId)
                    .SetProperty(x => x.Status, "open")
                    .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);

            if (changed != 1)
                return false;

            var previousName = await _db.Users.AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.Id == previousId)
                .Select(x => x.FirstName + " " + x.LastName)
                .FirstOrDefaultAsync(cancellationToken);

            var summary =
                $"Relevo automático de {previousName ?? previousId.ToString()} " +
                $"a {next.UserName}: el responsable anterior no tiene turno " +
                "habilitado disponible. " + result.Reason;

            var audit = new HelpdeskTicketEvent(
                organizationId,
                ticketId,
                null,
                "auto_handover",
                summary[..Math.Min(500, summary.Length)]);

            _db.HelpdeskTicketEvents.Add(audit);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return true;
            }
            finally
            {
                _db.Entry(audit).State = EntityState.Detached;
            }
        });
    }

    private async Task<bool> TechnicianHasActiveShiftAsync(
    Guid org,
    Guid userId,
    string category,
    DateTime now,
    CancellationToken ct,
    Guid? requestedTeamId = null)
    {
        if (!await EligibleTechnicians(org)
                .AnyAsync(id => id == userId, ct))
        {
            return false;
        }

        var teams = await _db.HelpdeskTeams
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == org &&
                x.IsActive &&
                (!requestedTeamId.HasValue ||
                 x.Id == requestedTeamId.Value))
            .ToListAsync(ct);

        var matchingTeamIds = teams
            .Where(x =>
                x.HandlesCategory(category) ||
                (
                    category == "general" &&
                    string.IsNullOrWhiteSpace(x.Categories)
                ))
            .Select(x => x.Id)
            .ToArray();

        if (matchingTeamIds.Length == 0)
        {
            return false;
        }

        var memberships = await _db.HelpdeskTeamMembers
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == org &&
                x.UserId == userId &&
                matchingTeamIds.Contains(x.TeamId) &&
                x.IsAvailable &&
                x.AcceptsAutomaticAssignments &&
                x.MaxOpenTickets > 0)
            .ToListAsync(ct);

        if (memberships.Count == 0)
        {
            return false;
        }

        var membershipTeamIds = memberships
            .Select(x => x.TeamId)
            .Distinct()
            .ToArray();

        var schedules = await _db
            .Set<HelpdeskTechnicianSchedule>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == org &&
                x.UserId == userId &&
                membershipTeamIds.Contains(x.TeamId))
            .ToListAsync(ct);

        /*
         * Regla coherente con EvaluateRoutingAsync:
         *
         * - Si NO existen horarios configurados:
         *   IsAvailable controla la disponibilidad.
         *
         * - Si existen horarios:
         *   al menos uno debe estar activo ahora.
         */
        if (schedules.Count == 0)
        {
            return true;
        }

        return schedules.Any(
            schedule =>
                schedule.IsOnDuty(now));
    }
}
