namespace TitanMDM.Application.Commands;

public interface IDeviceCommandService
{
    Task<DeviceCommandDto> CreateAsync(
        Guid organizationId,
        Guid createdByUserId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken = default);

    Task<DeviceCommandDto?> GetByIdAsync(
        Guid organizationId,
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task<DeviceCommandListResultDto> GetCommandsAsync(
        Guid organizationId,
        Guid? deviceId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        Guid organizationId,
        Guid commandId,
        CancellationToken cancellationToken = default);
}