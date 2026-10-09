using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    public async Task<Guid> CreateGroupedRequestAsync(
        Guid org,
        Guid actor,
        Guid groupId,
        string subject,
        string description,
        string type,
        string category,
        bool console,
        CancellationToken ct)
    {
        subject = (subject ?? "").Trim();
        description = (description ?? "").Trim();
        category = (category ?? "").Trim().ToLowerInvariant();
        type = (type ?? "").Trim().ToLowerInvariant();

        if (subject.Length is < 1 or > 250 ||
            description.Length is < 1 or > 4000)
        {
            throw new ArgumentException(
                "Asunto obligatorio de hasta 250 caracteres " +
                "y descripción de hasta 4000.");
        }

        if (type is not ("incident" or "request"))
            throw new ArgumentException("Selecciona Incidente o Solicitud.");

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                ct);

            try
            {
                await AcquireAutomaticAssignmentLockAsync(org, ct);
                if (!await _db.Users.AnyAsync(x =>
                    x.Id == actor &&
                    x.OrganizationId == org &&
                    x.IsActive, ct))
                {
                    throw new ArgumentException(
                        "La cuenta no está activa en esta organización.");
                }

                var group = await _db.HelpdeskTeams.AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == groupId &&
                        x.OrganizationId == org &&
                        x.IsActive, ct);

                if (group is null || !group.HandlesCategory(category))
                {
                    throw new ArgumentException(
                        "Selecciona una categoría del grupo activo elegido.");
                }

                var number = await _ticketNumbers.NextAsync(ct);

                var ticket = new HelpdeskTicket(
                    org,
                    number,
                    subject,
                    description,
                    type,
                    "medium",
                    category,
                    console ? "console" : "portal",
                    actor,
                    null,
                    null);

                ticket.SelectGroup(groupId);

                var now = DateTime.UtcNow;
                ticket.ApplySla(now.AddHours(6), now.AddHours(24));

                var evaluation = await EvaluateRoutingAsync(
                    org,
                    actor,
                    category,
                    ct,
                    groupId);

                _db.HelpdeskTickets.Add(ticket);

                _db.HelpdeskTicketEvents.Add(new HelpdeskTicketEvent(
                    org,
                    ticket.Id,
                    actor,
                    "created",
                    $"Ticket {number} creado. Grupo: {group.Name}. " +
                    "Prioridad inicial: media."));

                if (evaluation.Candidate is { } candidate)
                {
                    ticket.Assign(candidate.UserId);

                    _db.HelpdeskTicketEvents.Add(new HelpdeskTicketEvent(
                        org,
                        ticket.Id,
                        null,
                        "auto_assigned",
                        evaluation.Reason[
                            ..Math.Min(500, evaluation.Reason.Length)],
                        candidate.UserId));
                }
                else
                {
                    _db.HelpdeskTicketEvents.Add(new HelpdeskTicketEvent(
                        org,
                        ticket.Id,
                        null,
                        "routing_pending",
                        evaluation.Reason[
                            ..Math.Min(500, evaluation.Reason.Length)]));
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ticket.Id;
            }
            finally
            {
                foreach (var entry in _db.ChangeTracker.Entries()
                    .Where(x =>
                        x.Entity is HelpdeskTicket or HelpdeskTicketEvent)
                    .ToArray())
                {
                    entry.State = EntityState.Detached;
                }
            }
        });
    }
}
