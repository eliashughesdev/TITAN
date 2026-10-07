using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/site-coverage")]
public sealed class HelpdeskSiteCoverageController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    public HelpdeskSiteCoverageController(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    // ============================================================
    // CATALOG
    // ============================================================

    [HttpGet("catalog")]
    public async Task<IActionResult>
        GetCatalog(
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var sites =
            await _db
                .Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.Name)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Code,
                            x.Name,
                            x.City,
                            x.Province,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        var siteLocations =
            await _db
                .SiteLocations
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.Name)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.SiteId,
                            x.Name,
                            x.Description,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        var teams =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.Name)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Name,
                            x.Description,
                            x.Categories,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        var coverages =
            await _db
                .HelpdeskSiteCoverages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.Priority)
                .ThenBy(
                    x =>
                        x.SiteId)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.TeamId,
                            x.SiteId,
                            x.SiteLocationId,
                            x.Category,
                            x.Priority,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            new
            {
                sites,
                siteLocations,
                teams,
                coverages
            });
    }

    // ============================================================
    // CREATE COVERAGE
    // ============================================================

    [HttpPost]
    public async Task<IActionResult>
        CreateCoverage(
            [FromBody]
            CreateHelpdeskSiteCoverageRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (
            request.TeamId ==
                Guid.Empty
            ||
            request.SiteId ==
                Guid.Empty)
        {
            return BadRequest(
                new
                {
                    message =
                        "Grupo y localidad son obligatorios."
                });
        }

        var teamExists =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            request.TeamId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!teamExists)
        {
            return BadRequest(
                new
                {
                    message =
                        "El grupo no existe o está desactivado."
                });
        }

        var siteExists =
            await _db
                .Sites
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            request.SiteId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!siteExists)
        {
            return BadRequest(
                new
                {
                    message =
                        "La localidad no existe o está desactivada."
                });
        }

        if (
            request.SiteLocationId
                .HasValue)
        {
            var locationExists =
                await _db
                    .SiteLocations
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.SiteId ==
                                request.SiteId
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

        var category =
            string.IsNullOrWhiteSpace(
                request.Category)
                ? null
                : request.Category
                    .Trim()
                    .ToLowerInvariant();

        var duplicate =
            await _db
                .HelpdeskSiteCoverages
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TeamId ==
                            request.TeamId
                        &&
                        x.SiteId ==
                            request.SiteId
                        &&
                        x.SiteLocationId ==
                            request.SiteLocationId
                        &&
                        x.Category ==
                            category,
                    cancellationToken);

        if (duplicate)
        {
            return Conflict(
                new
                {
                    message =
                        "Esa cobertura ya existe."
                });
        }

        var coverage =
            new HelpdeskSiteCoverage(
                organizationId,
                request.TeamId,
                request.SiteId,
                request.SiteLocationId,
                category,
                request.Priority);

        _db.HelpdeskSiteCoverages
            .Add(
                coverage);

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            new
            {
                coverage.Id,
                coverage.TeamId,
                coverage.SiteId,
                coverage.SiteLocationId,
                coverage.Category,
                coverage.Priority,
                coverage.IsActive
            });
    }

    // ============================================================
    // UPDATE
    // ============================================================

    [HttpPut("{coverageId:guid}")]
    public async Task<IActionResult>
        UpdateCoverage(
            Guid coverageId,
            [FromBody]
            UpdateHelpdeskSiteCoverageRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var coverage =
            await _db
                .HelpdeskSiteCoverages
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            coverageId,
                    cancellationToken);

        if (coverage is null)
        {
            return NotFound();
        }

        if (
            request.SiteLocationId
                .HasValue)
        {
            var validLocation =
                await _db
                    .SiteLocations
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.SiteId ==
                                coverage.SiteId
                            &&
                            x.Id ==
                                request.SiteLocationId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!validLocation)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "La ubicación no pertenece a esta localidad."
                    });
            }
        }

        coverage.Update(
            request.SiteLocationId,
            request.Category,
            request.Priority);

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return NoContent();
    }

    // ============================================================
    // ACTIVATE / DEACTIVATE
    // ============================================================

    [HttpPost("{coverageId:guid}/activate")]
    public Task<IActionResult>
        Activate(
            Guid coverageId,
            CancellationToken cancellationToken)
    {
        return SetStatus(
            coverageId,
            true,
            cancellationToken);
    }

    [HttpPost("{coverageId:guid}/deactivate")]
    public Task<IActionResult>
        Deactivate(
            Guid coverageId,
            CancellationToken cancellationToken)
    {
        return SetStatus(
            coverageId,
            false,
            cancellationToken);
    }

    // ============================================================
    // DELETE
    // ============================================================

    [HttpDelete("{coverageId:guid}")]
    public async Task<IActionResult>
        Delete(
            Guid coverageId,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var coverage =
            await _db
                .HelpdeskSiteCoverages
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            coverageId,
                    cancellationToken);

        if (coverage is null)
        {
            return NotFound();
        }

        _db.HelpdeskSiteCoverages
            .Remove(
                coverage);

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return NoContent();
    }

    // ============================================================
    // INTERNAL
    // ============================================================

    private async Task<IActionResult>
        SetStatus(
            Guid coverageId,
            bool active,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var coverage =
            await _db
                .HelpdeskSiteCoverages
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            coverageId,
                    cancellationToken);

        if (coverage is null)
        {
            return NotFound();
        }

        if (active)
        {
            coverage.Activate();
        }
        else
        {
            coverage.Deactivate();
        }

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return NoContent();
    }

    private bool
        TryGetOrganization(
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

   private bool CanManage()
{
    return User.Claims.Any(
        claim =>
            claim.Type ==
                "permission"
            &&
            (
                string.Equals(
                    claim.Value,
                    "helpdesk.sites.manage",
                    StringComparison.OrdinalIgnoreCase)

                ||

                string.Equals(
                    claim.Value,
                    "helpdesk.sites.view",
                    StringComparison.OrdinalIgnoreCase)

                ||

                string.Equals(
                    claim.Value,
                    "helpdesk.admin.access",
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
}

public sealed record
    CreateHelpdeskSiteCoverageRequest(
        Guid TeamId,
        Guid SiteId,
        Guid? SiteLocationId,
        string? Category,
        int Priority = 100);

public sealed record
    UpdateHelpdeskSiteCoverageRequest(
        Guid? SiteLocationId,
        string? Category,
        int Priority = 100);