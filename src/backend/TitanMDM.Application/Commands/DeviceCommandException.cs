namespace TitanMDM.Application.Commands;

public sealed class DeviceCommandException : Exception
{
    public DeviceCommandException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}