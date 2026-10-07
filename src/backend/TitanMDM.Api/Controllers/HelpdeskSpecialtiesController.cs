using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/specialties")]
public sealed class HelpdeskSpecialtiesController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskSpecialtiesController(TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var teams = await _db.HelpdeskTeams
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsActive,
                x.Categories
            })
            .ToListAsync(cancellationToken);

        return Ok(teams.Select(x => new
        {
            x.Id,
            x.Name,
            x.IsActive,
            categories = Parse(x.Categories)
        }));
    }

    [HttpPut("teams/{teamId:guid}")]
    public async Task<IActionResult> Set(
        Guid teamId,
        [FromBody] SetCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        if (request.Categories is null)
            return BadRequest(new
            {
                message = "Envía una lista de categorías."
            });

        var team = await _db.HelpdeskTeams
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId &&
                     x.Id == teamId,
                cancellationToken);

        if (team is null)
            return NotFound();

        try
        {
            team.ConfigureCategories(request.Categories);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            team.Id,
            team.Name,
            categories = Parse(team.Categories)
        });
    }

    private static string[] Parse(string? value) =>
        (value ?? "")
            .Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

    private bool CanManage() =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            (string.Equals(
                 claim.Value,
                 "helpdesk.manage",
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 claim.Value,
                 "settings.manage",
                 StringComparison.OrdinalIgnoreCase)));

    private bool TryGetOrganization(out Guid organizationId)
    {
        var value =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        return Guid.TryParse(value, out organizationId);
    }

    public sealed record SetCategoriesRequest(
        string[] Categories);
}