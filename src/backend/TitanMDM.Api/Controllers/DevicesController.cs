using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Devices;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public sealed class DevicesController
    : ControllerBase
{
    private const string DevicesViewPermission =
        "devices.view";

    private const string WindowsWorkspacePermission =
        "workspace.windows.view";

    private const string AndroidWorkspacePermission =
        "workspace.android.view";

    private readonly IDeviceQueryService
        _deviceQueryService;

    private readonly IScopeAccessService
        _scopeAccessService;

    private readonly IDeviceAliasService?
        _aliasService;

    private readonly IDeviceRemovalService?
        _removalService;

    private const string DevicesUpdatePermission =
        "devices.update";

    public DevicesController(
        IDeviceQueryService deviceQueryService,
        IScopeAccessService scopeAccessService,
        IDeviceAliasService? aliasService = null,
        IDeviceRemovalService? removalService = null)
    {
        _deviceQueryService =
            deviceQueryService;

        _scopeAccessService =
            scopeAccessService;

        _aliasService =
            aliasService;

        _removalService =
            removalService;
    }

    // ============================================================
    // ALIAS
    // ============================================================

    [HttpPut("{deviceId:guid}/alias")]
    public async Task<IActionResult>
        UpdateAlias(
            Guid deviceId,
            [FromBody] UpdateDeviceAliasRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(DevicesUpdatePermission))
            return Forbid();

        var context = GetSecurityContext();
        if (context is null)
            return Unauthorized();

        if (_aliasService is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (!await _scopeAccessService.CanAccessDeviceAsync(
                context.Value.OrganizationId,
                context.Value.UserId,
                deviceId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var result = await _aliasService.SetAliasAsync(
                context.Value.OrganizationId,
                deviceId,
                request?.Alias,
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{deviceId:guid}")]
    public async Task<IActionResult> DeleteDevice(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission("devices.delete"))
            return Forbid();

        var context = GetSecurityContext();
        if (context is null)
            return Unauthorized();

        if (_removalService is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (!await _scopeAccessService.CanAccessDeviceAsync(
                context.Value.OrganizationId,
                context.Value.UserId,
                deviceId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var result = await _removalService.RemovePermanentlyAsync(
                context.Value.OrganizationId,
                context.Value.UserId,
                deviceId,
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    // ============================================================
    // LIST
    // ============================================================

    [HttpGet]
    public async Task<IActionResult>
        GetDevices(
            [FromQuery] string? search,
            [FromQuery] string? platform,
            [FromQuery] string? status,
            [FromQuery] string? compliance,
            [FromQuery] bool? managed,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                DevicesViewPermission))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var hasWindowsAccess =
            HasPermission(
                WindowsWorkspacePermission);

        var hasAndroidAccess =
            HasPermission(
                AndroidWorkspacePermission);

        if (
            !hasWindowsAccess
            &&
            !hasAndroidAccess)
        {
            return Forbid();
        }

        var normalizedPlatform =
            NormalizePlatform(
                platform);

        if (
            !string.IsNullOrWhiteSpace(
                normalizedPlatform)
            &&
            normalizedPlatform != "Windows"
            &&
            normalizedPlatform != "Android")
        {
            return BadRequest(
                new
                {
                    message =
                        "Platform debe ser Windows o Android."
                });
        }

        if (
            normalizedPlatform ==
                "Windows"
            &&
            !hasWindowsAccess)
        {
            return Forbid();
        }

        if (
            normalizedPlatform ==
                "Android"
            &&
            !hasAndroidAccess)
        {
            return Forbid();
        }

        if (
            string.IsNullOrWhiteSpace(
                normalizedPlatform))
        {
            if (
                hasWindowsAccess
                &&
                !hasAndroidAccess)
            {
                normalizedPlatform =
                    "Windows";
            }
            else if (
                hasAndroidAccess
                &&
                !hasWindowsAccess)
            {
                normalizedPlatform =
                    "Android";
            }
        }

        /*
         * ========================================================
         * SCOPE
         * ========================================================
         */

        var organizationWide =
            await _scopeAccessService
                .HasOrganizationScopeAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken);

        IReadOnlyCollection<Guid>?
            accessibleSiteIds =
                null;

        if (!organizationWide)
        {
            accessibleSiteIds =
                await _scopeAccessService
                    .GetAccessibleSiteIdsAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        cancellationToken);
        }

        /*
         * Nunca devolvemos 403 solamente porque el usuario
         * sea Site-scoped.
         *
         * Si no tiene Sites:
         * devuelve una lista vacía.
         */

        var result =
            await _deviceQueryService
                .GetDevicesAsync(
                    context.Value.OrganizationId,
                    accessibleSiteIds,
                    search,
                    normalizedPlatform,
                    status,
                    compliance,
                    managed,
                    sortBy,
                    sortDirection,
                    page,
                    pageSize,
                    cancellationToken);

        return Ok(
            result);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    [HttpGet("{deviceId:guid}")]
    public async Task<IActionResult>
        GetDeviceById(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                DevicesViewPermission))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    deviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        var device =
            await _deviceQueryService
                .GetDeviceByIdAsync(
                    context.Value.OrganizationId,
                    deviceId,
                    cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        if (
            string.Equals(
                device.Platform,
                "Windows",
                StringComparison.OrdinalIgnoreCase)
            &&
            !HasPermission(
                WindowsWorkspacePermission))
        {
            return Forbid();
        }

        if (
            string.Equals(
                device.Platform,
                "Android",
                StringComparison.OrdinalIgnoreCase)
            &&
            !HasPermission(
                AndroidWorkspacePermission))
        {
            return Forbid();
        }

        return Ok(
            device);
    }

    // ============================================================
    // ANDROID DETAILS
    // ============================================================

    [HttpGet("{deviceId:guid}/android")]
    public async Task<IActionResult>
        GetAndroidDeviceDetails(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        if (
            !HasPermission(
                DevicesViewPermission)
            ||
            !HasPermission(
                AndroidWorkspacePermission))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    deviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        var android =
            await _deviceQueryService
                .GetAndroidDeviceDetailsAsync(
                    context.Value.OrganizationId,
                    deviceId,
                    cancellationToken);

        return android is null
            ? NotFound()
            : Ok(
                android);
    }

    // ============================================================
    // SNAPSHOT
    // ============================================================

    [HttpGet("{deviceId:guid}/snapshot")]
    public async Task<IActionResult>
        GetOperationalSnapshot(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        if (!HasPermission(
                DevicesViewPermission))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            !await _scopeAccessService
                .CanAccessDeviceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    deviceId,
                    cancellationToken))
        {
            return Forbid();
        }

        var snapshot =
            await _deviceQueryService
                .GetOperationalSnapshotAsync(
                    context.Value.OrganizationId,
                    deviceId,
                    cancellationToken);

        if (snapshot is null)
        {
            return NotFound();
        }

        if (
            string.Equals(
                snapshot.Device.Platform,
                "Windows",
                StringComparison.OrdinalIgnoreCase)
            &&
            !HasPermission(
                WindowsWorkspacePermission))
        {
            return Forbid();
        }

        if (
            string.Equals(
                snapshot.Device.Platform,
                "Android",
                StringComparison.OrdinalIgnoreCase)
            &&
            !HasPermission(
                AndroidWorkspacePermission))
        {
            return Forbid();
        }

        return Ok(
            snapshot);
    }

    private static string?
        NormalizePlatform(
            string? platform)
    {
        if (
            string.IsNullOrWhiteSpace(
                platform))
        {
            return null;
        }

        var value =
            platform.Trim();

        if (
            value.Equals(
                "Windows",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Windows";
        }

        if (
            value.Equals(
                "Android",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Android";
        }

        return value;
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

    private bool HasPermission(
        string permission)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct
        SecurityContext(
            Guid OrganizationId,
            Guid UserId);
}
