using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/categories")]
public sealed class HelpdeskCategoriesController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    public HelpdeskCategoriesController(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    // ============================================================
    // GET CATEGORY CATALOG
    // ============================================================

    [HttpGet]
    public async Task<IActionResult>
        Get(
            CancellationToken cancellationToken)
    {
        if (
            !CanCreateOrViewTickets())
        {
            return Forbid();
        }

        if (
            !TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var teams =
            await _db
                .HelpdeskTeams
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

        var grouped =
            teams
                .Select(
                    team =>
                        new
                        {
                            team.Id,

                            team.Name,

                            items =
                                ParseCategories(
                                    team.Categories)
                        })
                .Where(
                    item =>
                        item.items.Length >
                        0)
                .ToArray();

        var categories =
            grouped
                .SelectMany(
                    group =>
                        group.items)
                .Append(
                    "general")
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    item =>
                        string.Equals(
                            item,
                            "general",
                            StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : 1)
                .ThenBy(
                    item =>
                        item)
                .ToArray();

        return Ok(
            new
            {
                items =
                    categories,

                configured =
                    categories.Length >
                    1,

                groups =
                    grouped,

                total =
                    categories.Length
            });
    }

    // ============================================================
    // CATEGORY PARSER
    // ============================================================

    private static string[]
        ParseCategories(
            string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
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
            .Select(
                item =>
                    item
                        .Trim()
                        .ToLowerInvariant())
            .Where(
                item =>
                    item.Length
                    is > 0
                    and <= 100)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                item =>
                    item)
            .ToArray();
    }

    // ============================================================
    // ORGANIZATION
    // ============================================================

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

    // ============================================================
    // AUTHORIZATION
    // ============================================================

    private bool
        CanCreateOrViewTickets()
    {
        return HasAnyPermission(
            // Modern RBAC.
            "helpdesk.portal.access",
            "helpdesk.request.create",
            "helpdesk.request.own.view",
            "helpdesk.agent.access",
            "helpdesk.ticket.details.view",
            "helpdesk.admin.access",

            // Compatibility.
            "tickets.create",
            "tickets.view",
            "helpdesk.view",
            "helpdesk.manage");
    }

    private bool
        HasAnyPermission(
            params string[] permissions)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                permissions.Any(
                    permission =>
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }
}
