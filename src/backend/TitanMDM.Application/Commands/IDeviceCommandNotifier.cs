namespace TitanMDM.Application.Commands;

public interface IDeviceCommandNotifier
{
    Task NotifyAvailableAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task NotifyUpdatedAsync(
        DeviceCommandDto command,
        CancellationToken cancellationToken = default);
}
