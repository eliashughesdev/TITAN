namespace TitanMDM.Application.Devices.Agent;

public sealed class DeviceAuthenticationException
    : Exception
{
    public DeviceAuthenticationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}