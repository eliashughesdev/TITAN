using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Security;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/android-enterprise/devices/{deviceId:guid}/actions")]
public sealed class AndroidRemoteActionsController
    : ControllerBase
{
    private readonly IAndroidRemoteActionService _service;

    public AndroidRemoteActionsController(
        IAndroidRemoteActionService service)
    {
        _service =
            service ??
            throw new ArgumentNullException(
                nameof(service));
    }

    [HttpPost]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Actions)]
    public async Task<IActionResult> Execute(
        Guid deviceId,
        [FromBody]
        AndroidRemoteActionRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _service.ExecuteAsync(
                    organizationId.Value,
                    userId.Value,
                    deviceId,
                    request,
                    HttpContext.TraceIdentifier,
                    HttpContext.Connection
                        .RemoteIpAddress
                        ?.ToString(),
                    cancellationToken);

            return Ok(result);
        }
        catch (AndroidRemoteActionException exception)
            when (exception.Code is
                "DEVICE_NOT_FOUND" or
                "ANDROID_DEVICE_NOT_ENROLLED")
        {
            return NotFound(new
            {
                code = exception.Code,
                message = exception.Message
            });
        }
        catch (AndroidRemoteActionException exception)
        {
            return BadRequest(new
            {
                code = exception.Code,
                message = exception.Message
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

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : null;
    }
}
