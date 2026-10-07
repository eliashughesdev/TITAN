using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Api.Authorization;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[ModuleAccess("reports.view", "reports.export")]
[Route("api/reports/device-detail")]
public sealed class ReportDeviceDetailsController(
    TitanMdmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        string? search,
        string? platform,
        string? status,
        string? compliance,
        string? department,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 50,
        bool export = false,
        CancellationToken cancellationToken = default)
    {
        var claim =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        if (
            !Guid.TryParse(claim, out var organizationId) ||
            organizationId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (
            export &&
            !User.Claims.Any(x =>
                x.Type == "permission" &&
                x.Value == "reports.export"))
        {
            return Forbid();
        }

        if (
            page < 1 ||
            page > 1000000 ||
            pageSize < 1 ||
            pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Página o tamaño de página inválido."
            });
        }

        if (
            fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc > toUtc)
        {
            return BadRequest(new
            {
                message = "La fecha inicial no puede superar la final."
            });
        }

        var query = db.Devices
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(platform))
        {
            if (
                !Enum.TryParse<DevicePlatform>(
                    platform, true, out var value) ||
                !Enum.IsDefined(value))
            {
                return BadRequest(new
                {
                    message = "Plataforma inválida."
                });
            }

            query = query.Where(x => x.Platform == value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (
                !Enum.TryParse<DeviceStatus>(
                    status, true, out var value) ||
                !Enum.IsDefined(value))
            {
                return BadRequest(new
                {
                    message = "Estado inválido."
                });
            }

            query = query.Where(x => x.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(compliance))
        {
            if (
                !Enum.TryParse<ComplianceStatus>(
                    compliance, true, out var value) ||
                !Enum.IsDefined(value))
            {
                return BadRequest(new
                {
                    message = "Cumplimiento inválido."
                });
            }

            query = query.Where(
                x => x.ComplianceStatus == value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.DeviceName.Contains(term) ||
                x.SerialNumber.Contains(term) ||
                (
                    x.AssignedUser != null &&
                    x.AssignedUser.Contains(term)
                ) ||
                (
                    x.IpAddress != null &&
                    x.IpAddress.Contains(term)
                ));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var term = department.Trim();

            query = query.Where(x =>
                x.Department != null &&
                x.Department.Contains(term));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(
                x => x.LastSeenAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(
                x => x.LastSeenAtUtc <= toUtc.Value);
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(x => x.DeviceName)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.DeviceName,
                x.Platform,
                x.Status,
                x.ComplianceStatus,
                x.SerialNumber,
                x.Manufacturer,
                x.Model,
                x.OperatingSystem,
                x.OperatingSystemVersion,
                x.AssignedUser,
                x.Department,
                x.IpAddress,
                x.BatteryLevel,
                x.IsManaged,
                x.EnrolledAtUtc,
                x.LastSeenAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new
        {
            x.Id,
            x.DeviceName,
            Platform = x.Platform.ToString(),
            Status = x.Status.ToString(),
            ComplianceStatus = x.ComplianceStatus.ToString(),
            x.SerialNumber,
            x.Manufacturer,
            x.Model,
            x.OperatingSystem,
            x.OperatingSystemVersion,
            x.AssignedUser,
            x.Department,
            x.IpAddress,
            x.BatteryLevel,
            x.IsManaged,
            x.EnrolledAtUtc,
            x.LastSeenAtUtc
        });

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        });
    }
}