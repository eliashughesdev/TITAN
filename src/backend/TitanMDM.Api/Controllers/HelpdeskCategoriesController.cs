using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/categories")]
public sealed class HelpdeskCategoriesController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskCategoriesController(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        if (!CanCreateOrViewTickets())
            return Forbid();

        var claim =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        if (!Guid.TryParse(
                claim,
                out var organizationId))
        {
            return Unauthorized();
        }

        var configured = await _db.HelpdeskTeams
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.IsActive)
            .Select(x => x.Categories)
            .ToListAsync(
                cancellationToken);

        var categories = configured
            .SelectMany(value =>
                (value ?? string.Empty)
                    .Split(
                        '|',
                        StringSplitOptions
                            .RemoveEmptyEntries |
                        StringSplitOptions
                            .TrimEntries))
            .Select(value =>
                value.Trim().ToLowerInvariant())
            .Where(value =>
                value.Length is > 0 and <= 80)
            .Append("general")
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(value =>
                value == "general" ? 0 : 1)
            .ThenBy(value => value)
            .ToArray();

        return Ok(new
        {
            items = categories,
            configured =
                categories.Length > 1
        });
    }

    private bool CanCreateOrViewTickets() =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            (string.Equals(
                 claim.Value,
                 "tickets.create",
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 claim.Value,
                 "tickets.view",
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 claim.Value,
                 "helpdesk.view",
                 StringComparison.OrdinalIgnoreCase)));
}