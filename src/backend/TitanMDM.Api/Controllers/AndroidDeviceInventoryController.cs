using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.AndroidEnterprise;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/android-enterprise/devices")]
public sealed class AndroidDeviceInventoryController
    : ControllerBase
{
    private readonly IAndroidDeviceSyncService
        _syncService;

    public AndroidDeviceInventoryController(
        IAndroidDeviceSyncService syncService)
    {
        _syncService =
            syncService ??
            throw new ArgumentNullException(
                nameof(syncService));
    }

    [HttpPost("sync")]
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
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _syncService
                .GetSummaryAsync(
                    organizationId.Value,
                    cancellationToken);

        return Ok(result);
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
}