using Microsoft.AspNetCore.SignalR;

using TitanMDM.Api.Hubs;
using TitanMDM.Application.Commands;

namespace TitanMDM.Api.Services;

public sealed class DeviceCommandSignalRNotifier
    : IDeviceCommandNotifier
{
    private readonly IHubContext<DeviceCommandHub> _hubContext;
    private readonly ILogger<DeviceCommandSignalRNotifier> _logger;

    public DeviceCommandSignalRNotifier(
        IHubContext<DeviceCommandHub> hubContext,
        ILogger<DeviceCommandSignalRNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task NotifyAvailableAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        return SendSafelyAsync(
            _hubContext.Clients.Group(DeviceCommandHub.AgentGroup(deviceId)),
            "CommandAvailable",
            commandId);
    }

    public Task NotifyUpdatedAsync(
        DeviceCommandDto command,
        CancellationToken cancellationToken = default)
    {
        return SendSafelyAsync(
            _hubContext.Clients.Group(
                DeviceCommandHub.ViewerGroup(
                    command.OrganizationId,
                    command.DeviceId)),
            "DeviceCommandUpdated",
            command);
    }

    private async Task SendSafelyAsync(
        IClientProxy client,
        string method,
        object payload)
    {
        try
        {
            await client.SendCoreAsync(
                method,
                [payload],
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Device command SignalR notification failed. Method={Method}.",
                method);
        }
    }
}
