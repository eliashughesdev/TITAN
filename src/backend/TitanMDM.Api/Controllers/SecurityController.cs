using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/security")]
[Authorize]
[RequirePermission(
    PermissionCodes.Security.View)]
public sealed class SecurityController
    : ControllerBase
{
    private readonly ISecurityPostureService
        _securityPostureService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public SecurityController(
        ISecurityPostureService securityPostureService,
        IScopeAccessService scopeAccessService)
    {
        _securityPostureService =
            securityPostureService;

        _scopeAccessService =
            scopeAccessService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult>
        GetDashboard(
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            !await _scopeAccessService
                .HasOrganizationScopeAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken))
        {
            return Forbid();
        }

        return Ok(
            await _securityPostureService
                .GetDashboardAsync(
                    context.Value.OrganizationId,
                    cancellationToken));
    }

    [HttpGet("devices")]
    public async Task<IActionResult>
        GetDevices(
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            !await _scopeAccessService
                .HasOrganizationScopeAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken))
        {
            return Forbid();
        }

        return Ok(
            await _securityPostureService
                .GetDevicesAsync(
                    context.Value.OrganizationId,
                    cancellationToken));
    }

    private SecurityContext?
        GetSecurityContext()
    {
        var organization =
            User.FindFirstValue(
                "organization_id");

        var user =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        if (
            !Guid.TryParse(
                organization,
                out var organizationId)
            ||
            !Guid.TryParse(
                user,
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