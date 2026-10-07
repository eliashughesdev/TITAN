using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;

using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/my/helpdesk/request-form")]
public sealed class HelpdeskRequestFormController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    private readonly IHelpdeskService
        _helpdesk;

    public HelpdeskRequestFormController(
        TitanMdmDbContext db,
        IHelpdeskService helpdesk)
    {
        _db =
            db;

        _helpdesk =
            helpdesk;
    }

    // ============================================================
    // GROUPS AVAILABLE TO REQUESTER
    // ============================================================

    [HttpGet("groups")]
    public async Task<IActionResult>
        Groups(
            CancellationToken cancellationToken)
    {
        if (
            !Identity(
                out var organizationId,
                out var actorUserId))
        {
            return Unauthorized();
        }

        if (
            !CanUsePortal())
        {
            return Forbid();
        }

        var activeUser =
            await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                            actorUserId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!activeUser)
        {
            return Forbid();
        }

        /*
         * Solo devolvemos grupos activos
         * con al menos una categoría.
         *
         * No queremos enseñar al colaborador
         * grupos internos o incompletos.
         */
        var groups =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .OrderBy(
                    x =>
                        x.Name)
                .ToListAsync(
                    cancellationToken);

        var result =
            groups
                .Select(
                    group =>
                        new
                        {
                            group.Id,
                            group.Name,

                            categories =
                                (group.Categories ?? "")
                                    .Split(
                                        '|',
                                        StringSplitOptions
                                            .RemoveEmptyEntries
                                        |
                                        StringSplitOptions
                                            .TrimEntries)
                                    .Distinct(
                                        StringComparer
                                            .OrdinalIgnoreCase)
                                    .OrderBy(
                                        x => x)
                                    .ToArray()
                        })
                .Where(
                    x =>
                        x.categories.Length >
                        0)
                .ToArray();

        return Ok(
            result);
    }

    // ============================================================
    // CREATE
    // ============================================================

    [HttpPost("tickets")]
    public async Task<IActionResult>
        Create(
            [FromBody]
            CreateRequest request,
            CancellationToken cancellationToken)
    {
        if (
            !Identity(
                out var organizationId,
                out var actorUserId))
        {
            return Unauthorized();
        }

        if (
            !HasAnyPermission(
                "helpdesk.request.create",
                "tickets.create"))
        {
            return Forbid();
        }

        if (
            _helpdesk
            is not HelpdeskService service)
        {
            return Problem(
                title:
                    "Servicio de Helpdesk no disponible.",
                detail:
                    "La implementación actual no admite solicitudes agrupadas.");
        }

        var canUseConsole =
            request.Console
            &&
            HasAnyPermission(
                "helpdesk.agent.access",
                "helpdesk.admin.access",
                "helpdesk.ticket.details.view",
                "helpdesk.view",
                "tickets.view",
                "tickets.comment",
                "helpdesk.manage");

        try
        {
            var id =
                await service
                    .CreateGroupedRequestAsync(
                        organizationId,
                        actorUserId,
                        request.GroupId,
                        request.Subject,
                        request.Description,
                        request.Type,
                        request.Category,
                        canUseConsole,
                        cancellationToken);

            return Ok(
                new
                {
                    id
                });
        }
        catch (
            ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    // ============================================================
    // DELETE GROUP
    // ============================================================

    [HttpDelete("groups/{groupId:guid}")]
    public async Task<IActionResult>
        Delete(
            Guid groupId,
            CancellationToken cancellationToken)
    {
        if (
            !Identity(
                out var organizationId,
                out var actorUserId))
        {
            return Unauthorized();
        }

        if (
            !HasAnyPermission(
                "helpdesk.groups.manage",
                "helpdesk.admin.access",
                "helpdesk.manage",
                "settings.manage"))
        {
            return Forbid();
        }

        var actorExists =
            await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                            actorUserId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!actorExists)
        {
            return Forbid();
        }

        var group =
            await _db.HelpdeskTeams
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            groupId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            group is null)
        {
            return NotFound();
        }

        group.SetActive(
            false);

        await _db.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new
            {
                message =
                    "Grupo desactivado. " +
                    "Los tickets históricos conservan su referencia."
            });
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private bool CanUsePortal()
    {
        return HasAnyPermission(
            "helpdesk.portal.access",
            "helpdesk.request.create",
            "helpdesk.request.own.view",
            "helpdesk.agent.access",
            "helpdesk.admin.access",
            "tickets.create",
            "helpdesk.view",
            "tickets.view");
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return permissions.Any(
            Has);
    }

    private bool Has(
        string code)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                string.Equals(
                    claim.Value,
                    code,
                    StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private bool Identity(
        out Guid organizationId,
        out Guid actorUserId)
    {
        var organization =
            Guid.TryParse(
                User.FindFirstValue(
                    "organization_id")
                ??
                User.FindFirstValue(
                    "organizationId"),
                out organizationId);

        var actor =
            Guid.TryParse(
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
                    "userId"),
                out actorUserId);

        return
            organization &&
            actor;
    }

    public sealed record CreateRequest(
        Guid GroupId,
        string Subject,
        string Description,
        string Type,
        string Category,
        bool Console = false);
}