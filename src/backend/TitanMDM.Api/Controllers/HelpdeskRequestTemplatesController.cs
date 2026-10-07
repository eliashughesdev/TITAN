using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/my/helpdesk/templates")]
public sealed class HelpdeskRequestTemplatesController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    public HelpdeskRequestTemplatesController(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    private bool CanManage =>
        HasAnyPermission(
            "helpdesk.templates.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");

    private bool CanView =>
        CanManage
        ||
        HasAnyPermission(
            "helpdesk.templates.view",
            "helpdesk.portal.access",
            "helpdesk.request.create",
            "helpdesk.request.own.view",
            "tickets.create",
            "tickets.view",
            "helpdesk.view");

    // ============================================================
    // LIST
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery]
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        if (!CanView)
        {
            return Forbid();
        }

        if (all &&
            !CanManage)
        {
            return Forbid();
        }

        var identity =
            await GetIdentityAsync(
                cancellationToken);

        if (identity is null)
        {
            return Unauthorized();
        }

        var query =
            _db.Set<HelpdeskRequestTemplate>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId);

        if (!all)
        {
            query =
                query.Where(
                    x =>
                        x.IsActive);
        }

        var items =
            await query
                .OrderByDescending(
                    x =>
                        x.IsActive)
                .ThenBy(
                    x =>
                        x.Category)
                .ThenBy(
                    x =>
                        x.Title)
                .ToListAsync(
                    cancellationToken);

        return Ok(
            new
            {
                items =
                    items
                        .Select(
                            ToView)
                        .ToArray(),

                canManage =
                    CanManage
            });
    }

    // ============================================================
    // CREATE
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        TemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var identity =
            await GetIdentityAsync(
                cancellationToken);

        if (identity is null)
        {
            return Unauthorized();
        }

        var duplicate =
            await _db
                .Set<HelpdeskRequestTemplate>()
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.Title.ToLower() ==
                            (
                                request.Title
                                ??
                                string.Empty
                            )
                            .Trim()
                            .ToLower(),
                    cancellationToken);

        if (duplicate)
        {
            return Conflict(
                new
                {
                    message =
                        "Ya existe una plantilla con ese nombre."
                });
        }

        var template =
            new HelpdeskRequestTemplate(
                identity.Value.OrganizationId,
                identity.Value.UserId);

        try
        {
            await ConfigureAsync(
                template,
                request,
                identity.Value.UserId,
                cancellationToken);

            _db.Set<HelpdeskRequestTemplate>()
                .Add(
                    template);

            await _db.SaveChangesAsync(
                cancellationToken);

            return Ok(
                ToView(
                    template));
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
    }

    // ============================================================
    // UPDATE
    // ============================================================

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody]
        TemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var identity =
            await GetIdentityAsync(
                cancellationToken);

        if (identity is null)
        {
            return Unauthorized();
        }

        var template =
            await _db
                .Set<HelpdeskRequestTemplate>()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.Id ==
                            id,
                    cancellationToken);

        if (template is null)
        {
            return NotFound(
                new
                {
                    message =
                        "La plantilla no existe."
                });
        }

        if (
            request.Revision.HasValue
            &&
            request.Revision.Value !=
                template.Revision)
        {
            return Conflict(
                new
                {
                    message =
                        "La plantilla fue modificada por otro usuario. Actualiza el catálogo antes de guardar."
                });
        }

        var requestedTitle =
            (
                request.Title
                ??
                string.Empty
            )
            .Trim();

        var duplicate =
            await _db
                .Set<HelpdeskRequestTemplate>()
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.Id !=
                            id
                        &&
                        x.Title.ToLower() ==
                            requestedTitle
                                .ToLower(),
                    cancellationToken);

        if (duplicate)
        {
            return Conflict(
                new
                {
                    message =
                        "Ya existe otra plantilla con ese nombre."
                });
        }

        try
        {
            await ConfigureAsync(
                template,
                request,
                identity.Value.UserId,
                cancellationToken);

            await _db.SaveChangesAsync(
                cancellationToken);

            return Ok(
                ToView(
                    template));
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
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(
                new
                {
                    message =
                        "La plantilla fue modificada mientras estabas editándola."
                });
        }
    }

    // ============================================================
    // CONFIGURE
    // ============================================================

    private async Task ConfigureAsync(
        HelpdeskRequestTemplate template,
        TemplateRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var title =
            (
                request.Title
                ??
                string.Empty
            )
            .Trim();

        var description =
            (
                request.Description
                ??
                string.Empty
            )
            .Trim();

        var category =
            (
                request.Category
                ??
                "general"
            )
            .Trim()
            .ToLowerInvariant();

        var ticketType =
            (
                request.TicketType
                ??
                "incident"
            )
            .Trim()
            .ToLowerInvariant();

        // ========================================================
        // DOMAIN-CONTRACT LIMITS
        // ========================================================

        if (
            title.Length <
                3
            ||
            title.Length >
                100)
        {
            throw new ArgumentException(
                "El nombre de la plantilla debe tener entre 3 y 100 caracteres.");
        }

        if (description.Length >
            300)
        {
            throw new ArgumentException(
                "La descripción no puede exceder 300 caracteres.");
        }

        if (
            category.Length <
                1
            ||
            category.Length >
                80)
        {
            throw new ArgumentException(
                "La categoría debe tener entre 1 y 80 caracteres.");
        }

        if (ticketType is not (
                "incident"
                or
                "request"))
        {
            throw new ArgumentException(
                "El tipo debe ser incident o request.");
        }

        // ========================================================
        // CATEGORY MUST EXIST IN ACTIVE HELPDESK GROUPS
        // ========================================================

        var configuredCategories =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            template.OrganizationId
                        &&
                        x.IsActive)
                .Select(
                    x =>
                        x.Categories)
                .ToListAsync(
                    cancellationToken);

        var availableCategories =
            configuredCategories
                .SelectMany(
                    value =>
                        (
                            value
                            ??
                            string.Empty
                        )
                        .Split(
                            '|',
                            StringSplitOptions
                                .RemoveEmptyEntries
                            |
                            StringSplitOptions
                                .TrimEntries))
                .Append(
                    "general")
                .Select(
                    x =>
                        x.Trim()
                            .ToLowerInvariant())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (!availableCategories.Contains(
                category,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "La categoría seleccionada no existe en los grupos activos de Mesa de Ayuda.");
        }

        // ========================================================
        // QUESTIONS
        // ========================================================

        var questions =
            (
                request.Questions
                ??
                []
            )
            .Select(
                x =>
                    (
                        x
                        ??
                        string.Empty
                    )
                    .Trim())
            .Where(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x))
            .ToArray();

        if (
            questions.Length <
                1
            ||
            questions.Length >
                8)
        {
            throw new ArgumentException(
                "Cada plantilla debe contener entre 1 y 8 preguntas.");
        }

        if (questions.Any(
                x =>
                    x.Length <
                        3
                    ||
                    x.Length >
                        120))
        {
            throw new ArgumentException(
                "Cada pregunta debe tener entre 3 y 120 caracteres.");
        }

        if (
            questions
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count()
            !=
            questions.Length)
        {
            throw new ArgumentException(
                "La plantilla contiene preguntas duplicadas.");
        }

        template.Configure(
            title,
            description,
            category,
            ticketType,
            questions,
            request.IsActive,
            actorUserId);
    }

    // ============================================================
    // VIEW
    // ============================================================

    private static object ToView(
        HelpdeskRequestTemplate template)
    {
        return new
        {
            template.Id,
            template.Title,
            template.Description,
            template.Category,
            template.TicketType,
            template.IsActive,
            template.Revision,

            questions =
                JsonSerializer
                    .Deserialize<string[]>(
                        template.QuestionsJson)
                ??
                [],

            template.UpdatedAtUtc
        };
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private async Task<
        (
            Guid OrganizationId,
            Guid UserId
        )?>
        GetIdentityAsync(
            CancellationToken cancellationToken)
    {
        var organizationClaim =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userClaim =
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

        if (
            !Guid.TryParse(
                organizationClaim,
                out var organizationId)
            ||
            !Guid.TryParse(
                userClaim,
                out var userId))
        {
            return null;
        }

        var active =
            await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            userId
                        &&
                        x.IsActive,
                    cancellationToken);

        return active
            ? (
                organizationId,
                userId
            )
            : null;
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

    public sealed record TemplateRequest(
        string? Title,
        string? Description,
        string? Category,
        string? TicketType,
        string[]? Questions,
        bool IsActive,
        int? Revision);
}