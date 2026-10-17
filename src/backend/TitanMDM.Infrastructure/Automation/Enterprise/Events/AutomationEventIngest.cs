using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Events;

internal interface IAutomationEventIngest
{
    Task IngestForRulesAsync(
        AutomationEvent automationEvent,
        IReadOnlyCollection<AutomationRule> enterpriseRules,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        Guid organizationId,
        Guid? deviceId,
        string triggerType,
        string? triggerPayloadJson,
        Guid? actorUserId,
        int cycleDepth,
        bool simulated,
        CancellationToken cancellationToken = default);

    Task ExecuteRuleNowAsync(
        AutomationRule rule,
        Guid? deviceId,
        string? triggerPayloadJson,
        Guid actorUserId,
        bool simulated,
        CancellationToken cancellationToken = default);
}

/*
 * AUT-1.3 — Entrada de eventos al motor empresarial.
 *
 * Evalúa las condiciones de cada regla con definición y
 * encola un item idempotente. Después intenta procesarlo en
 * línea (best-effort) para no depender de que el worker esté
 * registrado; el worker recoge lo pendiente.
 */
internal sealed class AutomationEventIngest
    : IAutomationEventIngest
{
    private readonly TitanMdmDbContext _db;
    private readonly IAutomationQueueService _queue;
    private readonly IAutomationQueueProcessor _processor;

    public AutomationEventIngest(
        TitanMdmDbContext db,
        IAutomationQueueService queue,
        IAutomationQueueProcessor processor)
    {
        _db = db;
        _queue = queue;
        _processor = processor;
    }

    public async Task IngestForRulesAsync(
        AutomationEvent automationEvent,
        IReadOnlyCollection<AutomationRule> enterpriseRules,
        CancellationToken cancellationToken = default)
    {
        if (enterpriseRules.Count == 0)
            return;

        foreach (var rule in enterpriseRules)
        {
            if (!rule.IsEnabled)
                continue;

            var evaluation =
                Engine.AutomationRuleEngine.Evaluate(
                    rule,
                    automationEvent
                        .TriggerPayloadJson);

            if (!evaluation.Passed)
                continue;

            await EnqueueAndProcessAsync(
                rule,
                automationEvent.OrganizationId,
                automationEvent.DeviceId,
                automationEvent.TriggerType,
                automationEvent.TriggerPayloadJson,
                automationEvent.ActorUserId,
                cycleDepth: 0,
                simulated: false,
                cancellationToken);
        }
    }

    public async Task PublishAsync(
        Guid organizationId,
        Guid? deviceId,
        string triggerType,
        string? triggerPayloadJson,
        Guid? actorUserId,
        int cycleDepth,
        bool simulated,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            return;

        if (!Enum.TryParse<
                AutomationTriggerType>(
                triggerType,
                true,
                out var trigger))
        {
            return;
        }

        var rules =
            await _db
                .Set<AutomationRule>()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.IsEnabled &&
                    x.TriggerType == trigger &&
                    x.DefinitionJson != null)
                .ToListAsync(cancellationToken);

        await IngestForRulesAsync(
            new AutomationEvent(
                organizationId,
                deviceId,
                triggerType,
                triggerPayloadJson ?? "{}",
                actorUserId ?? Guid.Empty),
            rules,
            cancellationToken);
    }

    public async Task ExecuteRuleNowAsync(
        AutomationRule rule,
        Guid? deviceId,
        string? triggerPayloadJson,
        Guid actorUserId,
        bool simulated,
        CancellationToken cancellationToken = default)
    {
        await EnqueueAndProcessAsync(
            rule,
            rule.OrganizationId,
            deviceId,
            "Manual",
            triggerPayloadJson,
            actorUserId,
            cycleDepth: 0,
            simulated: simulated,
            cancellationToken);
    }

    private async Task EnqueueAndProcessAsync(
        AutomationRule rule,
        Guid organizationId,
        Guid? deviceId,
        string triggerType,
        string? triggerPayloadJson,
        Guid? actorUserId,
        int cycleDepth,
        bool simulated,
        CancellationToken cancellationToken)
    {
        var payload =
            string.IsNullOrWhiteSpace(
                triggerPayloadJson)
                ? "{}"
                : triggerPayloadJson!;

        var key = BuildIdempotencyKey(
            organizationId,
            rule.Id,
            triggerType,
            deviceId,
            payload,
            cycleDepth);

        var item = new AutomationQueueItem(
            organizationId,
            rule.Id,
            triggerType,
            payload,
            deviceId,
            key,
            cycleDepth: cycleDepth,
            simulated: simulated,
            actorUserId: actorUserId,
            priority: rule.Priority);

        var stored = await _queue.EnqueueAsync(
            item,
            cancellationToken);

        if (stored.Id != item.Id)
        {
            // Ya existía un item no terminal: no lo
            // reprocesamos aquí.
            return;
        }

        await _processor.ProcessAsync(
            stored.Id,
            organizationId,
            cancellationToken);
    }

    private static string BuildIdempotencyKey(
        Guid organizationId,
        Guid ruleId,
        string triggerType,
        Guid? deviceId,
        string payloadJson,
        int cycleDepth)
    {
        if (cycleDepth > 0)
        {
            // Reencolados por reevaluación: identidad
            // única para permitir el ciclo controlado.
            return
                $"reeval-{organizationId:N}-{ruleId:N}-{Guid.NewGuid():N}";
        }

        var raw =
            $"{organizationId:N}|{ruleId:N}|{triggerType}|{deviceId?.ToString("N") ?? "-"}|{payloadJson}";

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(raw));

        return "auto-" + Convert.ToHexString(hash);
    }
}
