namespace TitanMDM.Application.Devices.Agent;

public interface IDeviceAuthenticator
{
    Task AuthenticateAsync(
        Guid deviceId,
        string deviceSecret,
        CancellationToken cancellationToken = default);
}