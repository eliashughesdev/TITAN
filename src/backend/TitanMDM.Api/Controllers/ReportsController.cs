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

        var sites =
            await GetAccessibleSitesAsync(
                context.Value,
                cancellationToken);

        return Ok(
            await _reportsService
                .GetOverviewAsync(
                    context.Value.OrganizationId,
                    sites,
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

        var sites =
            await GetAccessibleSitesAsync(
                context.Value,
                cancellationToken);

        var bytes =
            await _reportsService
                .ExportDevicesCsvAsync(
                    context.Value.OrganizationId,
                    sites,
                    cancellationToken);

        return File(
            bytes,
            "text/csv; charset=utf-8",
            $"titanmdm-devices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("devices/export/excel")]
    [RequirePermission(
        PermissionCodes.Reports.Export)]
    public async Task<IActionResult>
        ExportDevicesExcel(
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var sites =
            await GetAccessibleSitesAsync(
                context.Value,
                cancellationToken);

        var bytes =
            await _reportsService
                .ExportDevicesExcelAsync(
                    context.Value.OrganizationId,
                    sites,
                    cancellationToken);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"titanmdm-devices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet("devices/export/pdf")]
    [RequirePermission(
        PermissionCodes.Reports.Export)]
    public async Task<IActionResult>
        ExportDevicesPdf(
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        var sites =
            await GetAccessibleSitesAsync(
                context.Value,
                cancellationToken);

        var bytes =
            await _reportsService
                .ExportDevicesPdfAsync(
                    context.Value.OrganizationId,
                    sites,
                    cancellationToken);

        return File(
            bytes,
            "application/pdf",
            $"titanmdm-devices-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }

    private async Task<
        IReadOnlyCollection<Guid>?>
        GetAccessibleSitesAsync(
            SecurityContext context,
            CancellationToken cancellationToken)
    {
        var scope =
            await _scopeAccessService
                .GetScopeSnapshotAsync(
                    context.OrganizationId,
                    context.UserId,
                    cancellationToken);

        return scope.OrganizationWide
            ? null
            : scope.SiteIds;
    }

    private SecurityContext?
        GetSecurityContext()
    {
        var organization =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

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