namespace TitanMDM.Application.Enrollment.DeviceRegistration;

public sealed class DeviceRegistrationException : Exception
{
    public DeviceRegistrationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}