using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Api.Authorization;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[ModuleAccess("apps.view", "apps.manage")]
[Route("api/software-packages/deployments")]
public sealed class SoftwareDeploymentResultsController(
    TitanMdmDbContext db) : ControllerBase
{
    [HttpGet("{deploymentId:guid}/results")]
    public async Task<IActionResult> Get(
        Guid deploymentId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var claim =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        if (!Guid.TryParse(claim, out var organizationId))
            return Unauthorized();

        if (
            page < 1 ||
            page > 1000000 ||
            pageSize < 1 ||
            pageSize > 100)
        {
            return BadRequest();
        }

        var deployment = await db.SoftwareDeployments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == deploymentId &&
                    x.OrganizationId == organizationId,
                cancellationToken);

        if (deployment is null)
            return NotFound();

        // El productor serializa el payload sin indentación.
        var marker =
            $"\"deploymentId\":\"{deploymentId:D}\"";

        var query = db.DeviceCommands
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.CommandType == "SOFTWARE_INSTALL" &&
                x.PayloadJson.Contains(marker));

        var totalCount =
            await query.CountAsync(cancellationToken);

        var groups = await query
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var rows = await (
            from command in query
            join device in db.Devices
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                on command.DeviceId equals device.Id
                into devices
            from device in devices.DefaultIfEmpty()
            orderby command.CreatedAtUtc descending, command.Id
            select new
            {
                command.Id,
                command.DeviceId,
                DeviceName = device == null
                    ? "Equipo retirado"
                    : device.DeviceName,
                command.Status,
                command.CreatedAtUtc,
                command.UpdatedAtUtc,
                command.CompletedAtUtc,
                command.ErrorCode,
                command.ErrorMessage,
                command.ResultJson
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize),
            queuedDevices = deployment.QueuedDevices,
            correlationAvailable = totalCount > 0,
            summary = groups.Select(x => new
            {
                status = x.Status.ToString(),
                count = x.Count
            }),
            items = rows.Select(x => new
            {
                x.Id,
                x.DeviceId,
                x.DeviceName,
                Status = x.Status.ToString(),
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CompletedAtUtc,
                x.ErrorCode,
                x.ErrorMessage,
                x.ResultJson
            })
        });
    }
}