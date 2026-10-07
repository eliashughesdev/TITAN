
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Helpdesk;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/helpdesk/entra")]
[Authorize]
public sealed class EntraIdSettingsController : ControllerBase
{
    private const string SettingsManage = "settings.manage";
    private const string HelpdeskManage = "helpdesk.manage";
    private const string TicketsView = "tickets.view";

    private readonly IEntraIdDirectoryService _entraIdDirectoryService;

    public EntraIdSettingsController(IEntraIdDirectoryService entraIdDirectoryService)
    {
        _entraIdDirectoryService = entraIdDirectoryService;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        if (!CanManageDirectory())
            return Forbid();

        var organizationId = GetOrganizationId();
        if (organizationId is null)
            return Unauthorized(new { message = "El token no contiene una organización válida." });

        var settings = await _entraIdDirectoryService.GetSettingsAsync(
            organizationId.Value,
            cancellationToken);

        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(
        [FromBody] SaveEntraIdSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageDirectory())
            return Forbid();

        var organizationId = GetOrganizationId();
        if (organizationId is null)
            return Unauthorized(new { message = "El token no contiene una organización válida." });

        var settings = await _entraIdDirectoryService.SaveSettingsAsync(
            organizationId.Value,
            request,
            cancellationToken);

        return Ok(settings);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        if (!CanManageDirectory())
            return Forbid();

        var organizationId = GetOrganizationId();
        if (organizationId is null)
            return Unauthorized(new { message = "El token no contiene una organización válida." });

        try
        {
            var result = await _entraIdDirectoryService.SyncDirectoryAsync(
                organizationId.Value,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(TicketsView, HelpdeskManage, SettingsManage))
            return Forbid();

        var organizationId = GetOrganizationId();
        if (organizationId is null)
            return Unauthorized(new { message = "El token no contiene una organización válida." });

        var users = await _entraIdDirectoryService.SearchDirectoryAsync(
            organizationId.Value,
            search,
            cancellationToken);

        return Ok(users);
    }

    private bool CanManageDirectory()
    {
        return HasAnyPermission(SettingsManage, HelpdeskManage);
    }

    private Guid? GetOrganizationId()
    {
        var value = User.FindFirstValue("organization_id") ?? User.FindFirstValue("organizationId");
        return Guid.TryParse(value, out var organizationId) ? organizationId : null;
    }

    private bool HasPermission(string permission)
    {
        return User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(HasPermission);
    }
}
