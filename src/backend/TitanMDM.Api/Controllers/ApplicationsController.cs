using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
[RequirePermission(
    PermissionCodes.Applications.View)]
public sealed class ApplicationsController
    : ControllerBase
{
    private readonly IApplicationInventoryService
        _applicationInventoryService;

    public ApplicationsController(
        IApplicationInventoryService applicationInventoryService)
    {
        _applicationInventoryService =
            applicationInventoryService;
    }

    [HttpGet]
    public async Task<IActionResult>
        GetApplications(
            [FromQuery] string? search,
            [FromQuery] bool? systemApp,
            CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización válida."
                });
        }

        var result =
            await _applicationInventoryService
                .GetApplicationsAsync(
                    organizationId.Value,
                    search,
                    systemApp,
                    cancellationToken);

        return Ok(result);
    }

    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult>
        GetDeviceApplications(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización válida."
                });
        }

        var result =
            await _applicationInventoryService
                .GetDeviceApplicationsAsync(
                    organizationId.Value,
                    deviceId,
                    cancellationToken);

        return Ok(result);
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var organizationId)
            ? organizationId
            : null;
    }
}