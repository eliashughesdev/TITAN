using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Security;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(RbacAuditFilter))]
[Route("api/android-enterprise/devices")]
public sealed class AndroidDeviceInventoryController
    : ControllerBase
{
    private readonly IAndroidDeviceSyncService
        _syncService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public AndroidDeviceInventoryController(
        IAndroidDeviceSyncService syncService,
        IScopeAccessService scopeAccessService)
    {
        _syncService =
            syncService ??
            throw new ArgumentNullException(
                nameof(syncService));

        _scopeAccessService =
            scopeAccessService ??
            throw new ArgumentNullException(
                nameof(scopeAccessService));
    }

    [HttpGet]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetInventory(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var accessibleSiteIds =
            await ResolveSiteScopeAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken);

        var result =
            await _syncService
                .GetInventoryPageAsync(
                    organizationId.Value,
                    search,
                    page,
                    pageSize,
                    accessibleSiteIds,
                    cancellationToken);

        return Ok(result);
    }

    [HttpPost("sync")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> Synchronize(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        try
        {
            var result =
                await _syncService
                    .SynchronizeAsync(
                        organizationId.Value,
                        cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    [HttpGet("summary")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
            return Unauthorized();

        var accessibleSiteIds =
            await ResolveSiteScopeAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken);

        var result =
            await _syncService
                .GetSummaryAsync(
                    organizationId.Value,
                    accessibleSiteIds,
                    cancellationToken);

        return Ok(result);
    }

    private async Task<IReadOnlyCollection<Guid>?>
        ResolveSiteScopeAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken)
    {
        var organizationWide =
            await _scopeAccessService
                .HasOrganizationScopeAsync(
                    organizationId,
                    userId,
                    cancellationToken);

        if (organizationWide)
            return null;

        return await _scopeAccessService
            .GetAccessibleSiteIdsAsync(
                organizationId,
                userId,
                cancellationToken);
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