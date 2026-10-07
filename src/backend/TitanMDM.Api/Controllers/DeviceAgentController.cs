using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Devices.Agent;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device")]
public sealed class DeviceAgentController
    : ControllerBase
{
    private readonly IDeviceAgentService
        _deviceAgentService;

    public DeviceAgentController(
        IDeviceAgentService deviceAgentService)
    {
        _deviceAgentService =
            deviceAgentService;
    }

    [AllowAnonymous]
    [HttpPost("heartbeat")]
    public async Task<IActionResult>
        Heartbeat(
            [FromBody]
            DeviceHeartbeatRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _deviceAgentService
                    .HeartbeatAsync(
                        request,
                        cancellationToken);

            return Ok(
                result);
        }
        catch (
            DeviceAuthenticationException ex)
        {
            var statusCode =
                ex.Code switch
                {
                    "DEVICE_NOT_FOUND" =>
                        StatusCodes.Status404NotFound,

                    "INVALID_DEVICE_ID" =>
                        StatusCodes.Status400BadRequest,

                    _ =>
                        StatusCodes.Status401Unauthorized
                };

            return StatusCode(
                statusCode,
                new
                {
                    code =
                        ex.Code,

                    message =
                        ex.Message
                });
        }
    }
}