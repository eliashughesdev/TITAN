using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Notifications;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

/*
 * Fase 3 - Centro de notificaciones. Cualquier usuario
 * autenticado puede consultarlo; el contenido se filtra por
 * sus permisos (claims "permission") y alcance de sede.
 */
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationFeedService _feed;
    private readonly IScopeAccessService _scope;

    public NotificationsController(
        INotificationFeedService feed,
        IScopeAccessService scope)
    {
        _feed = feed;
        _scope = scope;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationFeedDto>> Get(
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("organization_id"), out var organizationId) ||
            !Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
                out var userId))
        {
            return Unauthorized();
        }

        var permissions =
            User.FindAll("permission")
                .Select(x => x.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshot =
            await _scope.GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        var feed =
            await _feed.GetFeedAsync(
                new NotificationFeedRequest(
                    organizationId,
                    userId,
                    permissions,
                    snapshot.OrganizationWide ? null : snapshot.SiteIds),
                cancellationToken);

        Response.Headers.CacheControl = "no-store";

        return Ok(feed);
    }
}
