using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/routing")]
public sealed class HelpdeskRoutingDiagnosticsController
    : ControllerBase
{
    private readonly IHelpdeskService
        _helpdesk;

    private readonly TitanMdmDbContext
        _db;

    public HelpdeskRoutingDiagnosticsController(
        IHelpdeskService helpdesk,
        TitanMdmDbContext db)
    {
        _helpdesk =
            helpdesk;

        _db =
            db;
    }

    // ============================================================
    // PREVIEW / SIMULATION
    // ============================================================

    [HttpPost("preview")]
    public async Task<IActionResult>
        Preview(
            [FromBody]
            HelpdeskRoutingPreviewRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanViewRouting())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (!TryGetUserId(
                out var currentUserId))
        {
            return Unauthorized();
        }

        var requesterUserId =
            request.RequesterUserId
            ??
            currentUserId;

        var requesterExists =
            await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            requesterUserId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!requesterExists)
        {
            return BadRequest(
                new
                {
                    message =
                        "El solicitante seleccionado no existe o está inactivo."
                });
        }

        if (request.SiteId.HasValue)
        {
            var siteExists =
                await _db.Sites
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                request.SiteId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!siteExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "La localidad seleccionada no existe o está inactiva."
                    });
            }
        }

        if (request.SiteLocationId.HasValue)
        {
            if (!request.SiteId.HasValue)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Selecciona primero una localidad."
                    });
            }

            var locationExists =
                await _db.SiteLocations
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.SiteId ==
                                request.SiteId.Value
                            &&
                            x.Id ==
                                request.SiteLocationId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!locationExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "La sublocalidad no pertenece a la localidad seleccionada."
                    });
            }
        }

        if (request.RequestedTeamId.HasValue)
        {
            var teamExists =
                await _db.HelpdeskTeams
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                request.RequestedTeamId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!teamExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El grupo solicitado no existe o está inactivo."
                    });
            }
        }

        try
        {
            var result =
                await _helpdesk
                    .PreviewRoutingDiagnosticAsync(
                        organizationId,
                        requesterUserId,
                        request,
                        cancellationToken);

            return Ok(
                result);
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
    // CATALOG FOR SIMULATOR
    // ============================================================

    [HttpGet("catalog")]
    public async Task<IActionResult>
        Catalog(
            CancellationToken cancellationToken)
    {
        if (!CanViewRouting())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var sites =
            await _db.Sites
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
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Code,
                            x.Name
                        })
                .ToListAsync(
                    cancellationToken);

        var locations =
            await _db.SiteLocations
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
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.SiteId,
                            x.Name
                        })
                .ToListAsync(
                    cancellationToken);

        var teams =
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
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Name,
                            x.Categories
                        })
                .ToListAsync(
                    cancellationToken);

        var categories =
            teams
                .SelectMany(
                    team =>
                        ParseCategories(
                            team.Categories))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    x =>
                        x)
                .ToArray();

        return Ok(
            new
            {
                sites,
                locations,

                teams =
                    teams.Select(
                        x =>
                            new
                            {
                                x.Id,
                                x.Name
                            }),

                categories
            });
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static IEnumerable<string>
        ParseCategories(
            string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return [];
        }

        return value
            .Split(
                '|',
                StringSplitOptions
                    .RemoveEmptyEntries
                |
                StringSplitOptions
                    .TrimEntries)
            .Where(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x));
    }

    private bool CanViewRouting()
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                (
                    string.Equals(
                        claim.Value,
                        "helpdesk.admin.access",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.routing.view",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.routing.manage",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.technicians.view",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.technicians.manage",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.manage",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "settings.manage",
                        StringComparison.OrdinalIgnoreCase)
                ));
    }

    private bool TryGetOrganization(
        out Guid organizationId)
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out organizationId);
    }

    private bool TryGetUserId(
        out Guid userId)
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
            out userId);
    }
}