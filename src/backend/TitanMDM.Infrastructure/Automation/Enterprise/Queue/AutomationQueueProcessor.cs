using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Automation;
using TitanMDM.Domain.Entities;
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
    private readonly IAutomationAuthorizationService _authorization;
    private readonly IAdministrativeAuditWriter _audit;
    private readonly ILogger<AutomationQueueProcessor> _logger;

    public AutomationQueueProcessor(
        TitanMdmDbContext db,
        IAutomationActionRegistry actions,
        IAutomationAuthorizationService authorization,
        IAdministrativeAuditWriter audit,
        ILogger<AutomationQueueProcessor> logger)
    {
        _db = db;
        _actions = actions;
        _authorization = authorization;
        _audit = audit;
        _logger = logger;
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

            var saveToken =
                cancellationToken.IsCancellationRequested &&
                item.State != AutomationQueueItemState.Running
                    ? CancellationToken.None
                    : cancellationToken;

            await _db.SaveChangesAsync(
                saveToken);

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

        if (item.TriggerType.Equals(
                "ScheduledTask",
                StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteScheduledTaskAsync(
                item,
                cancellationToken);

            return;
        }

        if (!item.AutomationRuleId.HasValue)
        {
            item.Skip(
                "Automation queue item requires a rule.");

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
         * AUT-1.8 — Actor efectivo. Las ejecuciones
         * programadas/automáticas no llevan actor explícito;
         * se atribuyen a quien modificó o creó la regla. Las
         * ejecuciones manuales conservan el actor original.
         */
        var effectiveActorId =
            item.ActorUserId
            ?? rule.ModifiedByUserId
            ?? rule.CreatedByUserId;

        var actorScope =
            await _authorization.GetScopeAsync(
                item.OrganizationId,
                effectiveActorId,
                cancellationToken);

        if (!IsDefinitionWithinScope(
                definition,
                actorScope))
        {
            const string scopeDenial =
                "Actor scope does not cover the automation " +
                "rule.";

            item.Skip(scopeDenial);

            await AuditDenialAsync(
                item,
                effectiveActorId,
                scopeDenial,
                cancellationToken);

            return;
        }

        /*
         * AUT-1.8 — Tope de concurrencia por organización.
         */
        var organizationRunning =
            await _db
                .Set<AutomationExecution>()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            item.OrganizationId &&
                        x.Status ==
                            AutomationExecutionStatus
                                .Running,
                    cancellationToken);

        if (organizationRunning >=
            AutomationPolicyDefaults
                .MaxConcurrentPerOrganization)
        {
            item.Skip(
                "Per-organization concurrency limit reached.");

            return;
        }

        /*
         * Aislamiento por localidad.
         */
        if (!await IsDeviceWithinDefinitionScopeAsync(
                item.OrganizationId,
                item.DeviceId,
                definition,
                cancellationToken))
        {
            item.Skip(
                "Device is outside the rule site scope.");

            return;
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

        /*
         * AUT-1.8 — Puerta de autorización global.
         *
         * Toda ejecución real exige el permiso
         * "automation.execute" del actor efectivo. Se deniega
         * cerrado ante actor desconocido o sin permiso.
         */
        if (!item.Simulated &&
            !await HasExecutePermissionAsync(
                item.OrganizationId,
                effectiveActorId,
                cancellationToken))
        {
            const string denial =
                "Actor is not authorized to execute " +
                "automations.";

            execution.Skip(denial);
            item.Skip(denial);

            await AuditDenialAsync(
                item,
                effectiveActorId,
                denial,
                cancellationToken);

            return;
        }

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

            /*
             * AUT-1.8 — Autorización por acción. Cada acción
             * puede exigir un permiso adicional (p. ej.
             * "devices.commands") y el actor debe poder
             * operar sobre el dispositivo destino.
             */
            var denial = await AuthorizeActionAsync(
                item.OrganizationId,
                effectiveActorId,
                action,
                item.DeviceId,
                cancellationToken);

            if (denial is not null)
            {
                runs.Add(
                    new AutomationActionRunDto(
                        action.Code,
                        action.Order,
                        "denied",
                        denial,
                        null));

                failed = true;

                await AuditDenialAsync(
                    item,
                    effectiveActorId,
                    denial,
                    cancellationToken);

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
                        effectiveActorId,
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

            /*
             * AUT-1.8 — Auditoría de acciones sensibles
             * (impacto medio/alto).
             */
            if (IsSensitiveAction(action.Code))
            {
                await AuditActionAsync(
                    item,
                    effectiveActorId,
                    action.Code,
                    result.Success
                        ? "success"
                        : "failed");
            }

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

        /*
         * AUT-1.8 — Autorización de la tarea programada.
         * El actor es el que encoló la tarea; si falta, se
         * deniega cerrado.
         */
        var actorId = item.ActorUserId ?? Guid.Empty;
        var timeoutSeconds = 60;

        if (item.AutomationRuleId.HasValue)
        {
            var rule = await _db
                .Set<AutomationRule>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == item.AutomationRuleId.Value &&
                        x.OrganizationId == item.OrganizationId,
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

            timeoutSeconds = Math.Clamp(
                definition.Policies.TimeoutSeconds,
                1,
                600);

            var scope = await _authorization.GetScopeAsync(
                item.OrganizationId,
                actorId,
                cancellationToken);

            if (!IsDefinitionWithinScope(
                    definition,
                    scope))
            {
                const string scopeDenial =
                    "Actor scope does not cover the " +
                    "automation rule.";

                item.Fail(scopeDenial);

                await AuditDenialAsync(
                    item,
                    actorId,
                    scopeDenial,
                    cancellationToken);

                return;
            }

            if (!await IsDeviceWithinDefinitionScopeAsync(
                    item.OrganizationId,
                    item.DeviceId,
                    definition,
                    cancellationToken))
            {
                item.Fail(
                    "Device is outside the rule site scope.");
                return;
            }
        }

        var organizationRunning = await _db
            .Set<AutomationExecution>()
            .CountAsync(
                x =>
                    x.OrganizationId == item.OrganizationId &&
                    x.Status ==
                        AutomationExecutionStatus.Running,
                cancellationToken);

        var organizationQueueRunning = await _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId == item.OrganizationId &&
                    x.State == AutomationQueueItemState.Running,
                cancellationToken);

        if (organizationRunning >=
            AutomationPolicyDefaults
                .MaxConcurrentPerOrganization ||
            organizationQueueRunning >
                AutomationPolicyDefaults
                    .MaxConcurrentPerOrganization)
        {
            item.ScheduleRetry(
                "Per-organization concurrency limit reached.",
                DateTime.UtcNow.AddSeconds(30));
            return;
        }

        if (!await HasExecutePermissionAsync(
                item.OrganizationId,
                actorId,
                cancellationToken))
        {
            const string executeDenial =
                "Actor is not authorized to execute " +
                "automations.";

            item.Fail(executeDenial);

            await AuditDenialAsync(
                item,
                actorId,
                executeDenial,
                cancellationToken);

            return;
        }

        var scheduledAction =
            new AutomationActionDto
            {
                Code = payload.ActionCode!,
                Parameters = parameters
            };

        var denial = await AuthorizeActionAsync(
            item.OrganizationId,
            actorId,
            scheduledAction,
            item.DeviceId,
            cancellationToken);

        if (denial is not null)
        {
            item.Fail(denial);

            await AuditDenialAsync(
                item,
                actorId,
                denial,
                cancellationToken);

            return;
        }

        using var timeoutSource =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeoutSource.CancelAfter(
            TimeSpan.FromSeconds(timeoutSeconds));

        AutomationActionResult result;

        try
        {
            result = await handler.ExecuteAsync(
                new AutomationActionContext(
                    item.OrganizationId,
                    item.DeviceId,
                    actorId,
                    item.AutomationRuleId,
                    item.AutomationExecutionId,
                    scheduledAction,
                    "{}",
                    false,
                    item.CycleDepth,
                    Array.Empty<Guid>()),
                timeoutSource.Token);
        }
        catch (OperationCanceledException)
            when (
                timeoutSource.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
        {
            result = new AutomationActionResult(
                false,
                $"Action timed out after {timeoutSeconds}s.");
        }
        catch (Exception exception)
        {
            result = new AutomationActionResult(
                false,
                exception.Message);
        }

        if (IsSensitiveAction(payload.ActionCode!))
        {
            await AuditActionAsync(
                item,
                actorId,
                payload.ActionCode!,
                result.Success
                    ? "success"
                    : "failed");
        }

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

    private Task<bool> HasExecutePermissionAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return _authorization.HasPermissionAsync(
            organizationId,
            actorUserId,
            AutomationPermissions.Execute,
            cancellationToken);
    }

    /*
     * AUT-1.8 — Comprueba el permiso específico de la acción
     * y el acceso al dispositivo destino. Devuelve null si
     * está autorizada o el motivo de la denegación.
     */
    private async Task<string?> AuthorizeActionAsync(
        Guid organizationId,
        Guid actorUserId,
        AutomationActionDto action,
        Guid? deviceId,
        CancellationToken cancellationToken)
    {
        var actionCode = action.Code;

        var required =
            AutomationActionCatalog
                .RequiredPermissionFor(actionCode);

        if (required is not null &&
            !await _authorization.HasPermissionAsync(
                organizationId,
                actorUserId,
                required,
                cancellationToken))
        {
            return $"Actor lacks permission " +
                $"'{required}' for action " +
                $"'{actionCode}'.";
        }

        var descriptor =
            AutomationActionCatalog.Find(actionCode);

        if (descriptor?.RequiresDevice == true)
        {
            if (!deviceId.HasValue)
            {
                return $"Action '{actionCode}' requires a " +
                    "target device.";
            }

            if (!await _authorization.CanAccessDeviceAsync(
                    organizationId,
                    actorUserId,
                    deviceId.Value,
                    cancellationToken))
            {
                return "Actor cannot access the target " +
                    $"device for action '{actionCode}'.";
            }
        }

        if (actionCode.Equals(
                "EscalateTicket",
                StringComparison.OrdinalIgnoreCase))
        {
            action.Parameters.TryGetValue(
                "ticketId",
                out var ticketIdRaw);

            if (!Guid.TryParse(ticketIdRaw, out var ticketId))
            {
                return "EscalateTicket requires a valid " +
                    "ticketId.";
            }

            if (!await _authorization.CanAccessTicketAsync(
                    organizationId,
                    actorUserId,
                    ticketId,
                    cancellationToken))
            {
                return "Actor cannot access the target " +
                    "ticket for action 'EscalateTicket'.";
            }
        }

        return null;
    }

    private static bool IsSensitiveAction(
        string actionCode)
    {
        var impact =
            AutomationActionCatalog
                .Find(actionCode)
                ?.Impact;

        return string.Equals(
                impact,
                "medium",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                impact,
                "high",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefinitionWithinScope(
        AutomationRuleDefinition definition,
        AuthorizationScopeSnapshot scope)
    {
        if (scope.OrganizationWide)
            return true;

        if (definition.ScopeSiteIds.Count == 0)
            return false;

        var accessible = scope.SiteIds.ToHashSet();

        return definition.ScopeSiteIds
            .All(accessible.Contains);
    }

    private async Task<bool>
        IsDeviceWithinDefinitionScopeAsync(
            Guid organizationId,
            Guid? deviceId,
            AutomationRuleDefinition definition,
            CancellationToken cancellationToken)
    {
        if (definition.ScopeSiteIds.Count == 0)
            return true;

        if (!deviceId.HasValue)
            return false;

        var siteId = await _db.Devices
            .AsNoTracking()
            .Where(x =>
                x.Id == deviceId.Value &&
                x.OrganizationId == organizationId &&
                !x.IsDeleted)
            .Select(x => (Guid?)x.SiteId)
            .FirstOrDefaultAsync(cancellationToken);

        return AutomationRuleEngine.IncludesSite(
            definition,
            siteId);
    }

    private Task AuditDenialAsync(
        AutomationQueueItem item,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        return WriteAuditSafelyAsync(
            new AdministrativeAuditEntry(
                item.OrganizationId,
                actorUserId == Guid.Empty
                    ? null
                    : actorUserId,
                "automation.action.denied",
                "AutomationQueueItem",
                item.Id.ToString(),
                $"denied: {reason}",
                item.AutomationExecutionId
                        ?.ToString()
                    ?? item.Id.ToString(),
                null),
            cancellationToken);
    }

    private Task AuditActionAsync(
        AutomationQueueItem item,
        Guid actorUserId,
        string actionCode,
        string result)
    {
        return WriteAuditSafelyAsync(
            new AdministrativeAuditEntry(
                item.OrganizationId,
                actorUserId == Guid.Empty
                    ? null
                    : actorUserId,
                $"automation.action.{actionCode}",
                "AutomationQueueItem",
                item.Id.ToString(),
                result,
                item.AutomationExecutionId
                        ?.ToString()
                    ?? item.Id.ToString(),
                null),
            CancellationToken.None);
    }

    private async Task WriteAuditSafelyAsync(
        AdministrativeAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            await _audit.WriteAsync(
                entry,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            foreach (var trackedAudit in _db.ChangeTracker
                .Entries<AdministrativeAuditEvent>()
                .Where(x =>
                    x.State == EntityState.Added))
            {
                trackedAudit.State = EntityState.Detached;
            }

            // An audit storage outage must not replay an action
            // that may already have committed an external effect.
            _logger.LogWarning(
                exception,
                "Automation audit write failed for {Action} " +
                "and queue item {QueueItemId}.",
                entry.Action,
                entry.TargetId);
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
