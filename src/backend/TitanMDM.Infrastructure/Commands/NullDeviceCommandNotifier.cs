using TitanMDM.Application.Commands;

namespace TitanMDM.Infrastructure.Commands;

internal sealed class NullDeviceCommandNotifier
    : IDeviceCommandNotifier
{
    public Task NotifyAvailableAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task NotifyUpdatedAsync(
        DeviceCommandDto command,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
