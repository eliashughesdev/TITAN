using Microsoft.AspNetCore.SignalR;
using TitanMDM.Api.Hubs;
using TitanMDM.Application.Location;

namespace TitanMDM.Api.Services;

/// <summary>
/// Emite DeviceLocationUpdated a los visores
/// suscritos al dispositivo usando el hub
/// SignalR de comandos existente.
/// </summary>
public sealed class LocationSignalRNotifier
    : ILocationUpdateNotifier
{
    private readonly IHubContext<DeviceCommandHub>
        _hubContext;

    private readonly ILogger<LocationSignalRNotifier>
        _logger;

    public LocationSignalRNotifier(
        IHubContext<DeviceCommandHub> hubContext,
        ILogger<LocationSignalRNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyLocationUpdatedAsync(
        Guid organizationId,
        Guid deviceId,
        string locationState,
        DateTime? capturedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            deviceId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _hubContext.Clients.Group(
                    DeviceCommandHub.ViewerGroup(
                        organizationId,
                        deviceId))
                .SendAsync(
                    "DeviceLocationUpdated",
                    new
                    {
                        deviceId,
                        organizationId,
                        locationState,
                        capturedAtUtc
                    },
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "No fue posible emitir DeviceLocationUpdated para {DeviceId}.",
                deviceId);
        }
    }
}
