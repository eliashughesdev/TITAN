namespace TitanMDM.Application.Commands.Agent;

public interface IDeviceCommandAgentService
{
    Task ExpireStaleCommandsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task MarkDeliveredAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task MarkExecutingAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task MarkSuccessAsync(
        Guid deviceId,
        Guid commandId,
        string? resultJson,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid deviceId,
        Guid commandId,
        string errorCode,
        string errorMessage,
        string? resultJson,
        CancellationToken cancellationToken = default);
}
