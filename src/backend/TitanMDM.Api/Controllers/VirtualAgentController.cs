using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/virtual-agent")]
public sealed class VirtualAgentController : ControllerBase
{
    private readonly TitanMdmDbContext _db;
    private readonly IHelpdeskService _helpdesk;

    public VirtualAgentController(
        TitanMdmDbContext db,
        IHelpdeskService helpdesk)
    {
        _db = db;
        _helpdesk = helpdesk;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(
        CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(cancellationToken);
        if (identity is null)
            return Unauthorized();

        if (!identity.AssistantEnabled)
            return Forbid();

        return Ok(new
        {
            user = new
            {
                identity.UserId,
                identity.OrganizationId,
                identity.Name,
                identity.Email
            },
            roles = identity.Roles,
            permissions = identity.Permissions,
            tools = AvailableTools(identity),
            generatedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] AgentActionRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(cancellationToken);
        if (identity is null)
            return Unauthorized();

        if (!identity.AssistantEnabled)
            return Forbid();

        var action = request.Action?.Trim().ToLowerInvariant();

        return action switch
        {
            "helpdesk.my_tickets" =>
                await ListMyTicketsAsync(
                    identity,
                    cancellationToken),

            "helpdesk.my_ticket" =>
                await GetMyTicketAsync(
                    identity,
                    request,
                    cancellationToken),

            "helpdesk.create_ticket" =>
                await CreateMyTicketAsync(
                    identity,
                    request,
                    cancellationToken),

            _ => BadRequest(new
            {
                message = "Herramienta del agente virtual no reconocida."
            })
        };
    }

    private async Task<IActionResult> ListMyTicketsAsync(
        AgentIdentity identity,
        CancellationToken cancellationToken)
    {
        var tickets = await _db.HelpdeskTickets
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == identity.OrganizationId &&
                x.RequesterUserId == identity.UserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Subject,
                x.Status,
                x.Priority,
                x.Category,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            action = "helpdesk.my_tickets",
            items = tickets
        });
    }

    private async Task<IActionResult> GetMyTicketAsync(
        AgentIdentity identity,
        AgentActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.TicketId.HasValue ||
            request.TicketId.Value == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Indica el identificador del ticket."
            });
        }

        var ticket = await _db.HelpdeskTickets
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == identity.OrganizationId &&
                x.RequesterUserId == identity.UserId &&
                x.Id == request.TicketId.Value)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Subject,
                x.Description,
                x.Status,
                x.Priority,
                x.Category,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
            return NotFound(new
            {
                message = "El ticket no existe entre tus solicitudes."
            });

        return Ok(new
        {
            action = "helpdesk.my_ticket",
            item = ticket
        });
    }

    private async Task<IActionResult> CreateMyTicketAsync(
        AgentIdentity identity,
        AgentActionRequest request,
        CancellationToken cancellationToken)
    {
        // La concesión del asistente no sustituye los permisos
        // funcionales del usuario conectado.
        if (!identity.Permissions.Contains(
                "tickets.create",
                StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var subject = request.Subject?.Trim();
        var description = request.Description?.Trim();

        if (string.IsNullOrWhiteSpace(subject) ||
            subject.Length > 250 ||
            string.IsNullOrWhiteSpace(description) ||
            description.Length > 4000)
        {
            return BadRequest(new
            {
                message =
                    "Indica asunto y descripción válidos para crear el ticket."
            });
        }

        var category = string.IsNullOrWhiteSpace(request.Category)
            ? "general"
            : request.Category.Trim();

        if (category.Length > 80)
        {
            return BadRequest(new
            {
                message = "La categoría no puede superar 80 caracteres."
            });
        }

        try
        {
            var ticket = await _helpdesk.CreateTicketAsync(
                identity.OrganizationId,
                identity.UserId,
                new CreateHelpdeskTicketRequest(
                    subject,
                    description,
                    "incident",
                    "medium",
                    category,
                    "assistant",
                    null,
                    identity.UserId,
                    null),
                cancellationToken);

            // CreateTicketAsync registra los eventos de creación y
            // de asignación. No devolvemos el detalle TIC, que puede
            // incluir notas internas.
            return Ok(new
            {
                action = "helpdesk.create_ticket",
                ticket.Id,
                ticket.Number,
                message = "Ticket creado para el usuario en sesión."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<AgentIdentity?> ResolveIdentityAsync(
        CancellationToken cancellationToken)
    {
        var organizationClaim =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        var userClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("user_id") ??
            User.FindFirstValue("userId");

        if (!Guid.TryParse(organizationClaim, out var organizationId) ||
            !Guid.TryParse(userClaim, out var userId))
            return null;

        var user = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.Id == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.FirstName,
                x.LastName,
                x.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var assistantEnabled = await _db.HelpdeskAssistantAccess
            .AsNoTracking()
            .AnyAsync(x =>
                x.OrganizationId == organizationId &&
                x.UserId == userId &&
                x.IsEnabled,
                cancellationToken);

        var roles = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == userId &&
                  role.OrganizationId == organizationId &&
                  role.IsActive
            select role.Name
        )
        .Distinct()
        .ToListAsync(cancellationToken);

        var permissions = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            join rolePermission in _db.RolePermissions.AsNoTracking()
                on role.Id equals rolePermission.RoleId
            join permission in _db.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId &&
                  role.OrganizationId == organizationId &&
                  role.IsActive &&
                  permission.IsActive
            select permission.Code
        )
        .Distinct()
        .ToListAsync(cancellationToken);

        return new AgentIdentity(
            organizationId,
            userId,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email,
            assistantEnabled,
            roles,
            permissions);
    }

    private static string[] AvailableTools(
        AgentIdentity identity)
    {
        var tools = new List<string>
        {
            "helpdesk.my_tickets",
            "helpdesk.my_ticket"
        };

        if (identity.Permissions.Contains(
                "tickets.create",
                StringComparer.OrdinalIgnoreCase))
        {
            tools.Add("helpdesk.create_ticket");
        }

        return tools.ToArray();
    }

    public sealed record AgentActionRequest(
        string? Action,
        Guid? TicketId,
        string? Subject,
        string? Description,
        string? Category);

    private sealed record AgentIdentity(
        Guid OrganizationId,
        Guid UserId,
        string Name,
        string Email,
        bool AssistantEnabled,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions);
}