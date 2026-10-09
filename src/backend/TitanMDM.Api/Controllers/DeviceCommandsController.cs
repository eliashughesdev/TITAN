using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device-commands")]
[Authorize]
public sealed class DeviceCommandsController
    : ControllerBase
{
    private readonly IDeviceCommandService
        _deviceCommandService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public DeviceCommandsController(
        IDeviceCommandService deviceCommandService,
        IScopeAccessService scopeAccessService)
    {
        _deviceCommandService =
            deviceCommandService;

        _scopeAccessService =
            scopeAccessService;
    }

    [HttpPost]
    [RequirePermission(
        PermissionCodes.Devices.Commands)]
    public async Task<IActionResult>
        Create(
            [FromBody]
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized(
                new
                {
                    code =
                        "INVALID_IDENTITY",

                    message =
                        "El token no contiene una identidad válida."
                });
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    request.DeviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var command =
                await _deviceCommandService
                    .CreateAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        request,
                        cancellationToken);

            return CreatedAtAction(
                nameof(
                    GetById),
                new
                {
                    commandId =
                        command.Id
                },
                command);
        }
        catch (
            DeviceCommandException exception)
        {
            return MapException(
                exception);
        }
    }

    [HttpGet]
    [RequirePermission(
        PermissionCodes.Devices.View)]
    public async Task<IActionResult>
        GetCommands(
            [FromQuery] Guid? deviceId,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        /*
         * Consultar historial de un dispositivo concreto
         * requiere acceso al dispositivo.
         */

        if (
            deviceId.HasValue
            &&
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    deviceId.Value,
                    cancellationToken))
        {
            return Forbid();
        }

        /*
         * La consulta global de comandos solamente permanece
         * habilitada para Organization scope hasta convertir
         * IDeviceCommandService en query scoped en WIN-R9.
         *
         * Esto evita una fuga silenciosa de historial.
         */

        if (
            !deviceId.HasValue
            &&
            !await _scopeAccessService
                .HasOrganizationScopeAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken))
        {
            return BadRequest(
                new
                {
                    code =
                        "DEVICE_FILTER_REQUIRED",

                    message =
                        "Para cuentas limitadas por localidad debe especificarse DeviceId al consultar comandos."
                });
        }

        try
        {
            var result =
                await _deviceCommandService
                    .GetCommandsAsync(
                        context.Value.OrganizationId,
                        deviceId,
                        status,
                        page,
                        pageSize,
                        cancellationToken);

            return Ok(
                result);
        }
        catch (
            DeviceCommandException exception)
        {
            return MapException(
                exception);
        }
    }

    [HttpGet("{commandId:guid}")]
    [RequirePermission(
        PermissionCodes.Devices.View)]
    public async Task<IActionResult>
        GetById(
            Guid commandId,
            CancellationToken cancellationToken = default)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var command =
            await _deviceCommandService
                .GetByIdAsync(
                    context.Value.OrganizationId,
                    commandId,
                    cancellationToken);

        if (
            command is null)
        {
            return NotFound(
                new
                {
                    code =
                        "COMMAND_NOT_FOUND",

                    message =
                        "El comando no existe."
                });
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    command.DeviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        return Ok(
            command);
    }

    [HttpPost("{commandId:guid}/cancel")]
    [RequirePermission(
        PermissionCodes.Devices.Commands)]
    public async Task<IActionResult>
        Cancel(
            Guid commandId,
            CancellationToken cancellationToken = default)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var command =
            await _deviceCommandService
                .GetByIdAsync(
                    context.Value.OrganizationId,
                    commandId,
                    cancellationToken);

        if (
            command is null)
        {
            return NotFound();
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    command.DeviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        try
        {
            await _deviceCommandService
                .CancelAsync(
                    context.Value.OrganizationId,
                    commandId,
                    cancellationToken);

            return NoContent();
        }
        catch (
            DeviceCommandException exception)
        {
            return MapException(
                exception);
        }
    }

    private IActionResult MapException(
        DeviceCommandException exception)
    {
        var statusCode =
            exception.Code switch
            {
                "INVALID_ORGANIZATION" =>
                    StatusCodes
                        .Status400BadRequest,

                "INVALID_USER" =>
                    StatusCodes
                        .Status400BadRequest,

                "INVALID_DEVICE" =>
                    StatusCodes
                        .Status400BadRequest,

                "INVALID_COMMAND_TYPE" =>
                    StatusCodes
                        .Status400BadRequest,

                "INVALID_EXPIRATION" =>
                    StatusCodes
                        .Status400BadRequest,

                "INVALID_STATUS" =>
                    StatusCodes
                        .Status400BadRequest,

                "DEVICE_NOT_FOUND" =>
                    StatusCodes
                        .Status404NotFound,

                "COMMAND_NOT_FOUND" =>
                    StatusCodes
                        .Status404NotFound,

                "COMMAND_TERMINAL" =>
                    StatusCodes
                        .Status409Conflict,

                _ =>
                    StatusCodes
                        .Status400BadRequest
            };

        return StatusCode(
            statusCode,
            new
            {
                code =
                    exception.Code,

                message =
                    exception.Message
            });
    }

    private SecurityContext?
        GetSecurityContext()
    {
        var organizationText =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userText =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        if (
            !Guid.TryParse(
                organizationText,
                out var organizationId)
            ||
            !Guid.TryParse(
                userText,
                out var userId))
        {
            return null;
        }

        return new SecurityContext(
            organizationId,
            userId);
    }

    private readonly record struct
        SecurityContext(
            Guid OrganizationId,
            Guid UserId);
}