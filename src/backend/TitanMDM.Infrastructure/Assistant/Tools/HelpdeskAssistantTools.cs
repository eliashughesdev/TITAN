using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Assistant;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Assistant.Tools;

public sealed class HelpdeskMyTicketsTool
    : AssistantToolBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskMyTicketsTool(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "helpdesk.my_tickets",
            "Mis solicitudes de soporte",
            "Lista los tickets de soporte abiertos por el usuario en sesión, con estado, prioridad y vencimientos SLA.",
            AssistantToolKind.Read,
            Array.Empty<string>());

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var principal =
            context.Principal;

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    ticket =>
                        ticket.OrganizationId ==
                            principal.OrganizationId &&
                        ticket.RequesterUserId ==
                            principal.UserId)
                .OrderByDescending(
                    ticket =>
                        ticket.CreatedAtUtc)
                .Take(
                    25)
                .Select(
                    ticket =>
                        new
                        {
                            ticket.Id,
                            ticket.Number,
                            ticket.Subject,
                            ticket.Status,
                            ticket.Priority,
                            ticket.Category,
                            ticket.CreatedAtUtc,
                            ticket.UpdatedAtUtc,
                            ticket.FirstResponseDueAtUtc,
                            ticket.ResolveDueAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var summary =
            AssistantDataSanitizer.ToCompactJson(
                tickets,
                4000);

        return AssistantToolResult.Ok(
            $"El usuario tiene {tickets.Count} tickets propios.",
            AssistantDataSanitizer.WrapUntrustedData(
                "helpdesk.my_tickets",
                summary));
    }
}

public sealed class HelpdeskSlaStatusTool
    : AssistantToolBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskSlaStatusTool(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "helpdesk.sla_status",
            "Estado SLA de mis tickets",
            "Resume el cumplimiento de SLA (vencidos y por vencer) de los tickets abiertos por el usuario en sesión.",
            AssistantToolKind.Read,
            Array.Empty<string>());

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var principal =
            context.Principal;

        var now =
            DateTime.UtcNow;

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    ticket =>
                        ticket.OrganizationId ==
                            principal.OrganizationId &&
                        ticket.RequesterUserId ==
                            principal.UserId)
                .Select(
                    ticket =>
                        new
                        {
                            ticket.Number,
                            ticket.Subject,
                            ticket.Status,
                            ticket.FirstResponseDueAtUtc,
                            ticket.ResolveDueAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var open =
            tickets
                .Where(
                    ticket =>
                        !IsTerminal(
                            ticket.Status))
                .ToList();

        var overdue =
            open
                .Where(
                    ticket =>
                        IsOverdue(
                            ticket.ResolveDueAtUtc,
                            ticket.FirstResponseDueAtUtc,
                            now))
                .Select(
                    ticket =>
                        new
                        {
                            ticket.Number,
                            ticket.Subject,
                            ticket.FirstResponseDueAtUtc,
                            ticket.ResolveDueAtUtc
                        })
                .ToList();

        var payload =
            AssistantDataSanitizer.ToCompactJson(
                new
                {
                    totalOpen = open.Count,
                    overdue = overdue.Count,
                    overdueTickets = overdue
                },
                3000);

        return AssistantToolResult.Ok(
            open.Count == 0
                ? "El usuario no tiene tickets abiertos."
                : $"{overdue.Count} de {open.Count} tickets abiertos superaron su SLA.",
            AssistantDataSanitizer.WrapUntrustedData(
                "helpdesk.sla_status",
                payload));
    }

    private static bool IsTerminal(
        string status)
    {
        return string.Equals(
                   status,
                   "resolved",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   status,
                   "closed",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOverdue(
        DateTime? resolveDue,
        DateTime? firstResponseDue,
        DateTime now)
    {
        return (resolveDue.HasValue &&
                resolveDue.Value < now) ||
               (firstResponseDue.HasValue &&
                firstResponseDue.Value < now);
    }
}

public sealed class HelpdeskTicketTool
    : AssistantToolBase
{
    private readonly TitanMdmDbContext _db;
    private readonly IScopeAccessService _scopeAccess;

    public HelpdeskTicketTool(
        TitanMdmDbContext db,
        IScopeAccessService scopeAccess)
    {
        _db =
            db;

        _scopeAccess =
            scopeAccess;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "helpdesk.ticket",
            "Detalle de ticket autorizado",
            "Devuelve el detalle de un ticket por identificador, solo si el usuario tiene permiso y alcance sobre él.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Helpdesk.TicketsView
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var ticketId =
            ReadGuid(
                arguments,
                "ticketId");

        if (ticketId is null ||
            ticketId == Guid.Empty)
        {
            return AssistantToolResult.Fail(
                "invalid_argument",
                "Indica el identificador del ticket.");
        }

        var principal =
            context.Principal;

        var allowed =
            await _scopeAccess.CanAccessTicketAsync(
                principal.OrganizationId,
                principal.UserId,
                ticketId.Value,
                cancellationToken);

        if (!allowed)
        {
            return AssistantToolResult.Fail(
                "forbidden",
                "El ticket no está dentro de tu alcance.");
        }

        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    item =>
                        item.OrganizationId ==
                            principal.OrganizationId &&
                        item.Id == ticketId.Value)
                .Select(
                    item =>
                        new
                        {
                            item.Number,
                            item.Subject,
                            item.Status,
                            item.Priority,
                            item.Category,
                            item.DeviceId,
                            item.CreatedAtUtc,
                            item.UpdatedAtUtc,
                            item.FirstResponseDueAtUtc,
                            item.ResolveDueAtUtc
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (ticket is null)
        {
            return AssistantToolResult.Fail(
                "not_found",
                "El ticket no existe.");
        }

        return AssistantToolResult.Ok(
            $"Ticket {ticket.Number} en estado {ticket.Status}.",
            AssistantDataSanitizer.WrapUntrustedData(
                "helpdesk.ticket",
                AssistantDataSanitizer.ToCompactJson(
                    ticket,
                    3000)));
    }
}
