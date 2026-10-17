using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Queue;

internal interface IAutomationQueueProcessor
{
    Task<bool> ProcessAsync(
        Guid itemId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

/*
 * AUT-1.5 — Procesador de la cola.
 *
 * Ejecuta un item aplicando políticas de errores,
 * reintentos, concurrencia, timeout, anti-ciclos y
 * aislamiento por organización.
 */
internal sealed class AutomationQueueProcessor
    : IAutomationQueueProcessor
{
    private static readonly TimeSpan Lease =
        TimeSpan.FromMinutes(2);

    private readonly TitanMdmDbContext _db;
    private readonly IAutomationActionRegistry _actions;

    public AutomationQueueProcessor(
        TitanMdmDbContext db,
        IAutomationActionRegistry actions)
    {
        _db = db;
        _actions = actions;
    }

    public async Task<bool> ProcessAsync(
        Guid itemId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var item =
            await _db
                .Set<AutomationQueueItem>()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == itemId &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (item is null)
            return false;

        if (IsTerminal(item.State))
            return true;

        var now = DateTime.UtcNow;

        if (item.State ==
            AutomationQueueItemState.Pending)
        {
            if (!item.TryAcquire(now, Lease))
                return true;
        }

        if (item.State !=
            AutomationQueueItemState.Running)
        {
            // Fue reclamado por otro procesador o está
            // esperando reintento; no hacemos nada.
            return true;
        }

        try
        {
            await ExecuteItemAsync(
                item,
                cancellationToken);

            await _db.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleUnexpectedAsync(
                item,
                exception,
                cancellationToken);

            return false;
        }
    }

    private async Task ExecuteItemAsync(
        AutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        /*
         * Anti-ciclos: si la profundidad supera el máximo,
         * se corta el ciclo sin ejecutar.
         */
        if (item.CycleDepth >
            AutomationPolicyDefaults.MaxCycleDepth)
        {
            item.Skip(
                "Automation cycle depth limit reached.");

            return;
        }

        if (!item.AutomationRuleId.HasValue)
        {
            await ExecuteScheduledTaskAsync(
                item,
                cancellationToken);

            return;
        }

        var rule =
            await _db
                .Set<AutomationRule>()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            item.AutomationRuleId
                                .Value &&
                        x.OrganizationId ==
                            item.OrganizationId,
                    cancellationToken);

        if (rule is null)
        {
            item.Skip(
                "Automation rule no longer exists.");

            return;
        }

        if (!rule.IsEnabled)
        {
            item.Skip(
                "Automation rule is disabled.");

            return;
        }

        var definition =
            AutomationRuleEngine
                .DeserializeDefinition(rule)
            ?? new AutomationRuleDefinition();

        /*
         * Aislamiento por localidad.
         */
        if (definition.ScopeSiteIds.Count > 0)
        {
            Guid? siteId = null;

            if (item.DeviceId.HasValue)
            {
                siteId = await _db.Devices
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                            item.DeviceId.Value)
                    .Select(x => (Guid?)x.SiteId)
                    .FirstOrDefaultAsync(
                        cancellationToken);
            }

            if (!AutomationRuleEngine
                    .IncludesSite(
                        definition,
                        siteId))
            {
                item.Skip(
                    "Device is outside the rule site scope.");

                return;
            }
        }

        /*
         * Límite de ejecuciones por minuto por regla.
         */
        var oneMinuteAgo =
            DateTime.UtcNow.AddMinutes(-1);

        var recentExecutions =
            await _db
                .Set<AutomationExecution>()
                .CountAsync(
                    x =>
                        x.AutomationRuleId ==
                            rule.Id &&
                        x.StartedAtUtc >=
                            oneMinuteAgo,
                    cancellationToken);

        if (recentExecutions >=
            AutomationPolicyDefaults
                .MaxExecutionsPerMinutePerRule)
        {
            item.Skip(
                "Per-rule execution rate limit reached.");

            return;
        }

        /*
         * Control de concurrencia.
         */
        var concurrencyPolicy =
            definition.Policies
                .ConcurrencyPolicy;

        if (concurrencyPolicy.Equals(
                "SkipIfRunning",
                StringComparison.OrdinalIgnoreCase))
        {
            var running =
                await _db
                    .Set<AutomationExecution>()
                    .AnyAsync(
                        x =>
                            x.AutomationRuleId ==
                                rule.Id &&
                            x.Status ==
                                AutomationExecutionStatus
                                    .Running,
                        cancellationToken);

            if (running)
            {
                item.Skip(
                    "A concurrent execution is already running.");

                return;
            }
        }

        if (definition.Policies
                .MinSecondsBetweenExecutions > 0)
        {
            var window =
                DateTime.UtcNow.AddSeconds(
                    -definition.Policies
                        .MinSecondsBetweenExecutions);

            var tooSoon =
                await _db
                    .Set<AutomationExecution>()
                    .AnyAsync(
                        x =>
                            x.AutomationRuleId ==
                                rule.Id &&
                            x.StartedAtUtc >
                                window,
                        cancellationToken);

            if (tooSoon)
            {
                item.Skip(
                    "Minimum interval between executions not elapsed.");

                return;
            }
        }

        var execution =
            new AutomationExecution(
                item.OrganizationId,
                rule.Id,
                item.DeviceId,
                item.TriggerType,
                item.TriggerPayloadJson);

        execution.Configure(
            item.CycleDepth,
            item.ActorUserId,
            item.Simulated);

        execution.SetRetryCount(
            Math.Max(0, item.Attempts - 1));

        _db.Set<AutomationExecution>()
            .Add(execution);

        item.AttachExecution(execution.Id);

        /*
         * Evaluación de condiciones.
         *
         * La ejecución manual explícita (trigger "Manual")
         * omite las condiciones, igual que el motor legacy.
         */
        var skipConditions =
            item.TriggerType.Equals(
                "Manual",
                StringComparison.OrdinalIgnoreCase);

        var evaluation = skipConditions
            ? new AutomationConditionEvaluation(
                true,
                Array.Empty<
                    AutomationConditionTraceDto>())
            : AutomationRuleEngine.Evaluate(
                rule,
                item.TriggerPayloadJson);

        if (!evaluation.Passed)
        {
            execution.Skip(
                AutomationDefinitionSerializer
                    .Serialize(
                        new
                        {
                            matched = false,
                            traces =
                                evaluation.Traces
                        }));

            item.Complete();
            return;
        }

        var actions = ResolveActions(
            definition,
            item);

        if (actions.Count == 0)
        {
            execution.Complete(
                AutomationDefinitionSerializer
                    .Serialize(
                        new
                        {
                            matched = true,
                            actions =
                                Array.Empty<object>()
                        }));

            rule.RegisterExecution();
            rule.ResetFailures();
            item.Complete();
            return;
        }

        /*
         * Simulación: no se ejecutan efectos reales.
         */
        if (item.Simulated)
        {
            var planned =
                new List<AutomationActionRunDto>();

            foreach (var action in actions)
            {
                var handler =
                    _actions.Resolve(action.Code);

                var valid = handler is not null &&
                    AutomationActionCatalog
                        .IsEnabled(action.Code);

                planned.Add(
                    new AutomationActionRunDto(
                        action.Code,
                        action.Order,
                        valid
                            ? "planned"
                            : "invalid",
                        valid
                            ? "Ready to run."
                            : handler is null
                                ? "Unknown action."
                                : "Action disabled in AUT-1.",
                        null));
            }

            execution.Complete(
                AutomationDefinitionSerializer
                    .Serialize(
                        new
                        {
                            matched = true,
                            simulated = true,
                            traces =
                                evaluation.Traces,
                            actions = planned
                        }));

            item.Complete();
            return;
        }

        /*
         * Ejecución real con timeout global.
         */
        using var timeoutSource =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        var timeoutSeconds = Math.Clamp(
            definition.Policies.TimeoutSeconds,
            1,
            600);

        timeoutSource.CancelAfter(
            TimeSpan.FromSeconds(
                timeoutSeconds));

        var runs =
            new List<AutomationActionRunDto>();

        var failed = false;
        var stop = false;

        foreach (var action in actions)
        {
            if (stop)
                break;

            var handler =
                _actions.Resolve(action.Code);

            if (handler is null)
            {
                runs.Add(
                    new AutomationActionRunDto(
                        action.Code,
                        action.Order,
                        "failed",
                        "Unknown action.",
                        null));

                failed = true;

                if (!action.ContinueOnError &&
                    !ContinueAfterFailure(
                        definition.Policies
                            .ErrorPolicy))
                {
                    stop = true;
                }

                continue;
            }

            var stopwatch =
                Stopwatch.StartNew();

            AutomationActionResult result;

            try
            {
                result = await handler.ExecuteAsync(
                    new AutomationActionContext(
                        item.OrganizationId,
                        item.DeviceId,
                        item.ActorUserId ??
                            Guid.Empty,
                        rule.Id,
                        execution.Id,
                        action,
                        item.TriggerPayloadJson,
                        false,
                        item.CycleDepth,
                        definition.ScopeSiteIds),
                    timeoutSource.Token);
            }
            catch (OperationCanceledException)
                when (
                    timeoutSource
                        .IsCancellationRequested &&
                    !cancellationToken
                        .IsCancellationRequested)
            {
                runs.Add(
                    new AutomationActionRunDto(
                        action.Code,
                        action.Order,
                        "timeout",
                        $"Action timed out after {timeoutSeconds}s.",
                        null));

                failed = true;
                stop = true;
                break;
            }
            catch (Exception exception)
            {
                result = new AutomationActionResult(
                    false,
                    exception.Message,
                    null);
            }

            stopwatch.Stop();

            runs.Add(
                new AutomationActionRunDto(
                    action.Code,
                    action.Order,
                    result.Success
                        ? "success"
                        : "failed",
                    result.Message,
                    result.ResultJson));

            if (!result.Success)
            {
                failed = true;

                if (!action.ContinueOnError &&
                    !ContinueAfterFailure(
                        definition.Policies
                            .ErrorPolicy))
                {
                    stop = true;
                }
            }
        }

        var resultJson =
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    matched = true,
                    actions = runs
                });

        if (!failed)
        {
            execution.Complete(resultJson);
            rule.RegisterExecution();
            rule.ResetFailures();
            item.Complete();
        }
        else
        {
            var errorMessage =
                runs.LastOrDefault(
                    x => x.Status != "success")
                    ?.Message ??
                "One or more actions failed.";

            execution.Fail(errorMessage);
            rule.RegisterFailure();

            ApplyFailurePolicy(rule, definition);

            ScheduleRetryOrDeadLetter(
                item,
                definition,
                errorMessage);
        }
    }

    private async Task ExecuteScheduledTaskAsync(
        AutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        var payload =
            AutomationDefinitionSerializer
                .Deserialize<
                    ScheduledTaskPayload>(
                    item.TriggerPayloadJson);

        if (payload is null ||
            string.IsNullOrWhiteSpace(
                payload.ActionCode))
        {
            item.Skip(
                "Scheduled task payload is invalid.");
            return;
        }

        if (item.Simulated)
        {
            item.Complete();
            return;
        }

        var handler = _actions.Resolve(
            payload.ActionCode!);

        var parameters =
            AutomationDefinitionSerializer
                .Deserialize<
                    Dictionary<string, string?>>(
                    payload.ActionParamsJson)
            ?? new Dictionary<string, string?>();

        if (handler is null)
        {
            item.Fail(
                $"Scheduled action '{payload.ActionCode}' is unknown.");
            return;
        }

        var result = await handler.ExecuteAsync(
            new AutomationActionContext(
                item.OrganizationId,
                item.DeviceId,
                item.ActorUserId ?? Guid.Empty,
                item.AutomationRuleId,
                item.AutomationExecutionId,
                new AutomationActionDto
                {
                    Code = payload.ActionCode!,
                    Parameters = parameters
                },
                "{}",
                false,
                item.CycleDepth,
                Array.Empty<Guid>()),
            cancellationToken);

        if (result.Success)
        {
            item.Complete();
        }
        else
        {
            item.Fail(
                result.Message ??
                "Scheduled action failed.");
        }
    }

    private static List<AutomationActionDto>
        ResolveActions(
            AutomationRuleDefinition definition,
            AutomationQueueItem item)
    {
        if (item.TriggerType.Equals(
                "ScheduledTask",
                StringComparison.OrdinalIgnoreCase))
        {
            return new List<AutomationActionDto>();
        }

        return definition.Actions
            .OrderBy(x => x.Order)
            .Take(
                AutomationPolicyDefaults
                    .MaxActionsPerRule)
            .ToList();
    }

    private static void ApplyFailurePolicy(
        AutomationRule rule,
        AutomationRuleDefinition definition)
    {
        var policy =
            definition.Policies.ErrorPolicy;

        if (!policy.Equals(
                "DisableRuleAfterFailures",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var threshold = Math.Max(
            1,
            definition.Policies
                .DisableAfterFailures);

        if (rule.ConsecutiveFailures >= threshold)
            rule.SetEnabled(false);
    }

    private static bool ContinueAfterFailure(
        string? errorPolicy)
    {
        return string.Equals(
            errorPolicy,
            "ContinueActions",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ScheduleRetryOrDeadLetter(
        AutomationQueueItem item,
        AutomationRuleDefinition definition,
        string error)
    {
        var backoffSeconds = Math.Max(
            0,
            definition.Policies
                .RetryBackoffSeconds);

        var exponent = Math.Max(
            0,
            item.Attempts - 1);

        var delay = TimeSpan.FromSeconds(
            backoffSeconds *
            Math.Pow(2, exponent));

        item.ScheduleRetry(
            error,
            DateTime.UtcNow + delay);
    }

    private async Task HandleUnexpectedAsync(
        AutomationQueueItem item,
        Exception exception,
        CancellationToken cancellationToken)
    {
        item.ScheduleRetry(
            exception.Message,
            DateTime.UtcNow.AddSeconds(60));

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    private static bool IsTerminal(
        AutomationQueueItemState state)
    {
        return state ==
                AutomationQueueItemState.Completed ||
            state ==
                AutomationQueueItemState.DeadLetter ||
            state ==
                AutomationQueueItemState.Failed ||
            state ==
                AutomationQueueItemState.Skipped;
    }

    private sealed class ScheduledTaskPayload
    {
        public string? ActionCode { get; set; }

        public string? ActionParamsJson { get; set; }
    }
}
