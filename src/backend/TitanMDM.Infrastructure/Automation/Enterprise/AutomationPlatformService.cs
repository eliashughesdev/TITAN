using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Automation.Enterprise.Scheduling;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise;

/*
 * AUT-1.2 / 1.4 / 1.6 / 1.7 — Servicio de plataforma.
 *
 * Orquesta el catálogo, la validación, la simulación, la
 * creación/edición de reglas empresariales, el tablero, el
 * historial, los reintentos, las notificaciones y las
 * programaciones. Siempre con aislamiento por organización.
 */
internal sealed class AutomationPlatformService
    : IAutomationPlatformService
{
    private readonly TitanMdmDbContext _db;
    private readonly IAutomationQueueService _queue;
    private readonly IAutomationQueueProcessor _processor;
    private readonly IAutomationEventIngest _ingest;

    public AutomationPlatformService(
        TitanMdmDbContext db,
        IAutomationQueueService queue,
        IAutomationQueueProcessor processor,
        IAutomationEventIngest ingest)
    {
        _db = db;
        _queue = queue;
        _processor = processor;
        _ingest = ingest;
    }

    public Task<IReadOnlyCollection<
            AutomationEventDescriptorDto>>
        GetEventCatalogAsync(
            CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<
            AutomationEventDescriptorDto> dtos =
            AutomationEventCatalog.All
                .Select(x =>
                    new AutomationEventDescriptorDto(
                        x.Code,
                        x.Module,
                        x.Label,
                        x.Description,
                        x.Connected,
                        x.PayloadFields))
                .ToArray();

        return Task.FromResult(dtos);
    }

    public Task<IReadOnlyCollection<
            AutomationActionDescriptorDto>>
        GetActionCatalogAsync(
            CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<
            AutomationActionDescriptorDto> dtos =
            AutomationActionCatalog.All
                .Select(x =>
                    new AutomationActionDescriptorDto(
                        x.Code,
                        x.Module,
                        x.Label,
                        x.Description,
                        x.Impact,
                        x.RequiresDevice,
                        x.Enabled,
                        x.Parameters
                            .Select(p =>
                                new AutomationActionParameterDescriptorDto(
                                    p.Name,
                                    p.Label,
                                    p.Required,
                                    p.DefaultValue))
                            .ToArray()))
                .ToArray();

        return Task.FromResult(dtos);
    }

    public Task<AutomationDefinitionValidationDto>
        ValidateDefinitionAsync(
            AutomationRuleDefinition definition,
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            AutomationDefinitionValidator.Validate(
                definition));
    }

    public async Task<AutomationRuleDto>
        CreateEnterpriseRuleAsync(
            Guid organizationId,
            Guid userId,
            EnterpriseCreateRuleRequest request,
            CancellationToken cancellationToken = default)
    {
        var definition =
            request.Definition;

        EnsureValid(definition);

        var trigger = ParseTrigger(
            definition.Trigger);

        var rule = new AutomationRule(
            organizationId,
            request.Name,
            request.Description,
            trigger,
            SerializeConditions(definition),
            ResolveLegacyAction(definition),
            SerializeActionPayload(definition),
            userId);

        ApplyDefinitionJson(
            rule,
            definition,
            userId);

        if (!request.IsEnabled)
            rule.SetEnabled(false);

        _db.Set<AutomationRule>()
            .Add(rule);

        await UpsertScheduleAsync(
            rule,
            definition,
            cancellationToken);

        await _db.SaveChangesAsync(
            cancellationToken);

        return AutomationRuleMapper.Map(rule);
    }

    public async Task<IReadOnlyCollection<
            AutomationEnterpriseRuleDto>>
        GetEnterpriseRulesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        var rules = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.DefinitionJson != null)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return rules
            .Select(rule =>
            {
                var definition =
                    AutomationRuleEngine.DeserializeDefinition(rule)
                    ?? new AutomationRuleDefinition();

                return new AutomationEnterpriseRuleDto(
                    rule.Id,
                    rule.Name,
                    rule.Description,
                    rule.IsEnabled,
                    rule.Module ?? definition.Module,
                    rule.Priority,
                    rule.ConsecutiveFailures,
                    rule.ExecutionCount,
                    rule.UpdatedAtUtc,
                    rule.LastExecutedAtUtc,
                    definition);
            })
            .ToArray();
    }

    public async Task<AutomationRuleDto>
        UpdateEnterpriseRuleAsync(
            Guid organizationId,
            Guid ruleId,
            Guid userId,
            EnterpriseUpdateRuleRequest request,
            CancellationToken cancellationToken = default)
    {
        var definition =
            request.Definition;

        EnsureValid(definition);

        var rule = await _db
            .Set<AutomationRule>()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == ruleId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Automation rule not found.");

        var trigger = ParseTrigger(
            definition.Trigger);

        rule.Update(
            request.Name,
            request.Description,
            trigger,
            SerializeConditions(definition),
            ResolveLegacyAction(definition),
            SerializeActionPayload(definition),
            request.IsEnabled);

        ApplyDefinitionJson(
            rule,
            definition,
            userId);

        await UpsertScheduleAsync(
            rule,
            definition,
            cancellationToken);

        await _db.SaveChangesAsync(
            cancellationToken);

        return AutomationRuleMapper.Map(rule);
    }

    public async Task<AutomationSimulationResultDto>
        SimulateAsync(
            Guid organizationId,
            Guid userId,
            Guid ruleId,
            SimulateRuleRequest request,
            CancellationToken cancellationToken = default)
    {
        var rule = await _db
            .Set<AutomationRule>()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == ruleId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (rule is null)
        {
            return new AutomationSimulationResultDto(
                ruleId,
                false,
                true,
                Array.Empty<
                    AutomationConditionTraceDto>(),
                Array.Empty<
                    AutomationPlannedActionDto>(),
                new[] { "Rule not found." });
        }

        var payload =
            request.TriggerPayloadJson ?? "{}";

        var evaluation =
            AutomationRuleEngine.Evaluate(
                rule,
                payload);

        if (!evaluation.Passed)
        {
            return new AutomationSimulationResultDto(
                ruleId,
                false,
                true,
                evaluation.Traces,
                Array.Empty<
                    AutomationPlannedActionDto>(),
                new[] { "Conditions not met." });
        }

        var definition =
            AutomationRuleEngine
                .DeserializeDefinition(rule)
            ?? new AutomationRuleDefinition();

        var planned = definition.Actions
            .OrderBy(x => x.Order)
            .Select(action =>
            {
                var descriptor =
                    AutomationActionCatalog.Find(
                        action.Code);

                var valid =
                    descriptor?.Enabled == true;

                return new AutomationPlannedActionDto(
                    action.Code,
                    action.Order,
                    valid,
                    valid
                        ? "Ready to run (simulation only)."
                        : descriptor is null
                            ? "Unknown action."
                            : "Action not enabled in AUT-1.");
            })
            .ToArray();

        var warnings =
            new List<string>();

        if (definition.Actions.Any(x =>
            AutomationActionCatalog.Find(x.Code)
                ?.RequiresDevice == true) &&
            request.DeviceId is null)
        {
            warnings.Add(
                "Some actions require a device; provide a device id to preview them.");
        }

        return new AutomationSimulationResultDto(
            ruleId,
            true,
            true,
            evaluation.Traces,
            planned,
            warnings);
    }

    public async Task<AutomationDashboardDto>
        GetDashboardAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        var rules = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId)
            .ToListAsync(cancellationToken);

        var executions = await _db
            .Set<AutomationExecution>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.StartedAtUtc >= since)
            .ToListAsync(cancellationToken);

        var pending = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .Pending,
                cancellationToken);

        var dead = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .DeadLetter,
                cancellationToken);

        var real = executions
            .Where(x => !x.Simulated)
            .ToArray();

        var success = real.Count(x =>
            x.Status ==
                AutomationExecutionStatus.Success);

        var failed = real.Count(x =>
            x.Status ==
                AutomationExecutionStatus.Failed);

        var total = real.Length;

        var successRate = total == 0
            ? 0
            : Math.Round(
                100.0 * success / total,
                2);

        var durations = real
            .Where(x => x.DurationMs.HasValue)
            .Select(x => x.DurationMs!.Value)
            .ToArray();

        var averageDuration = durations.Length == 0
            ? 0
            : Math.Round(
                durations.Average(),
                1);

        var recent = await MapExecutionsAsync(
            organizationId,
            executions
                .OrderByDescending(
                    x => x.StartedAtUtc)
                .Take(10)
                .ToArray(),
            cancellationToken);

        var alerts =
            new List<AutomationAlertDto>();

        foreach (var rule in rules
            .Where(x => x.ConsecutiveFailures > 0)
            .OrderByDescending(
                x => x.ConsecutiveFailures)
            .Take(5))
        {
            alerts.Add(
                new AutomationAlertDto(
                    rule.ConsecutiveFailures >= 3
                        ? "critical"
                        : "warning",
                    $"Rule '{rule.Name}' has {rule.ConsecutiveFailures} consecutive failure(s).",
                    rule.Id,
                    rule.UpdatedAtUtc));
        }

        if (dead > 0)
        {
            alerts.Add(
                new AutomationAlertDto(
                    "critical",
                    $"{dead} automation item(s) in dead-letter.",
                    null,
                    DateTime.UtcNow));
        }

        return new AutomationDashboardDto(
            rules.Count,
            rules.Count(x => x.IsEnabled),
            real.Length,
            failed,
            pending,
            dead,
            successRate,
            averageDuration,
            recent,
            alerts);
    }

    public async Task<AutomationExecutionDetailDto?>
        GetExecutionDetailAsync(
            Guid organizationId,
            Guid executionId,
            CancellationToken cancellationToken = default)
    {
        var execution = await _db
            .Set<AutomationExecution>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == executionId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (execution is null)
            return null;

        var ruleName = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                x.Id == execution.AutomationRuleId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Automation";

        string? deviceName = null;

        if (execution.DeviceId.HasValue)
        {
            deviceName = await _db.Devices
                .AsNoTracking()
                .Where(x =>
                    x.Id == execution.DeviceId.Value)
                .Select(x => x.DeviceName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new AutomationExecutionDetailDto(
            execution.Id,
            execution.AutomationRuleId,
            ruleName,
            execution.DeviceId,
            deviceName,
            execution.TriggerType,
            execution.Status.ToString(),
            execution.Simulated,
            execution.RetryCount,
            execution.CycleDepth,
            execution.DurationMs,
            execution.ResultJson,
            execution.ErrorMessage,
            ParseActionRuns(execution.ResultJson),
            execution.StartedAtUtc,
            execution.CompletedAtUtc);
    }

    public async Task<bool> RetryExecutionAsync(
        Guid organizationId,
        Guid userId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await _db
            .Set<AutomationExecution>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == executionId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (execution is null)
            return false;

        var rule = await _db
            .Set<AutomationRule>()
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        execution.AutomationRuleId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (rule is null)
            return false;

        var item = new AutomationQueueItem(
            organizationId,
            rule.Id,
            execution.TriggerType,
            execution.TriggerPayloadJson,
            execution.DeviceId,
            $"retry-{execution.Id:N}-{Guid.NewGuid():N}",
            cycleDepth: 0,
            simulated: false,
            actorUserId: userId,
            priority: rule.Priority);

        var stored = await _queue.EnqueueAsync(
            item,
            cancellationToken);

        await _processor.ProcessAsync(
            stored.Id,
            organizationId,
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyCollection<
            AutomationNotificationDto>>
        GetNotificationsAsync(
            Guid organizationId,
            int limit,
            CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var notifications = await _db
            .Set<AutomationNotification>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId)
            .OrderByDescending(
                x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var ruleIds = notifications
            .Where(x => x.AutomationRuleId.HasValue)
            .Select(x =>
                x.AutomationRuleId!.Value)
            .Distinct()
            .ToArray();

        var ruleNames = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                ruleIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Name,
                cancellationToken);

        return notifications
            .Select(x =>
                new AutomationNotificationDto(
                    x.Id,
                    x.AutomationRuleId,
                    x.AutomationRuleId.HasValue
                        ? ruleNames.GetValueOrDefault(
                            x.AutomationRuleId
                                .Value)
                        : null,
                    x.DeviceId,
                    x.Title,
                    x.Message,
                    x.Severity,
                    x.Channel,
                    x.IsRead,
                    x.CreatedAtUtc,
                    x.ReadAtUtc))
            .ToArray();
    }

    public async Task<bool> MarkNotificationReadAsync(
        Guid organizationId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db
            .Set<AutomationNotification>()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == notificationId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken);

        if (notification is null)
            return false;

        notification.MarkRead();

        await _db.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    public async Task<AutomationScheduleDto?>
        GetScheduleAsync(
            Guid organizationId,
            Guid ruleId,
            CancellationToken cancellationToken = default)
    {
        var schedule = await _db
            .Set<AutomationSchedule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.AutomationRuleId == ruleId,
                cancellationToken);

        return schedule is null
            ? null
            : MapSchedule(schedule);
    }

    public async Task<AutomationScheduleDto>
        SaveScheduleAsync(
            Guid organizationId,
            Guid ruleId,
            Guid userId,
            AutomationScheduleRequest request,
            CancellationToken cancellationToken = default)
    {
        var rule = await _db
            .Set<AutomationRule>()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == ruleId &&
                    x.OrganizationId ==
                        organizationId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Automation rule not found.");

        if (!Enum.TryParse<
                AutomationScheduleRecurrence>(
                request.Recurrence,
                true,
                out var recurrence))
        {
            throw new ArgumentException(
                $"Invalid recurrence '{request.Recurrence}'.");
        }

        var schedule = await _db
            .Set<AutomationSchedule>()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.AutomationRuleId == ruleId,
                cancellationToken);

        if (schedule is null)
        {
            schedule = new AutomationSchedule(
                organizationId,
                ruleId,
                recurrence,
                request.TimeZoneId ?? "UTC");

            _db.Set<AutomationSchedule>()
                .Add(schedule);
        }

        schedule.Update(
            recurrence,
            request.IntervalMinutes,
            request.TimeOfDay,
            request.DayOfWeek,
            request.DayOfMonth,
            request.TimeZoneId ?? "UTC",
            request.StartAtUtc,
            request.CatchUp,
            request.IsEnabled);

        schedule.ScheduleNext(
            AutomationScheduleCalculator.ComputeNext(
                recurrence,
                DateTime.UtcNow,
                request.IntervalMinutes,
                request.TimeOfDay,
                request.DayOfWeek,
                request.DayOfMonth,
                request.TimeZoneId ?? "UTC",
                request.StartAtUtc));

        await _db.SaveChangesAsync(
            cancellationToken);

        return MapSchedule(schedule);
    }

    public async Task DeleteScheduleAsync(
        Guid organizationId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _db
            .Set<AutomationSchedule>()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.AutomationRuleId == ruleId,
                cancellationToken);

        if (schedule is null)
            return;

        _db.Set<AutomationSchedule>()
            .Remove(schedule);

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<AutomationQueueStatsDto>
        GetQueueStatsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        var pending = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .Pending,
                cancellationToken);

        var retried = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .Retried,
                cancellationToken);

        var running = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .Running,
                cancellationToken);

        var dead = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.State ==
                        AutomationQueueItemState
                            .DeadLetter,
                cancellationToken);

        return new AutomationQueueStatsDto(
            pending,
            retried,
            running,
            dead);
    }

    private static void EnsureValid(
        AutomationRuleDefinition? definition)
    {
        var validation =
            AutomationDefinitionValidator.Validate(
                definition);

        if (validation.IsValid)
            return;

        var message = string.Join(
            "; ",
            validation.Issues
                .Where(x => x.Severity == "error")
                .Select(x =>
                    $"{x.Path}: {x.Message}"));

        throw new InvalidOperationException(
            $"Invalid automation definition. {message}");
    }

    private static AutomationTriggerType ParseTrigger(
        string value)
    {
        if (!Enum.TryParse<
                AutomationTriggerType>(
                value,
                true,
                out var trigger))
        {
            throw new ArgumentException(
                $"Invalid automation trigger '{value}'.");
        }

        return trigger;
    }

    private static void ApplyDefinitionJson(
        AutomationRule rule,
        AutomationRuleDefinition definition,
        Guid userId)
    {
        var scopeJson =
            definition.ScopeSiteIds.Count == 0
                ? null
                : JsonSerializer.Serialize(
                    definition.ScopeSiteIds);

        rule.ApplyDefinition(
            AutomationDefinitionSerializer.Serialize(
                definition),
            ParseTrigger(definition.Trigger),
            definition.Module,
            definition.Priority,
            scopeJson,
            userId);
    }

    private static string SerializeConditions(
        AutomationRuleDefinition definition)
    {
        return AutomationDefinitionSerializer.Serialize(
            definition.Conditions);
    }

    private static string SerializeActionPayload(
        AutomationRuleDefinition definition)
    {
        var first = definition.Actions
            .OrderBy(x => x.Order)
            .FirstOrDefault();

        return first is null
            ? "{}"
            : AutomationDefinitionSerializer.Serialize(
                first.Parameters);
    }

    private static AutomationActionType
        ResolveLegacyAction(
            AutomationRuleDefinition definition)
    {
        var first = definition.Actions
            .OrderBy(x => x.Order)
            .FirstOrDefault();

        if (first is not null &&
            Enum.TryParse<
                AutomationActionType>(
                first.Code,
                true,
                out var action))
        {
            return action;
        }

        return AutomationActionType.Notification;
    }

    private async Task UpsertScheduleAsync(
        AutomationRule rule,
        AutomationRuleDefinition definition,
        CancellationToken cancellationToken)
    {
        var requested = definition.Schedule;

        var existing = await _db
            .Set<AutomationSchedule>()
            .FirstOrDefaultAsync(
                x =>
                    x.AutomationRuleId == rule.Id,
                cancellationToken);

        if (requested is null)
        {
            if (existing is not null)
            {
                _db.Set<AutomationSchedule>()
                    .Remove(existing);
            }

            return;
        }

        if (!Enum.TryParse<
                AutomationScheduleRecurrence>(
                requested.Recurrence,
                true,
                out var recurrence))
        {
            recurrence =
                AutomationScheduleRecurrence.Once;
        }

        if (existing is null)
        {
            existing = new AutomationSchedule(
                rule.OrganizationId,
                rule.Id,
                recurrence,
                requested.TimeZoneId);

            _db.Set<AutomationSchedule>()
                .Add(existing);
        }

        existing.Update(
            recurrence,
            requested.IntervalMinutes,
            requested.TimeOfDay,
            requested.DayOfWeek,
            requested.DayOfMonth,
            requested.TimeZoneId,
            requested.StartAtUtc,
            requested.CatchUp,
            true);

        existing.ScheduleNext(
            AutomationScheduleCalculator.ComputeNext(
                recurrence,
                DateTime.UtcNow,
                requested.IntervalMinutes,
                requested.TimeOfDay,
                requested.DayOfWeek,
                requested.DayOfMonth,
                requested.TimeZoneId,
                requested.StartAtUtc));
    }

    private async Task<
        IReadOnlyCollection<AutomationExecutionDto>>
        MapExecutionsAsync(
            Guid organizationId,
            IReadOnlyCollection<AutomationExecution>
                executions,
            CancellationToken cancellationToken)
    {
        if (executions.Count == 0)
        {
            return Array.Empty<
                AutomationExecutionDto>();
        }

        var ruleIds = executions
            .Select(x => x.AutomationRuleId)
            .Distinct()
            .ToArray();

        var ruleNames = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                ruleIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Name,
                cancellationToken);

        var deviceIds = executions
            .Where(x => x.DeviceId.HasValue)
            .Select(x => x.DeviceId!.Value)
            .Distinct()
            .ToArray();

        var deviceNames = await _db.Devices
            .AsNoTracking()
            .Where(x =>
                deviceIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.DeviceName,
                cancellationToken);

        return executions
            .Select(x =>
                new AutomationExecutionDto(
                    x.Id,
                    x.AutomationRuleId,
                    ruleNames.GetValueOrDefault(
                        x.AutomationRuleId,
                        "Automation"),
                    x.DeviceId,
                    x.DeviceId.HasValue
                        ? deviceNames.GetValueOrDefault(
                            x.DeviceId.Value)
                        : null,
                    x.TriggerType,
                    x.Status.ToString(),
                    x.ResultJson,
                    x.ErrorMessage,
                    x.StartedAtUtc,
                    x.CompletedAtUtc))
            .ToArray();
    }

    private static IReadOnlyCollection<
        AutomationActionRunDto>
        ParseActionRuns(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(
                resultJson))
        {
            return Array.Empty<
                AutomationActionRunDto>();
        }

        try
        {
            using var document =
                JsonDocument.Parse(resultJson);

            if (!document.RootElement
                    .TryGetProperty(
                        "actions",
                        out var actions) ||
                actions.ValueKind !=
                    JsonValueKind.Array)
            {
                return Array.Empty<
                    AutomationActionRunDto>();
            }

            var runs =
                new List<AutomationActionRunDto>();

            foreach (var action in
                actions.EnumerateArray())
            {
                runs.Add(
                    new AutomationActionRunDto(
                        GetString(
                            action,
                            "code") ??
                        string.Empty,
                        action.TryGetProperty(
                            "order",
                            out var order) &&
                        order.ValueKind ==
                            JsonValueKind.Number
                            ? order.GetInt32()
                            : 0,
                        GetString(
                            action,
                            "status") ??
                        "unknown",
                        GetString(
                            action,
                            "message"),
                        GetString(
                            action,
                            "resultJson")));
            }

            return runs;
        }
        catch (JsonException)
        {
            return Array.Empty<
                AutomationActionRunDto>();
        }
    }

    private static string? GetString(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                property,
                out var value) &&
            value.ValueKind ==
                JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static AutomationScheduleDto MapSchedule(
        AutomationSchedule schedule)
    {
        return new AutomationScheduleDto(
            schedule.Id,
            schedule.AutomationRuleId,
            schedule.Recurrence.ToString(),
            schedule.IntervalMinutes,
            schedule.TimeOfDay,
            schedule.DayOfWeek,
            schedule.DayOfMonth,
            schedule.TimeZoneId,
            schedule.StartAtUtc,
            schedule.NextRunAtUtc,
            schedule.LastRunAtUtc,
            schedule.CatchUp,
            schedule.IsEnabled);
    }
}

internal static class AutomationRuleMapper
{
    public static AutomationRuleDto Map(
        AutomationRule rule)
    {
        return new AutomationRuleDto(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.TriggerType.ToString(),
            rule.ConditionsJson,
            rule.ActionType.ToString(),
            rule.ActionPayloadJson,
            rule.IsEnabled,
            rule.ExecutionCount,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc,
            rule.LastExecutedAtUtc);
    }
}
