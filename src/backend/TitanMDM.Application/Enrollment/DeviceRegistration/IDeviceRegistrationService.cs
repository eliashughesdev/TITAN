namespace TitanMDM.Application.Enrollment.DeviceRegistration;

public interface IDeviceRegistrationService
{
    Task<RegisterDeviceResultDto> RegisterAsync(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default);
}