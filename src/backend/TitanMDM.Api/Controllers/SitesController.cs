using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Security;
using TitanMDM.Application.Sites;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sites")]
public sealed class SitesController
    : ControllerBase
{
    private readonly ISiteService
        _siteService;

    public SitesController(
        ISiteService siteService)
    {
        _siteService =
            siteService;
    }

    [HttpGet]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult>
        GetSites(
            [FromQuery]
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(
            await _siteService
                .GetSitesAsync(
                    organizationId.Value,
                    includeInactive,
                    cancellationToken));
    }

    [HttpGet("{siteId:guid}")]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult>
        GetSite(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var site =
            await _siteService
                .GetSiteAsync(
                    organizationId.Value,
                    siteId,
                    cancellationToken);

        return site is null
            ? NotFound()
            : Ok(site);
    }

    [HttpPost]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        CreateSite(
            [FromBody]
            CreateSiteRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var site =
                await _siteService
                    .CreateSiteAsync(
                        organizationId.Value,
                        request,
                        cancellationToken);

            return CreatedAtAction(
                nameof(GetSite),
                new
                {
                    siteId =
                        site.Id
                },
                site);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpPut("{siteId:guid}")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        UpdateSite(
            Guid siteId,
            [FromBody]
            UpdateSiteRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteService
                    .UpdateSiteAsync(
                        organizationId.Value,
                        siteId,
                        request,
                        cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpPost("{siteId:guid}/activate")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        Activate(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        return await SetStatus(
            siteId,
            true,
            cancellationToken);
    }

    [HttpPost("{siteId:guid}/deactivate")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        Deactivate(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        return await SetStatus(
            siteId,
            false,
            cancellationToken);
    }

    [HttpGet("{siteId:guid}/locations")]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult>
        GetLocations(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteService
                    .GetLocationsAsync(
                        organizationId.Value,
                        siteId,
                        cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpPost("{siteId:guid}/locations")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        CreateLocation(
            Guid siteId,
            [FromBody]
            CreateSiteLocationRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var location =
                await _siteService
                    .CreateLocationAsync(
                        organizationId.Value,
                        siteId,
                        request,
                        cancellationToken);

            return Ok(
                location);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpPut(
        "{siteId:guid}/locations/{locationId:guid}")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        UpdateLocation(
            Guid siteId,
            Guid locationId,
            [FromBody]
            UpdateSiteLocationRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteService
                    .UpdateLocationAsync(
                        organizationId.Value,
                        siteId,
                        locationId,
                        request,
                        cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpPost(
        "{siteId:guid}/locations/{locationId:guid}/activate")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult>
        ActivateLocation(
            Guid siteId,
            Guid locationId,
            CancellationToken cancellationToken)
    {
        return SetLocationStatus(
            siteId,
            locationId,
            true,
            cancellationToken);
    }

    [HttpPost(
        "{siteId:guid}/locations/{locationId:guid}/deactivate")]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult>
        DeactivateLocation(
            Guid siteId,
            Guid locationId,
            CancellationToken cancellationToken)
    {
        return SetLocationStatus(
            siteId,
            locationId,
            false,
            cancellationToken);
    }

    private async Task<IActionResult> SetStatus(
        Guid siteId,
        bool active,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _siteService
                .SetSiteStatusAsync(
                    organizationId.Value,
                    siteId,
                    active,
                    cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    private async Task<IActionResult>
        SetLocationStatus(
            Guid siteId,
            Guid locationId,
            bool active,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _siteService
                .SetLocationStatusAsync(
                    organizationId.Value,
                    siteId,
                    locationId,
                    active,
                    cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : null;
    }
}