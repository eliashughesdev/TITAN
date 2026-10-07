namespace TitanMDM.Application.Devices.Agent;

public interface IDeviceAgentService
{
    Task<DeviceHeartbeatResultDto> HeartbeatAsync(
        DeviceHeartbeatRequest request,
        CancellationToken cancellationToken = default);
}