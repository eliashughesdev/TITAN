using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Audit;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController
    : ControllerBase
{
    private readonly IAuditQueryService
        _auditQueryService;

    public AuditController(
        IAuditQueryService auditQueryService)
    {
        _auditQueryService =
            auditQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] string? module,
        [FromQuery] string? action,
        [FromQuery] string? status,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (
            !organizationId
                .HasValue)
        {
            return Unauthorized();
        }

        var result =
            await _auditQueryService
                .GetEventsAsync(
                    organizationId.Value,
                    new AuditQueryDto(
                        search,
                        module,
                        action,
                        status,
                        userId,
                        deviceId,
                        fromUtc,
                        toUtc,
                        page,
                        pageSize),
                    cancellationToken);

        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult>
        GetSummary(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (
            !organizationId
                .HasValue)
        {
            return Unauthorized();
        }

        return Ok(
            await _auditQueryService
                .GetSummaryAsync(
                    organizationId.Value,
                    fromUtc,
                    toUtc,
                    cancellationToken));
    }

    [HttpGet("filters")]
    public async Task<IActionResult>
        GetFilters(
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (
            !organizationId
                .HasValue)
        {
            return Unauthorized();
        }

        return Ok(
            await _auditQueryService
                .GetFilterOptionsAsync(
                    organizationId.Value,
                    cancellationToken));
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult>
        ExportCsv(
            [FromQuery] string? search,
            [FromQuery] string? module,
            [FromQuery] string? action,
            [FromQuery] string? status,
            [FromQuery] Guid? userId,
            [FromQuery] Guid? deviceId,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (
            !organizationId
                .HasValue)
        {
            return Unauthorized();
        }

        var bytes =
            await _auditQueryService
                .ExportCsvAsync(
                    organizationId.Value,
                    new AuditQueryDto(
                        search,
                        module,
                        action,
                        status,
                        userId,
                        deviceId,
                        fromUtc,
                        toUtc,
                        1,
                        100),
                    cancellationToken);

        return File(
            bytes,
            "text/csv",
            $"titanmdm-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var organizationId)
            ? organizationId
            : null;
    }
}