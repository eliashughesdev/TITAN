using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;
using TitanMDM.Application.Applications;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
[RequirePermission(
    PermissionCodes.Applications.View)]
public sealed class ApplicationsController
    : ControllerBase
{
    private readonly IApplicationInventoryService
        _applicationInventoryService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public ApplicationsController(
        IApplicationInventoryService applicationInventoryService,
        IScopeAccessService scopeAccessService)
    {
        _applicationInventoryService =
            applicationInventoryService;

        _scopeAccessService =
            scopeAccessService;
    }

    [HttpGet]
    public async Task<IActionResult>
        GetApplications(
            [FromQuery] string? search,
            [FromQuery] bool? systemApp,
            CancellationToken cancellationToken = default)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var scope =
            await _scopeAccessService
                .GetScopeSnapshotAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken);

        IReadOnlyCollection<Guid>?
            siteIds =
                scope.OrganizationWide
                    ? null
                    : scope.SiteIds;

        var result =
            await _applicationInventoryService
                .GetApplicationsAsync(
                    context.Value.OrganizationId,
                    siteIds,
                    search,
                    systemApp,
                    cancellationToken);

        return Ok(
            result);
    }

    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult>
        GetDeviceApplications(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
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

        var result =
            await _applicationInventoryService
                .GetDeviceApplicationsAsync(
                    context.Value.OrganizationId,
                    deviceId,
                    cancellationToken);

        return Ok(
            result);
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