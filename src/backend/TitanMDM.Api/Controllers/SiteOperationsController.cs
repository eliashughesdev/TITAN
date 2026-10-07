using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Security;
using TitanMDM.Application.Sites;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sites/{siteId:guid}/operations")]
public sealed class SiteOperationsController
    : ControllerBase
{
    private readonly ISiteOperationsService
        _siteOperationsService;

    public SiteOperationsController(
        ISiteOperationsService siteOperationsService)
    {
        _siteOperationsService =
            siteOperationsService;
    }

    // ============================================================
    // DASHBOARD / SUMMARY
    // ============================================================

    [HttpGet("summary")]
    [RequirePermission(
        PermissionCodes.Dashboard.View)]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult>
        GetSummary(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteOperationsService
                    .GetSummaryAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        siteId,
                        cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    // ============================================================
    // POLICY
    // ============================================================

    [HttpPost("policies")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        AssignPolicy(
            Guid siteId,
            [FromBody]
            AssignPolicyToSiteRequest request,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteOperationsService
                    .AssignPolicyAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        siteId,
                        request.PolicyId,
                        cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    // ============================================================
    // SOFTWARE
    // ============================================================

    [HttpPost("software")]
    [RequirePermission(
        PermissionCodes.Applications.Manage)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public async Task<IActionResult>
        DeploySoftware(
            Guid siteId,
            [FromBody]
            DeploySoftwareToSiteRequest request,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _siteOperationsService
                    .DeploySoftwareAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        siteId,
                        request.PackageId,
                        cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    // ============================================================
    // REPORT EXPORT
    // ============================================================

    [HttpGet("reports/devices.csv")]
    [RequirePermission(
        PermissionCodes.Reports.Export)]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult>
        ExportDevices(
            Guid siteId,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            var bytes =
                await _siteOperationsService
                    .ExportDevicesCsvAsync(
                        context.Value.OrganizationId,
                        context.Value.UserId,
                        siteId,
                        cancellationToken);

            return File(
                bytes,
                "text/csv; charset=utf-8",
                $"titanmdm-site-{siteId:N}-devices-" +
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    // ============================================================
    // CLAIMS
    // ============================================================

    private SecurityContext?
        GetSecurityContext()
    {
        var organizationText =
            User.FindFirstValue(
                "organization_id");

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

    private readonly record struct SecurityContext(
        Guid OrganizationId,
        Guid UserId);
}