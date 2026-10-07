using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Commands.Agent;
using TitanMDM.Application.Devices.Agent;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device/commands")]
[AllowAnonymous]
public sealed class DeviceCommandAgentController
    : ControllerBase
{
    private readonly IDeviceCommandAgentService
        _commandService;

    private readonly IDeviceAuthenticator
        _deviceAuthenticator;

    public DeviceCommandAgentController(
        IDeviceCommandAgentService commandService,
        IDeviceAuthenticator deviceAuthenticator)
    {
        _commandService = commandService;
        _deviceAuthenticator =
            deviceAuthenticator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPending(
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        var commands =
            await _commandService
                .GetPendingCommandsAsync(
                    authentication.DeviceId,
                    cancellationToken);

        return Ok(commands);
    }

    [HttpPost("{commandId:guid}/delivered")]
    public async Task<IActionResult> Delivered(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        try
        {
            await _commandService.MarkDeliveredAsync(
                authentication.DeviceId,
                commandId,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = ex.Message
            });
        }
    }

    [HttpPost("{commandId:guid}/executing")]
    public async Task<IActionResult> Executing(
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        try
        {
            await _commandService.MarkExecutingAsync(
                authentication.DeviceId,
                commandId,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = ex.Message
            });
        }
    }

    [HttpPost("{commandId:guid}/success")]
    public async Task<IActionResult> Success(
        Guid commandId,
        [FromBody] CommandSuccessRequest? request,
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        try
        {
            await _commandService.MarkSuccessAsync(
                authentication.DeviceId,
                commandId,
                request?.ResultJson,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = ex.Message
            });
        }
    }

    [HttpPost("{commandId:guid}/failed")]
    public async Task<IActionResult> Failed(
        Guid commandId,
        [FromBody] CommandFailedRequest request,
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        try
        {
            await _commandService.MarkFailedAsync(
                authentication.DeviceId,
                commandId,
                request.ErrorCode,
                request.ErrorMessage,
                request.ResultJson,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = ex.Message
            });
        }
    }

    private async Task<DeviceAuthenticationResult>
        AuthenticateDeviceAsync(
            CancellationToken cancellationToken)
    {
        var deviceIdHeader =
            Request.Headers[
                "X-Titan-Device-Id"]
                .FirstOrDefault();

        var deviceSecret =
            Request.Headers[
                "X-Titan-Device-Secret"]
                .FirstOrDefault();

        if (!Guid.TryParse(
                deviceIdHeader,
                out var deviceId))
        {
            return DeviceAuthenticationResult.Fail(
                Unauthorized(new
                {
                    code = "INVALID_DEVICE_ID",
                    message =
                        "X-Titan-Device-Id no es válido."
                }));
        }

        try
        {
            await _deviceAuthenticator
                .AuthenticateAsync(
                    deviceId,
                    deviceSecret ?? string.Empty,
                    cancellationToken);

            return DeviceAuthenticationResult.Ok(
                deviceId);
        }
        catch (DeviceAuthenticationException ex)
        {
            return DeviceAuthenticationResult.Fail(
                Unauthorized(new
                {
                    code = ex.Code,
                    message = ex.Message
                }));
        }
    }

    public sealed record CommandSuccessRequest(
        string? ResultJson);

    public sealed record CommandFailedRequest(
        string ErrorCode,
        string ErrorMessage,
        string? ResultJson);

    private sealed record DeviceAuthenticationResult(
        bool Success,
        Guid DeviceId,
        IActionResult? Error)
    {
        public static DeviceAuthenticationResult Ok(
            Guid deviceId)
        {
            return new DeviceAuthenticationResult(
                true,
                deviceId,
                null);
        }

        public static DeviceAuthenticationResult Fail(
            IActionResult error)
        {
            return new DeviceAuthenticationResult(
                false,
                Guid.Empty,
                error);
        }
    }
}