namespace TitanMDM.Application.Automation;

public interface IAutomationEventDispatcher
{
    Task DispatchAsync(
        Guid organizationId,
        Guid? deviceId,
        string triggerType,
        object? payload = null,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default);
}