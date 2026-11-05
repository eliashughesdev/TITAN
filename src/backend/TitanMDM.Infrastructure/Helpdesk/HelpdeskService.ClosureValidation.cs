using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Domain.Helpdesk;

namespace TitanMDM.Infrastructure.Helpdesk;

/*
 * P1 Helpdesk - Validacion de cierre de tickets.
 *
 * Un ticket no puede pasar a Resuelto/Cerrado mientras
 * tenga trabajo tecnico en curso asociado:
 *   - sesion de soporte remoto activa;
 *   - comandos MDM sobre el dispositivo del ticket que
 *     siguen pendientes o ejecutandose.
 * La transicion se rechaza con InvalidOperationException
 * (HTTP 409) e indica que falta concluir.
 */
public sealed partial class HelpdeskService
{
    private static readonly DeviceCommandStatus[] InFlightCommandStatuses =
    {
        DeviceCommandStatus.Pending,
        DeviceCommandStatus.Queued,
        DeviceCommandStatus.Dispatching,
        DeviceCommandStatus.Sent,
        DeviceCommandStatus.Delivered,
        DeviceCommandStatus.Executing,
    };

    private static readonly RemoteSessionStatus[] ActiveRemoteSessionStatuses =
    {
        RemoteSessionStatus.Requested,
        RemoteSessionStatus.Connecting,
        RemoteSessionStatus.Connected,
        RemoteSessionStatus.Disconnecting,
    };

    internal async Task<IReadOnlyList<string>> GetPendingClosureBlockersAsync(
        HelpdeskTicket ticket,
        CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var now = DateTime.UtcNow;

        if (ticket.RemoteSessionId is { } remoteSessionId)
        {
            var sessionActive =
                await _db.RemoteSessions
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id == remoteSessionId &&
                            ActiveRemoteSessionStatuses.Contains(x.Status),
                        cancellationToken);

            if (sessionActive)
            {
                blockers.Add("la sesión de soporte remoto sigue activa");
            }
        }

        if (ticket.DeviceId is { } deviceId)
        {
            var pendingCommands =
                await _db.DeviceCommands
                    .AsNoTracking()
                    .Where(x =>
                        x.OrganizationId == ticket.OrganizationId &&
                        x.DeviceId == deviceId &&
                        x.CreatedAtUtc >= ticket.CreatedAtUtc &&
                        x.ExpiresAtUtc > now &&
                        InFlightCommandStatuses.Contains(x.Status))
                    .Select(x => x.CommandType)
                    .ToListAsync(cancellationToken);

            if (pendingCommands.Count > 0)
            {
                var summary =
                    string.Join(
                        ", ",
                        pendingCommands
                            .GroupBy(x => x)
                            .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
                            .Take(5));

                blockers.Add(
                    $"{pendingCommands.Count} comando(s) del dispositivo en curso ({summary})");
            }
        }

        return blockers;
    }

    private async Task EnsureTicketCanBeClosedAsync(
        HelpdeskTicket ticket,
        string targetStatus,
        CancellationToken cancellationToken)
    {
        var normalized = HelpdeskTicketStatus.Normalize(targetStatus);

        if (normalized is not (HelpdeskTicketStatus.Resolved or HelpdeskTicketStatus.Closed))
        {
            return;
        }

        var blockers = await GetPendingClosureBlockersAsync(ticket, cancellationToken);

        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                "No se puede " +
                (normalized == HelpdeskTicketStatus.Closed ? "cerrar" : "resolver") +
                $" el ticket {ticket.Number}: " +
                string.Join("; ", blockers) +
                ". Finaliza o cancela las tareas pendientes primero.");
        }
    }
}
