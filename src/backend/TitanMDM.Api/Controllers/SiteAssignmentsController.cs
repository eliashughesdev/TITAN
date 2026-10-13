using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Security;
using TitanMDM.Application.Sites;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/site-assignments")]
public sealed class SiteAssignmentsController
    : ControllerBase
{
    private readonly ISiteAssignmentService
        _siteAssignmentService;

    public SiteAssignmentsController(
        ISiteAssignmentService siteAssignmentService)
    {
        _siteAssignmentService =
            siteAssignmentService;
    }

    [HttpGet("users")]
    [RequirePermission(
        PermissionCodes.Users.View)]
    [RequirePermission(
        PermissionCodes.Sites.View)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] bool unassignedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();
        if (organizationId == Guid.Empty)
        {
            return Unauthorized();
        }

        return Ok(await _siteAssignmentService.GetUsersAsync(
            organizationId,
            search,
            unassignedOnly,
            cancellationToken));
    }

    [HttpPut("users/{userId:guid}")]
    [RequirePermission(
        PermissionCodes.Users.Manage)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult> AssignUser(
        Guid userId,
        [FromBody]
        AssignSiteRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            () =>
                _siteAssignmentService
                    .AssignUserAsync(
                        GetOrganizationId(),
                        userId,
                        request.SiteId,
                        request.SiteLocationId,
                    cancellationToken));
    }

    [HttpPut("users")]
    [RequirePermission(
        PermissionCodes.Users.Manage)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult> AssignUsers(
        [FromBody] AssignUsersToSiteRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            () => _siteAssignmentService.AssignUsersAsync(
                GetOrganizationId(),
                request.UserIds,
                request.SiteId,
                request.SiteLocationId,
                cancellationToken));
    }

    [HttpPut("devices/{deviceId:guid}")]
    [RequirePermission(
        PermissionCodes.Devices.Update)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult> AssignDevice(
        Guid deviceId,
        [FromBody]
        AssignSiteRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            () =>
                _siteAssignmentService
                    .AssignDeviceAsync(
                        GetOrganizationId(),
                        deviceId,
                        request.SiteId,
                        request.SiteLocationId,
                        cancellationToken));
    }

    [HttpPut("tickets/{ticketId:guid}")]
    [RequirePermission(
        PermissionCodes.Helpdesk.Manage)]
    [RequirePermission(
        PermissionCodes.Sites.Manage)]
    public Task<IActionResult> AssignTicket(
        Guid ticketId,
        [FromBody]
        AssignSiteRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            () =>
                _siteAssignmentService
                    .AssignTicketAsync(
                        GetOrganizationId(),
                        ticketId,
                        request.SiteId,
                        request.SiteLocationId,
                        cancellationToken));
    }

    private async Task<IActionResult>
        ExecuteAsync(
            Func<Task> operation)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            await operation();

            return NoContent();
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

    private Guid GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : Guid.Empty;
    }
}

public sealed record AssignSiteRequest(
    Guid? SiteId,
    Guid? SiteLocationId);

public sealed record AssignUsersToSiteRequest(
    IReadOnlyCollection<Guid> UserIds,
    Guid? SiteId,
    Guid? SiteLocationId);
