using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Commands;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device-commands")]
[Authorize]
public sealed class DeviceCommandsController : ControllerBase
{
    private readonly IDeviceCommandService _deviceCommandService;

    public DeviceCommandsController(
        IDeviceCommandService deviceCommandService)
    {
        _deviceCommandService = deviceCommandService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_ORGANIZATION",
                message =
                    "El token no contiene una organización válida."
            });
        }

        if (userId is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_USER",
                message =
                    "El token no contiene un usuario válido."
            });
        }

        try
        {
            var command =
                await _deviceCommandService.CreateAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    commandId = command.Id
                },
                command);
        }
        catch (DeviceCommandException ex)
        {
            return MapException(ex);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCommands(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_ORGANIZATION",
                message =
                    "El token no contiene una organización válida."
            });
        }

        try
        {
            var result =
                await _deviceCommandService.GetCommandsAsync(
                    organizationId.Value,
                    deviceId,
                    status,
                    page,
                    pageSize,
                    cancellationToken);

            return Ok(result);
        }
        catch (DeviceCommandException ex)
        {
            return MapException(ex);
        }
    }

    [HttpGet("{commandId:guid}")]
    public async Task<IActionResult> GetById(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_ORGANIZATION",
                message =
                    "El token no contiene una organización válida."
            });
        }

        var command =
            await _deviceCommandService.GetByIdAsync(
                organizationId.Value,
                commandId,
                cancellationToken);

        if (command is null)
        {
            return NotFound(new
            {
                code = "COMMAND_NOT_FOUND",
                message = "El comando no existe."
            });
        }

        return Ok(command);
    }

    [HttpPost("{commandId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_ORGANIZATION",
                message =
                    "El token no contiene una organización válida."
            });
        }

        try
        {
            await _deviceCommandService.CancelAsync(
                organizationId.Value,
                commandId,
                cancellationToken);

            return NoContent();
        }
        catch (DeviceCommandException ex)
        {
            return MapException(ex);
        }
    }

    private IActionResult MapException(
        DeviceCommandException exception)
    {
        var statusCode =
            exception.Code switch
            {
                "INVALID_ORGANIZATION" =>
                    StatusCodes.Status400BadRequest,

                "INVALID_USER" =>
                    StatusCodes.Status400BadRequest,

                "INVALID_DEVICE" =>
                    StatusCodes.Status400BadRequest,

                "INVALID_COMMAND_TYPE" =>
                    StatusCodes.Status400BadRequest,

                "INVALID_EXPIRATION" =>
                    StatusCodes.Status400BadRequest,

                "INVALID_STATUS" =>
                    StatusCodes.Status400BadRequest,

                "DEVICE_NOT_FOUND" =>
                    StatusCodes.Status404NotFound,

                "COMMAND_NOT_FOUND" =>
                    StatusCodes.Status404NotFound,

                "COMMAND_TERMINAL" =>
                    StatusCodes.Status409Conflict,

                _ =>
                    StatusCodes.Status400BadRequest
            };

        return StatusCode(
            statusCode,
            new
            {
                code = exception.Code,
                message = exception.Message
            });
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

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var userId)
            ? userId
            : null;
    }
}