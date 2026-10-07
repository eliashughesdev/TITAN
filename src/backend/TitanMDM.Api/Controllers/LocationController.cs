using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Location;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/location")]
public sealed class LocationController
    : ControllerBase
{
    private readonly IDeviceLocationService _locations;
    private readonly IDeviceCommandService _commands;

    public LocationController(
        IDeviceLocationService locations,
        IDeviceCommandService commands)
    {
        _locations = locations;
        _commands = commands;
    }

    [HttpGet("devices/{deviceId:guid}/latest")]
    public async Task<IActionResult> Latest(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _locations.GetLatestAsync(
                organizationId.Value,
                deviceId,
                cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpGet("devices/{deviceId:guid}/history")]
    public async Task<IActionResult> History(
        Guid deviceId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _locations.GetHistoryAsync(
                organizationId.Value,
                deviceId,
                limit,
                cancellationToken));
    }

    [HttpPost("devices/{deviceId:guid}/request")]
    public async Task<IActionResult> RequestLocation(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var command =
            await _commands.CreateAsync(
                organizationId.Value,
                userId.Value,
                new CreateDeviceCommandRequest(
                    deviceId,
                    "LOCATION_REQUEST",
                    "{}",
                    60),
                cancellationToken);

        return Ok(command);
    }

    [HttpGet("geofences")]
    public async Task<IActionResult> Geofences(
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _locations.GetGeofencesAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpPost("geofences")]
    public async Task<IActionResult> CreateGeofence(
        CreateGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _locations.CreateGeofenceAsync(
                organizationId.Value,
                request,
                cancellationToken));
    }

    [HttpPost("geofences/{geofenceId:guid}/devices")]
    public async Task<IActionResult> Assign(
        Guid geofenceId,
        AssignGeofenceDevicesRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        await _locations.AssignDevicesAsync(
            organizationId.Value,
            geofenceId,
            request.DeviceIds,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("geofences/{geofenceId:guid}")]
    public async Task<IActionResult> Delete(
        Guid geofenceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        await _locations.DeleteGeofenceAsync(
            organizationId.Value,
            geofenceId,
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