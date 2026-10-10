using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Devices.Agent;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Hubs;

[AllowAnonymous]
public sealed class DeviceCommandHub : Hub
{
    public const string Route = "/hubs/device-commands";

    private readonly IDeviceAuthenticator _deviceAuthenticator;
    private readonly IScopeAccessService _scopeAccess;
    private readonly TitanMdmDbContext _dbContext;
    private readonly ILogger<DeviceCommandHub> _logger;

    public DeviceCommandHub(
        IDeviceAuthenticator deviceAuthenticator,
        IScopeAccessService scopeAccess,
        TitanMdmDbContext dbContext,
        ILogger<DeviceCommandHub> logger)
    {
        _deviceAuthenticator = deviceAuthenticator;
        _scopeAccess = scopeAccess;
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var deviceIdText = Context.GetHttpContext()?
            .Request.Headers["X-Titan-Device-Id"]
            .FirstOrDefault();

        if (Guid.TryParse(deviceIdText, out var deviceId))
        {
            var secret = Context.GetHttpContext()?
                .Request.Headers["X-Titan-Device-Secret"]
                .FirstOrDefault();

            try
            {
                await _deviceAuthenticator.AuthenticateAsync(
                    deviceId,
                    secret ?? string.Empty,
                    Context.ConnectionAborted);

                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    AgentGroup(deviceId),
                    Context.ConnectionAborted);

                Context.Items["DeviceId"] = deviceId;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "WindowsAgent command channel authentication failed. DeviceId={DeviceId}.",
                    deviceId);

                Context.Abort();
                return;
            }
        }
        else if (Context.User?.Identity?.IsAuthenticated != true)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    [Authorize]
    public async Task SubscribeDevice(Guid deviceId)
    {
        var identity = GetHumanIdentity();

        if (!HasPermission("devices.view"))
        {
            throw new HubException("No tienes permiso para consultar comandos.");
        }

        var belongsToOrganization = await _dbContext.Devices
            .AsNoTracking()
            .AnyAsync(device =>
                device.Id == deviceId &&
                device.OrganizationId == identity.OrganizationId &&
                !device.IsDeleted,
                Context.ConnectionAborted);

        if (!belongsToOrganization ||
            !await _scopeAccess.CanAccessDeviceAsync(
                identity.OrganizationId,
                identity.UserId,
                deviceId,
                Context.ConnectionAborted))
        {
            throw new HubException("El dispositivo no existe o está fuera de tu alcance.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ViewerGroup(identity.OrganizationId, deviceId),
            Context.ConnectionAborted);
    }

    [Authorize]
    public Task UnsubscribeDevice(Guid deviceId)
    {
        var identity = GetHumanIdentity();

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            ViewerGroup(identity.OrganizationId, deviceId),
            Context.ConnectionAborted);
    }

    internal static string AgentGroup(Guid deviceId) =>
        $"device-command-agent:{deviceId:N}";

    internal static string ViewerGroup(Guid organizationId, Guid deviceId) =>
        $"device-command-viewer:{organizationId:N}:{deviceId:N}";

    private (Guid OrganizationId, Guid UserId) GetHumanIdentity()
    {
        var organizationText =
            Context.User?.FindFirstValue("organization_id") ??
            Context.User?.FindFirstValue("organizationId");

        var userText =
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Context.User?.FindFirstValue("sub");

        if (!Guid.TryParse(organizationText, out var organizationId) ||
            !Guid.TryParse(userText, out var userId))
        {
            throw new HubException("La identidad del técnico no es válida.");
        }

        return (organizationId, userId);
    }

    private bool HasPermission(string permission)
    {
        return Context.User?.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(
                claim.Value,
                permission,
                StringComparison.OrdinalIgnoreCase)) == true;
    }
}
