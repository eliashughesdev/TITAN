using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Security;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Location;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[RequireAnyPermission(
    PermissionCodes.Geofencing.View)]
[Route("api/location")]
public sealed class LocationController
    : ControllerBase
{
    private const string AuditTargetType =
        "DeviceLocation";

    private readonly IDeviceLocationService _locations;
    private readonly IDeviceCommandService _commands;
    private readonly IAdministrativeAuditWriter
        _audit;

    public LocationController(
        IDeviceLocationService locations,
        IDeviceCommandService commands,
        IAdministrativeAuditWriter audit)
    {
        _locations = locations;
        _commands = commands;
        _audit = audit;
    }

    [HttpGet("devices/{deviceId:guid}/latest")]
    public async Task<IActionResult> Latest(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var result =
            await _locations.GetLatestAsync(
                organizationId.Value,
                deviceId,
                cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Device.Latest",
            deviceId.ToString(),
            result is null
                ? "NotFound"
                : "Success",
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
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var result =
            await _locations.GetHistoryAsync(
                organizationId.Value,
                deviceId,
                limit,
                cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Device.History",
            deviceId.ToString(),
            "Success",
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("devices/{deviceId:guid}/request")]
    [RequireAnyPermission(
        PermissionCodes.Geofencing.Manage)]
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

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Device.Request",
            deviceId.ToString(),
            "Success",
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

    [HttpGet("fleet")]
    public async Task<IActionResult> GetFleet(
        [FromQuery] FleetLocationFilters filters,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var result =
            await _locations.GetFleetLocationsAsync(
                organizationId.Value,
                filters,
                cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Fleet.Query",
            null,
            "Success",
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var policy =
            await _locations.GetReportingPolicyAsync(
                organizationId.Value,
                cancellationToken);

        return policy is null
            ? Ok(new { isEnabled = false })
            : Ok(policy);
    }

    [HttpPost("policy")]
    [RequireAnyPermission(
        PermissionCodes.Geofencing.Manage)]
    public async Task<IActionResult> UpdatePolicy(
        UpdateReportingPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var result =
            await _locations.UpdateReportingPolicyAsync(
                organizationId.Value,
                request,
                cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Policy.Update",
            result.Id.ToString(),
            "Success",
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("geofences")]
    [RequireAnyPermission(
        PermissionCodes.Geofencing.Manage)]
    public async Task<IActionResult> CreateGeofence(
        CreateGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var result =
            await _locations.CreateGeofenceAsync(
                organizationId.Value,
                request,
                cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Geofence.Create",
            result.Id.ToString(),
            "Success",
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("geofences/{geofenceId:guid}/devices")]
    [RequireAnyPermission(
        PermissionCodes.Geofencing.Manage)]
    public async Task<IActionResult> Assign(
        Guid geofenceId,
        AssignGeofenceDevicesRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        await _locations.AssignDevicesAsync(
            organizationId.Value,
            geofenceId,
            request.DeviceIds,
            cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Geofence.Assign",
            geofenceId.ToString(),
            "Success",
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("geofences/{geofenceId:guid}")]
    [RequireAnyPermission(
        PermissionCodes.Geofencing.Manage)]
    public async Task<IActionResult> Delete(
        Guid geofenceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        await _locations.DeleteGeofenceAsync(
            organizationId.Value,
            geofenceId,
            cancellationToken);

        await WriteAuditAsync(
            organizationId.Value,
            userId.Value,
            "Location.Geofence.Delete",
            geofenceId.ToString(),
            "Success",
            cancellationToken);

        return NoContent();
    }

    private async Task WriteAuditAsync(
        Guid organizationId,
        Guid userId,
        string action,
        string? targetId,
        string result,
        CancellationToken cancellationToken)
    {
        try
        {
            await _audit.WriteAsync(
                new AdministrativeAuditEntry(
                    organizationId,
                    userId,
                    action,
                    AuditTargetType,
                    targetId,
                    result,
                    GetCorrelationId(),
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString()),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            /*
             * La auditoria nunca debe romper la
             * consulta de ubicaciones.
             */
        }
    }

    private string GetCorrelationId()
    {
        if (Request.Headers.TryGetValue(
                "X-Correlation-Id",
                out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return Activity.Current?.Id ??
            HttpContext.TraceIdentifier;
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
