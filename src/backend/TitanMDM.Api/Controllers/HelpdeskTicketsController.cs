using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Helpdesk;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/tickets")]
public sealed class HelpdeskTicketsController
    : ControllerBase
{
    // ============================================================
    // MODERN HELPDESK RBAC
    // ============================================================

    private const string PortalAccess =
        "helpdesk.portal.access";

    private const string AgentAccess =
        "helpdesk.agent.access";

    private const string AdminAccess =
        "helpdesk.admin.access";

    private const string RequestCreate =
        "helpdesk.request.create";

    private const string InboxMyWork =
        "helpdesk.inbox.my-work";

    private const string InboxUnassigned =
        "helpdesk.inbox.unassigned";

    private const string InboxAll =
        "helpdesk.inbox.all";

    private const string KanbanView =
        "helpdesk.kanban.view";

    private const string TicketDetails =
        "helpdesk.ticket.details.view";

    private const string TicketComment =
        "helpdesk.ticket.comment";

    private const string TicketInternalNote =
        "helpdesk.ticket.internal-note";

    private const string TicketTake =
        "helpdesk.ticket.take";

    private const string TicketAssign =
        "helpdesk.ticket.assign";

    private const string TicketTransition =
        "helpdesk.ticket.transition";

    private const string TicketResolve =
        "helpdesk.ticket.resolve";

    private const string TicketClose =
        "helpdesk.ticket.close";

    private const string TicketReopen =
        "helpdesk.ticket.reopen";

    // ============================================================
    // LEGACY COMPATIBILITY
    // ============================================================

    private const string LegacyHelpdeskView =
        "helpdesk.view";

    private const string LegacyHelpdeskManage =
        "helpdesk.manage";

    private const string LegacyTicketsView =
        "tickets.view";

    private const string LegacyTicketsCreate =
        "tickets.create";

    private const string LegacyTicketsAssign =
        "tickets.assign";

    private const string LegacyTicketsComment =
        "tickets.comment";

    private const string LegacyTicketsClose =
        "tickets.close";

    private readonly IHelpdeskService
        _helpdeskService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public HelpdeskTicketsController(
        IHelpdeskService helpdeskService,
        IScopeAccessService scopeAccessService)
    {
        _helpdeskService =
            helpdeskService;

        _scopeAccessService =
            scopeAccessService;
    }

    // ============================================================
    // LIST
    // ============================================================

    [HttpGet]
    public async Task<IActionResult>
        GetTickets(
            [FromQuery]
            string? search,

            [FromQuery]
            string? status,

            [FromQuery]
            string? priority,

            [FromQuery]
            Guid? deviceId,

            [FromQuery]
            Guid? assigneeUserId,

            [FromQuery]
            int page = 1,

            [FromQuery]
            int pageSize = 25,

            CancellationToken cancellationToken = default)
    {
        if (!CanViewWorkQueues())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        var organizationWide =
            await _scopeAccessService
                .HasOrganizationScopeAsync(
                    identity.Value.OrganizationId,
                    identity.Value.UserId,
                    cancellationToken);

        if (!organizationWide)
        {
            return Forbid();
        }

        var result =
            await _helpdeskService
                .GetTicketsAsync(
                    identity.Value.OrganizationId,
                    new HelpdeskTicketQuery(
                        search,
                        status,
                        priority,
                        deviceId,
                        assigneeUserId,
                        page,
                        pageSize),
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult>
        GetTicket(
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        if (!CanViewTicketDetails())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        if (!await CanAccessTicketAsync(
                identity.Value,
                ticketId,
                cancellationToken))
        {
            return Forbid();
        }

        var ticket =
            await _helpdeskService
                .GetTicketAsync(
                    identity.Value.OrganizationId,
                    ticketId,
                    cancellationToken);

        return ticket is null
            ? NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                })
            : Ok(
                ticket);
    }

    // ============================================================
    // CREATE
    // ============================================================

    [HttpPost]
    public async Task<IActionResult>
        CreateTicket(
            [FromBody]
            CreateHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission(
                RequestCreate,
                AdminAccess,
                LegacyTicketsCreate,
                LegacyHelpdeskManage))
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        try
        {
            var created =
                await _helpdeskService
                    .CreateTicketAsync(
                        identity.Value.OrganizationId,
                        identity.Value.UserId,
                        request,
                        cancellationToken);

            return Ok(
                created);
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
    // COMMENT / INTERNAL NOTE
    // ============================================================

    [HttpPost("{ticketId:guid}/comments")]
    public async Task<IActionResult>
        AddComment(
            Guid ticketId,
            [FromBody]
            AddHelpdeskCommentRequest request,
            CancellationToken cancellationToken = default)
    {
        var requiredPermission =
            request.IsInternal
                ? TicketInternalNote
                : TicketComment;

        if (!HasAnyPermission(
                requiredPermission,
                AdminAccess,
                LegacyTicketsComment,
                LegacyHelpdeskManage))
        {
            return Forbid();
        }

        if (!CanViewTicketDetails())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        if (!await CanAccessTicketAsync(
                identity.Value,
                ticketId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var ticket =
                await _helpdeskService
                    .AddCommentAsync(
                        identity.Value.OrganizationId,
                        ticketId,
                        identity.Value.UserId,
                        request,
                        cancellationToken);

            return ticket is null
                ? NotFound(
                    new
                    {
                        message =
                            "El ticket no existe."
                    })
                : Ok(
                    ticket);
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
    // ASSIGN
    // ============================================================

    [HttpPost("{ticketId:guid}/assign")]
    public async Task<IActionResult>
        Assign(
            Guid ticketId,
            [FromBody]
            AssignHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission(
                TicketAssign,
                TicketTake,
                AdminAccess,
                LegacyTicketsAssign,
                LegacyHelpdeskManage))
        {
            return Forbid();
        }

        if (!CanViewTicketDetails())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        if (!await CanAccessTicketAsync(
                identity.Value,
                ticketId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var ticket =
                await _helpdeskService
                    .AssignAsync(
                        identity.Value.OrganizationId,
                        ticketId,
                        identity.Value.UserId,
                        request,
                        cancellationToken);

            return ticket is null
                ? NotFound(
                    new
                    {
                        message =
                            "El ticket no existe."
                    })
                : Ok(
                    ticket);
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
    // STATUS TRANSITION
    // ============================================================

    [HttpPost("{ticketId:guid}/transition")]
    public async Task<IActionResult>
        Transition(
            Guid ticketId,
            [FromBody]
            TransitionHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!CanViewTicketDetails())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        string targetStatus;

        try
        {
            targetStatus =
                HelpdeskTicketStatus.Normalize(
                    request.Status);
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

        // ========================================================
        // OPERATIONAL TRANSITIONS
        // ========================================================

        if (targetStatus is
                HelpdeskTicketStatus.Open
                or
                HelpdeskTicketStatus.InProgress
                or
                HelpdeskTicketStatus.PendingUser)
        {
            if (!HasAnyPermission(
                    TicketTransition,
                    TicketComment,
                    AdminAccess,
                    LegacyTicketsComment,
                    LegacyHelpdeskManage))
            {
                return Forbid();
            }
        }

        // ========================================================
        // RESOLVE
        // ========================================================

        else if (
            targetStatus ==
            HelpdeskTicketStatus.Resolved)
        {
            if (!HasAnyPermission(
                    TicketResolve,
                    AdminAccess,
                    LegacyTicketsClose,
                    LegacyHelpdeskManage))
            {
                return Forbid();
            }
        }

        // ========================================================
        // CLOSE
        // ========================================================

        else if (
            targetStatus ==
            HelpdeskTicketStatus.Closed)
        {
            if (!HasAnyPermission(
                    TicketClose,
                    AdminAccess,
                    LegacyTicketsClose,
                    LegacyHelpdeskManage))
            {
                return Forbid();
            }
        }
        else
        {
            return BadRequest(
                new
                {
                    message =
                        "El estado solicitado no está permitido."
                });
        }

        if (!await CanAccessTicketAsync(
                identity.Value,
                ticketId,
                cancellationToken))
        {
            return Forbid();
        }

        var current =
            await _helpdeskService
                .GetTicketAsync(
                    identity.Value.OrganizationId,
                    ticketId,
                    cancellationToken);

        if (current is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                });
        }

        /*
         * La reapertura siempre debe utilizar /reopen
         * porque necesita motivo obligatorio y auditoría.
         */
        if (HelpdeskTicketStatus.IsTerminal(
                current.Status)
            &&
            targetStatus ==
                HelpdeskTicketStatus.Open)
        {
            return Conflict(
                new
                {
                    message =
                        "Los tickets resueltos o cerrados deben reabrirse mediante la acción Reabrir."
                });
        }

        /*
         * Validamos antes de entrar al servicio para devolver
         * un error más claro al Kanban/UI.
         */
        if (!HelpdeskTicketStatus.CanTransition(
                current.Status,
                targetStatus))
        {
            return Conflict(
                new
                {
                    message =
                        $"No se permite mover el ticket de " +
                        $"'{current.Status}' a '{targetStatus}'."
                });
        }

        try
        {
            var ticket =
                await _helpdeskService
                    .TransitionAsync(
                        identity.Value.OrganizationId,
                        ticketId,
                        identity.Value.UserId,
                        new TransitionHelpdeskTicketRequest(
                            targetStatus),
                        cancellationToken);

            return ticket is null
                ? NotFound(
                    new
                    {
                        message =
                            "El ticket no existe."
                    })
                : Ok(
                    ticket);
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
    // REOPEN
    // ============================================================

    [HttpPost("{ticketId:guid}/reopen")]
    public async Task<IActionResult>
        Reopen(
            Guid ticketId,
            [FromBody]
            ReopenHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!HasAnyPermission(
                TicketReopen,
                AdminAccess,
                LegacyTicketsClose,
                LegacyHelpdeskManage))
        {
            return Forbid();
        }

        if (!CanViewTicketDetails())
        {
            return Forbid();
        }

        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        if (!await CanAccessTicketAsync(
                identity.Value,
                ticketId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var ticket =
                await _helpdeskService
                    .ReopenAsync(
                        identity.Value.OrganizationId,
                        ticketId,
                        identity.Value.UserId,
                        request,
                        cancellationToken);

            return ticket is null
                ? NotFound(
                    new
                    {
                        message =
                            "El ticket no existe."
                    })
                : Ok(
                    ticket);
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
    // ACCESS
    // ============================================================

    private bool CanViewWorkQueues()
    {
        return HasAnyPermission(
            InboxMyWork,
            InboxUnassigned,
            InboxAll,
            KanbanView,
            AgentAccess,
            AdminAccess,
            LegacyHelpdeskView,
            LegacyTicketsView,
            LegacyHelpdeskManage);
    }

    private bool CanViewTicketDetails()
    {
        return HasAnyPermission(
            TicketDetails,
            AgentAccess,
            AdminAccess,
            LegacyHelpdeskView,
            LegacyTicketsView,
            LegacyHelpdeskManage);
    }

    private async Task<bool>
        CanAccessTicketAsync(
            (
                Guid OrganizationId,
                Guid UserId
            ) identity,
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        return await _scopeAccessService
            .CanAccessTicketAsync(
                identity.OrganizationId,
                identity.UserId,
                ticketId,
                cancellationToken);
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private (
        Guid OrganizationId,
        Guid UserId)?
        GetIdentity()
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null
            ||
            userId is null)
        {
            return null;
        }

        return (
            organizationId.Value,
            userId.Value);
    }

    private Guid?
        GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out var organizationId)
            ? organizationId
            : null;
    }

    private Guid?
        GetUserId()
    {
        var value =
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

        return Guid.TryParse(
            value,
            out var userId)
            ? userId
            : null;
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private bool HasPermission(
        string permission)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return permissions.Any(
            HasPermission);
    }
}