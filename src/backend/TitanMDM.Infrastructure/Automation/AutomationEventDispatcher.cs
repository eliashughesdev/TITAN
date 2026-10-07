using System.Text.Json;
using TitanMDM.Application.Automation;

namespace TitanMDM.Infrastructure.Automation;

public sealed class AutomationEventDispatcher
    : IAutomationEventDispatcher
{
    private readonly IAutomationService
        _automationService;

    public AutomationEventDispatcher(
        IAutomationService automationService)
    {
        _automationService =
            automationService;
    }

    public async Task DispatchAsync(
        Guid organizationId,
        Guid? deviceId,
        string triggerType,
        object? payload = null,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            return;

        if (string.IsNullOrWhiteSpace(
                triggerType))
            return;

        var payloadJson =
            payload is null
                ? "{}"
                : JsonSerializer.Serialize(
                    payload);

        await _automationService
            .ProcessEventAsync(
                new AutomationEvent(
                    organizationId,
                    deviceId,
                    triggerType,
                    payloadJson,
                    actorUserId ??
                    Guid.Empty),
                cancellationToken);
    }
}