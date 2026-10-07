using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Helpdesk;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // ============================================================
    // REOPEN
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto?>
        ReopenAsync(
            Guid organizationId,
            Guid ticketId,
            Guid actorUserId,
            ReopenHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "La organización es obligatoria.",
                nameof(organizationId));
        }

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "El ticket es obligatorio.",
                nameof(ticketId));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "El usuario que ejecuta la acción es obligatorio.",
                nameof(actorUserId));
        }

        var reason =
            request.Reason?
                .Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Debes indicar el motivo de la reapertura.");
        }

        if (reason.Length < 5)
        {
            throw new ArgumentException(
                "El motivo de reapertura debe tener al menos 5 caracteres.");
        }

        if (reason.Length > 1000)
        {
            throw new ArgumentException(
                "El motivo de reapertura no puede exceder 1000 caracteres.");
        }

        var ticket =
            await _db.HelpdeskTickets
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.Id == ticketId,
                    cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var previousStatus =
            HelpdeskTicketStatus.Normalize(
                ticket.Status);

        if (!HelpdeskTicketStatus.IsTerminal(
                previousStatus))
        {
            throw new InvalidOperationException(
                "Solo se pueden reabrir tickets resueltos o cerrados.");
        }

        ticket.Reopen();

        var summary =
            $"Ticket reabierto desde '{previousStatus}'. " +
            $"Motivo: {reason}";

        if (summary.Length > 1500)
        {
            summary =
                summary[..1500];
        }

        _db.HelpdeskTicketEvents.Add(
            new HelpdeskTicketEvent(
                organizationId,
                ticket.Id,
                actorUserId,
                "reopened",
                summary));

        await _db.SaveChangesAsync(
            cancellationToken);

        return await GetTicketAsync(
            organizationId,
            ticket.Id,
            cancellationToken);
    }
}