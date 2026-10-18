using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Commands.Agent;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Application.Location;
using TitanMDM.Infrastructure.Persistence;

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

    private readonly IDeviceLocationService
        _locationService;

    private readonly TitanMdmDbContext _dbContext;

    public DeviceCommandAgentController(
        IDeviceCommandAgentService commandService,
        IDeviceAuthenticator deviceAuthenticator,
        IDeviceLocationService locationService,
        TitanMdmDbContext dbContext)
    {
        _commandService = commandService;
        _deviceAuthenticator =
            deviceAuthenticator;
        _locationService = locationService;
        _dbContext = dbContext;
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
            return CommandStateError(ex);
        }
    }

    private IActionResult CommandStateError(
        InvalidOperationException exception)
    {
        if (exception.Message.StartsWith(
                "El comando no existe",
                StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = exception.Message
            });
        }

        return Conflict(new
        {
            code = "INVALID_COMMAND_STATE",
            message = exception.Message
        });
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
            return CommandStateError(ex);
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
            return CommandStateError(ex);
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
            return CommandStateError(ex);
        }
    }

    [HttpPost("location")]
    public async Task<IActionResult> ReportLocation(
        [FromBody] LocationReportRequest request,
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
            await _locationService.ProcessLocationAsync(
                authentication.DeviceId,
                request.ResultJson,
                cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                code = "LOCATION_REPORT_ERROR",
                message = ex.Message
            });
        }
    }

    [HttpGet("location/policy")]
    public async Task<IActionResult> GetLocationPolicy(
        CancellationToken cancellationToken)
    {
        var authentication =
            await AuthenticateDeviceAsync(
                cancellationToken);

        if (!authentication.Success)
        {
            return authentication.Error!;
        }

        var organizationId =
            await _dbContext.Devices
                .AsNoTracking()
                .Where(x =>
                    x.Id ==
                        authentication.DeviceId)
                .Select(x => x.OrganizationId)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (organizationId == Guid.Empty)
        {
            return NotFound(new
            {
                code = "DEVICE_NOT_FOUND",
                message =
                    "Dispositivo no encontrado."
            });
        }

        var policy =
            await _dbContext
                .LocationReportingPolicies
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId)
                .Select(x => new
                {
                    isEnabled = x.IsEnabled,
                    reportingIntervalMinutes =
                        x.ReportingIntervalMinutes,
                    retentionDays =
                        x.RetentionDays
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        /*
         * Sin politica configurada el agente NO
         * reporta: el comportamiento por defecto
         * es no capturar.
         */
        return Ok(policy ?? new
        {
            isEnabled = false,
            reportingIntervalMinutes = 15,
            retentionDays = 30
        });
    }

    public sealed record LocationReportRequest(
        string ResultJson);

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
