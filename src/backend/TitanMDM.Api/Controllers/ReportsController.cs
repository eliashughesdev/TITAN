using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Reports;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController
    : ControllerBase
{
    private readonly IReportsService
        _reportsService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public ReportsController(
        IReportsService reportsService,
        IScopeAccessService scopeAccessService)
    {
        _reportsService =
            reportsService;

        _scopeAccessService =
            scopeAccessService;
    }

    [HttpGet("overview")]
    [RequirePermission(
        PermissionCodes.Reports.View)]
    public async Task<IActionResult>
        GetOverview(
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
            await _reportsService
                .GetOverviewAsync(
                    context.Value.OrganizationId,
                    cancellationToken));
    }

    [HttpGet("devices/export/csv")]
    [RequirePermission(
        PermissionCodes.Reports.Export)]
    public async Task<IActionResult>
        ExportDevicesCsv(
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

        var bytes =
            await _reportsService
                .ExportDevicesCsvAsync(
                    context.Value.OrganizationId,
                    cancellationToken);

        return File(
            bytes,
            "text/csv; charset=utf-8",
            $"titanmdm-devices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
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