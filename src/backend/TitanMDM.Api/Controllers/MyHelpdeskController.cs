using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/my/helpdesk")]
public sealed class MyHelpdeskController
    : ControllerBase
{
    // ============================================================
    // MODERN PORTAL PERMISSIONS
    // ============================================================

    private const string PortalAccess =
        "helpdesk.portal.access";

    private const string RequestCreate =
        "helpdesk.request.create";

    private const string RequestOwnView =
        "helpdesk.request.own.view";

    private const string RequestOwnComment =
        "helpdesk.request.own.comment";

    private const string RequestOwnReopen =
        "helpdesk.request.own.reopen";

    private const string RequestOwnConfirm =
        "helpdesk.request.own.confirm";

    private const string AdminAccess =
        "helpdesk.admin.access";

    // ============================================================
    // LEGACY COMPATIBILITY
    // ============================================================

    private const string LegacyTicketsCreate =
        "tickets.create";

    private const string LegacyHelpdeskManage =
        "helpdesk.manage";

    private readonly TitanMdmDbContext
        _db;

    private readonly IHelpdeskService
        _helpdesk;

    public MyHelpdeskController(
        TitanMdmDbContext db,
        IHelpdeskService helpdesk)
    {
        _db =
            db;

        _helpdesk =
            helpdesk;
    }

    // ============================================================
    // MY TICKETS
    // ============================================================

    [HttpGet("tickets")]
    public async Task<IActionResult>
        GetMyTickets(
            CancellationToken cancellationToken)
    {
        if (!CanViewOwnTickets())
        {
            return Forbid();
        }

        if (!TryGetIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var tickets =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RequesterUserId ==
                            userId)
                .OrderByDescending(
                    x =>
                        x.UpdatedAtUtc)
                .ThenByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Number,
                            x.Subject,
                            x.Description,
                            x.Status,
                            x.Priority,
                            x.Category,
                            x.CreatedAtUtc,
                            x.UpdatedAtUtc,
                            x.FirstResponseDueAtUtc,
                            x.ResolveDueAtUtc,
                            x.ResolvedAtUtc
                        })
                .Take(
                    100)
                .ToListAsync(
                    cancellationToken);

        return Ok(
            tickets);
    }

    // ============================================================
    // MY TICKET DETAIL
    // ============================================================

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult>
        GetMyTicket(
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        if (!CanViewOwnTickets())
        {
            return Forbid();
        }

        if (!TryGetIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RequesterUserId ==
                            userId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        if (ticket is null)
        {
            return NotFound(
                new
                {
                    message =
                        "La solicitud no existe."
                });
        }

        var comments =
            await (
                from comment
                    in _db.HelpdeskTicketComments
                        .AsNoTracking()

                join author
                    in _db.Users
                        .AsNoTracking()
                    on comment.AuthorUserId
                    equals author.Id

                where
                    comment.OrganizationId ==
                        organizationId
                    &&
                    comment.TicketId ==
                        ticketId
                    &&
                    !comment.IsInternal
                    &&
                    author.OrganizationId ==
                        organizationId

                orderby
                    comment.CreatedAtUtc

                select new
                {
                    comment.Id,
                    comment.Body,
                    comment.CreatedAtUtc,

                    authorUserId =
                        author.Id,

                    authorName =
                        (
                            author.FirstName +
                            " " +
                            author.LastName
                        )
                        .Trim()
                }
            )
            .ToListAsync(
                cancellationToken);

        /*
         * Solo eventos seguros para el solicitante.
         *
         * Nunca exponemos:
         * - routing interno
         * - notas internas
         * - reglas administrativas
         * - cambios de infraestructura
         */
        var publicTypes =
            new[]
            {
                "created",
                "comment",
                "status",
                "requester_reply",
                "reopened"
            };

        var activity =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TicketId ==
                            ticketId
                        &&
                        publicTypes.Contains(
                            x.EventType))
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.EventType,
                            x.Summary,
                            x.CreatedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var automationSettings =
            await _db
                .Set<HelpdeskAutomationSettings>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken)
            ??
            new HelpdeskAutomationSettings(
                organizationId);

        var reopenWindowDays =
            automationSettings.ReopenDays;

        var canReopen =
            HelpdeskTicketStatus.IsTerminal(
                ticket.Status)
            &&
            ticket.ResolvedAtUtc.HasValue
            &&
            ticket.ResolvedAtUtc.Value
                .AddDays(
                    reopenWindowDays)
                >=
                DateTime.UtcNow
            &&
            CanReopenOwnTicket();

        var slaPaused =
            ticket.Status ==
            HelpdeskTicketStatus.PendingUser;

        var now =
            DateTime.UtcNow;

        var firstResponseBreached =
            !slaPaused
            &&
            ticket.FirstRespondedAtUtc is null
            &&
            ticket.FirstResponseDueAtUtc.HasValue
            &&
            ticket.FirstResponseDueAtUtc.Value <
                now;

        var resolutionBreached =
            !slaPaused
            &&
            ticket.ResolvedAtUtc is null
            &&
            ticket.ResolveDueAtUtc.HasValue
            &&
            ticket.ResolveDueAtUtc.Value <
                now;

        return Ok(
            new
            {
                ticket.Id,
                ticket.Number,
                ticket.Subject,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.Category,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.FirstResponseDueAtUtc,
                ticket.ResolveDueAtUtc,
                ticket.ResolvedAtUtc,

                slaPaused,

                slaBreached =
                    firstResponseBreached
                    ||
                    resolutionBreached,

                canReply =
                    !HelpdeskTicketStatus
                        .IsTerminal(
                            ticket.Status)
                    &&
                    CanCommentOwnTicket(),

                canReopen,

                reopenWindowDays,

                comments,

                activity
            });
    }

    // ============================================================
    // CREATE MY TICKET
    // ============================================================

    [HttpPost("tickets")]
    public async Task<IActionResult>
        CreateMyTicket(
            [FromBody]
            CreateMyTicketRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanCreateOwnTicket())
        {
            return Forbid();
        }

        if (!TryGetIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var subject =
            request.Subject?
                .Trim();

        var description =
            request.Description?
                .Trim();

        if (string.IsNullOrWhiteSpace(
                subject)
            ||
            subject.Length > 250
            ||
            string.IsNullOrWhiteSpace(
                description)
            ||
            description.Length > 4000)
        {
            return BadRequest(
                new
                {
                    message =
                        "Indica asunto y descripción dentro de los límites permitidos."
                });
        }

        if (request.DeviceId.HasValue)
        {
            var deviceExists =
                await _db.Devices
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id ==
                                request.DeviceId.Value
                            &&
                            x.OrganizationId ==
                                organizationId,
                        cancellationToken);

            if (!deviceExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El dispositivo no es válido."
                    });
            }
        }

        try
        {
            var priority =
                request.Priority?
                    .Trim()
                    .ToLowerInvariant();

            if (priority is not (
                    "low"
                    or
                    "medium"
                    or
                    "high"
                    or
                    "critical"))
            {
                priority =
                    "medium";
            }

            var type =
                request.Type?
                    .Trim()
                    .ToLowerInvariant();

            if (type is not (
                    "incident"
                    or
                    "request"))
            {
                type =
                    "incident";
            }

            var category =
                string.IsNullOrWhiteSpace(
                    request.Category)
                    ? "general"
                    : request.Category
                        .Trim();

            var created =
                await _helpdesk
                    .CreateTicketAsync(
                        organizationId,
                        userId,
                        new CreateHelpdeskTicketRequest(
                            subject,
                            description,
                            type,
                            priority,
                            category,
                            "selfservice",
                            request.DeviceId,
                            userId,
                            null),
                        cancellationToken);

            return CreatedAtAction(
                nameof(GetMyTicket),
                new
                {
                    ticketId =
                        created.Id
                },
                new
                {
                    created.Id,
                    created.Number
                });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // REQUESTER REPLY
    // ============================================================

    [HttpPost("tickets/{ticketId:guid}/reply")]
    public async Task<IActionResult>
        Reply(
            Guid ticketId,
            [FromBody]
            MyTicketReplyRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanCommentOwnTicket())
        {
            return Forbid();
        }

        if (!TryGetIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var body =
            request.Body?
                .Trim();

        if (string.IsNullOrWhiteSpace(
                body)
            ||
            body.Length > 4000)
        {
            return BadRequest(
                new
                {
                    message =
                        "La respuesta debe tener entre 1 y 4000 caracteres."
                });
        }

        var ticket =
            await _db.HelpdeskTickets
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            ticketId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RequesterUserId ==
                            userId,
                    cancellationToken);

        if (ticket is null)
        {
            return NotFound(
                new
                {
                    message =
                        "La solicitud no existe."
                });
        }

        if (HelpdeskTicketStatus.IsTerminal(
                ticket.Status))
        {
            return Conflict(
                new
                {
                    message =
                        "Este ticket está finalizado. Puedes reabrirlo si todavía está dentro del período permitido."
                });
        }

        _db.HelpdeskTicketComments.Add(
            new HelpdeskTicketComment(
                organizationId,
                ticketId,
                userId,
                body,
                isInternal: false));

        _db.HelpdeskTicketEvents.Add(
            new HelpdeskTicketEvent(
                organizationId,
                ticketId,
                userId,
                "requester_reply",
                "El solicitante respondió al ticket."));

        /*
         * ========================================================
         * WAITING USER -> ACTIVE
         * ========================================================
         *
         * Si existe técnico:
         *      pendinguser -> inprogress
         *
         * Si no existe técnico:
         *      pendinguser -> open
         *
         * Transition() reanuda automáticamente el SLA.
         * ========================================================
         */

        if (ticket.Status ==
            HelpdeskTicketStatus.PendingUser)
        {
            var previousStatus =
                ticket.Status;

            var targetStatus =
                ticket.AssigneeUserId.HasValue
                    ? HelpdeskTicketStatus.InProgress
                    : HelpdeskTicketStatus.Open;

            ticket.Transition(
                targetStatus);

            _db.HelpdeskTicketEvents.Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticketId,
                    userId,
                    "status",
                    $"Estado actualizado de {previousStatus} a {targetStatus} después de la respuesta del solicitante."));
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        return await GetMyTicket(
            ticketId,
            cancellationToken);
    }

    // ============================================================
    // REQUESTER REOPEN
    // ============================================================

    [HttpPost("tickets/{ticketId:guid}/reopen")]
    public async Task<IActionResult>
        ReopenMyTicket(
            Guid ticketId,
            [FromBody]
            ReopenMyTicketRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanReopenOwnTicket())
        {
            return Forbid();
        }

        if (!TryGetIdentity(
                out var organizationId,
                out var userId))
        {
            return Unauthorized();
        }

        var reason =
            request.Reason?
                .Trim();

        if (string.IsNullOrWhiteSpace(
                reason))
        {
            return BadRequest(
                new
                {
                    message =
                        "Debes indicar por qué necesitas reabrir la solicitud."
                });
        }

        if (reason.Length < 5)
        {
            return BadRequest(
                new
                {
                    message =
                        "El motivo debe tener al menos 5 caracteres."
                });
        }

        if (reason.Length > 1000)
        {
            return BadRequest(
                new
                {
                    message =
                        "El motivo no puede exceder 1000 caracteres."
                });
        }

        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            ticketId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.RequesterUserId ==
                            userId,
                    cancellationToken);

        if (ticket is null)
        {
            return NotFound(
                new
                {
                    message =
                        "La solicitud no existe."
                });
        }

        if (!HelpdeskTicketStatus.IsTerminal(
                ticket.Status))
        {
            return Conflict(
                new
                {
                    message =
                        "Solo puedes reabrir solicitudes resueltas o cerradas."
                });
        }

        if (!ticket.ResolvedAtUtc.HasValue)
        {
            return Conflict(
                new
                {
                    message =
                        "La solicitud no dispone de una fecha de resolución válida."
                });
        }

        var automationSettings =
            await _db
                .Set<HelpdeskAutomationSettings>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken)
            ??
            new HelpdeskAutomationSettings(
                organizationId);

        var reopenWindowDays =
            automationSettings.ReopenDays;

        var reopenDeadline =
            ticket.ResolvedAtUtc.Value
                .AddDays(
                    reopenWindowDays);

        if (DateTime.UtcNow >
            reopenDeadline)
        {
            return Conflict(
                new
                {
                    message =
                        $"El período de reapertura de {reopenWindowDays} días ya venció."
                });
        }

        try
        {
            var reopened =
                await _helpdesk
                    .ReopenAsync(
                        organizationId,
                        ticketId,
                        userId,
                        new ReopenHelpdeskTicketRequest(
                            reason),
                        cancellationToken);

            if (reopened is null)
            {
                return NotFound(
                    new
                    {
                        message =
                            "La solicitud no existe."
                    });
            }

            return await GetMyTicket(
                ticketId,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private bool TryGetIdentity(
        out Guid organizationId,
        out Guid userId)
    {
        var organizationValue =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub")
            ??
            User.FindFirstValue(
                "user_id")
            ??
            User.FindFirstValue(
                "userId");

        var validOrganization =
            Guid.TryParse(
                organizationValue,
                out organizationId);

        var validUser =
            Guid.TryParse(
                userValue,
                out userId);

        return validOrganization
            &&
            validUser;
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private bool CanViewOwnTickets()
    {
        return HasAnyPermission(
            PortalAccess,
            RequestOwnView,
            RequestCreate,
            AdminAccess,
            LegacyTicketsCreate,
            LegacyHelpdeskManage);
    }

    private bool CanCreateOwnTicket()
    {
        return HasAnyPermission(
            PortalAccess,
            RequestCreate,
            AdminAccess,
            LegacyTicketsCreate,
            LegacyHelpdeskManage);
    }

    private bool CanCommentOwnTicket()
    {
        return HasAnyPermission(
            PortalAccess,
            RequestOwnComment,
            AdminAccess,
            LegacyHelpdeskManage);
    }

    private bool CanReopenOwnTicket()
    {
        return HasAnyPermission(
            PortalAccess,
            RequestOwnReopen,
            RequestOwnConfirm,
            AdminAccess,
            LegacyHelpdeskManage);
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return permissions.Any(
            permission =>
                User.Claims.Any(
                    claim =>
                        claim.Type ==
                            "permission"
                        &&
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }

    // ============================================================
    // CONTRACTS
    // ============================================================

    public sealed record CreateMyTicketRequest(
        string Subject,
        string Description,
        string? Type,
        string? Priority,
        string? Category,
        Guid? DeviceId);

    public sealed record MyTicketReplyRequest(
        string Body);

    public sealed record ReopenMyTicketRequest(
        string Reason);
}