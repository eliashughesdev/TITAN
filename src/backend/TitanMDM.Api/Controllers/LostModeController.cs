using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.LostMode;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lost-mode")]
public sealed class LostModeController
    : ControllerBase
{
    private readonly ILostModeService _service;

    public LostModeController(
        ILostModeService service)
    {
        _service = service;
    }

    [HttpGet("{deviceId:guid}")]
    public async Task<IActionResult> Get(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _service.GetActiveAsync(
                organizationId.Value,
                deviceId,
                cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        ActivateLostModeRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        return Ok(
            await _service.ActivateAsync(
                organizationId.Value,
                userId.Value,
                request,
                cancellationToken));
    }

    [HttpPost("{deviceId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        await _service.DeactivateAsync(
            organizationId.Value,
            userId.Value,
            deviceId,
            cancellationToken);

        return NoContent();
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var result)
            ? result
            : null;
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var result)
            ? result
            : null;
    }
}