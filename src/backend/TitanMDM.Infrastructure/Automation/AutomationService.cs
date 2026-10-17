using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation;

public sealed class AutomationService
    : IAutomationService
{
    private readonly TitanMdmDbContext _db;
    private readonly IDeviceCommandService _commands;
    private readonly IScopeAccessService _scopeAccess;

    /*
     * AUT-1 — Motor empresarial opcional.
     *
     * Se resuelve de forma tolerante (GetService) para no
     * romper el arranque si AddTitanAutomationPlatform no se
     * ha registrado todavía (integración pendiente en Program.cs).
     * Las reglas legacy siguen ejecutándose en línea.
     */
    private readonly IAutomationEventIngest? _ingest;

    public AutomationService(
        TitanMdmDbContext db,
        IDeviceCommandService commands,
        IScopeAccessService scopeAccess,
        IServiceProvider services)
    {
        _db = db;
        _commands = commands;
        _scopeAccess = scopeAccess;
        _ingest = services
            .GetService<IAutomationEventIngest>();
    }

    public async Task<
        IReadOnlyCollection<AutomationRuleDto>>
        GetRulesAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var rules =
            await _db.Set<AutomationRule>()
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                    organizationId)
                .OrderBy(x => x.Name)
                .ToListAsync(
                    cancellationToken);

        var scope = await _scopeAccess
            .GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        return rules
            .Where(rule =>
                IsRuleWithinScope(rule, scope))
            .Select(MapRule)
            .ToArray();
    }

    public async Task<AutomationRuleDto?>
        GetRuleAsync(
            Guid organizationId,
            Guid userId,
            Guid ruleId,
            CancellationToken cancellationToken = default)
    {
        var rule =
            await _db.Set<AutomationRule>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == ruleId &&
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (rule is null)
            return null;

        var scope = await _scopeAccess
            .GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        return !IsRuleWithinScope(rule, scope)
            ? null
            : MapRule(rule);
    }

    public async Task<AutomationRuleDto>
        CreateAsync(
            Guid organizationId,
            Guid userId,
            CreateAutomationRuleRequest request,
            CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationScopeAsync(
            organizationId,
            userId,
            cancellationToken);

        var trigger =
            ParseTrigger(
                request.TriggerType);

        var action =
            ParseAction(
                request.ActionType);

        var rule =
            new AutomationRule(
                organizationId,
                request.Name,
                request.Description,
                trigger,
                NormalizeJson(
                    request.ConditionsJson),
                action,
                NormalizeJson(
                    request.ActionPayloadJson),
                userId);

        _db.Set<AutomationRule>()
            .Add(rule);

        await _db.SaveChangesAsync(
            cancellationToken);

        return MapRule(rule);
    }

    public async Task<AutomationRuleDto>
        UpdateAsync(
            Guid organizationId,
            Guid userId,
            Guid ruleId,
            UpdateAutomationRuleRequest request,
            CancellationToken cancellationToken = default)
    {
        var rule =
            await FindAccessibleRuleAsync(
                organizationId,
                userId,
                ruleId,
                cancellationToken);

        rule.Update(
            request.Name,
            request.Description,
            ParseTrigger(
                request.TriggerType),
            NormalizeJson(
                request.ConditionsJson),
            ParseAction(
                request.ActionType),
            NormalizeJson(
                request.ActionPayloadJson),
            request.IsEnabled);

        await _db.SaveChangesAsync(
            cancellationToken);

        return MapRule(rule);
    }

    public async Task SetEnabledAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var rule =
            await FindAccessibleRuleAsync(
                organizationId,
                userId,
                ruleId,
                cancellationToken);

        rule.SetEnabled(enabled);

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var rule =
            await FindAccessibleRuleAsync(
                organizationId,
                userId,
                ruleId,
                cancellationToken);

        _db.Set<AutomationRule>()
            .Remove(rule);

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<
        IReadOnlyCollection<AutomationExecutionDto>>
        GetExecutionsAsync(
            Guid organizationId,
            Guid userId,
            int limit,
            CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(
            limit,
            1,
            500);

        var rules = await _db
            .Set<AutomationRule>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var scope = await _scopeAccess
            .GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        var accessibleRuleIds = rules
            .Where(rule =>
                IsRuleWithinScope(rule, scope))
            .Select(x => x.Id)
            .ToArray();

        var executions =
            await _db
                .Set<AutomationExecution>()
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId &&
                    accessibleRuleIds.Contains(
                        x.AutomationRuleId))
                .OrderByDescending(
                    x => x.StartedAtUtc)
                .Take(limit)
                .ToListAsync(
                    cancellationToken);

        var ruleIds =
            executions
                .Select(x =>
                    x.AutomationRuleId)
                .Distinct()
                .ToArray();

        var ruleNames =
            await _db
                .Set<AutomationRule>()
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    ruleIds.Contains(x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.Name,
                    cancellationToken);

        var deviceIds =
            executions
                .Where(x =>
                    x.DeviceId.HasValue)
                .Select(x =>
                    x.DeviceId!.Value)
                .Distinct()
                .ToArray();

        var deviceNames =
            await _db.Devices
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
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

    public async Task ExecuteRuleAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        ExecuteAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule =
            await FindAccessibleRuleAsync(
                organizationId,
                userId,
                ruleId,
                cancellationToken);

        if (rule.HasDefinition)
        {
            if (_ingest is null)
            {
                throw new AutomationPlatformUnavailableException(
                    "The enterprise automation platform is not " +
                    "registered. Call AddTitanAutomationPlatform " +
                    "during startup.");
            }

            await _ingest.ExecuteRuleNowAsync(
                rule,
                request.DeviceId,
                NormalizeJson(
                    request.TriggerPayloadJson),
                userId,
                simulated: false,
                cancellationToken);

            return;
        }

        await ExecuteInternalAsync(
            rule,
            request.DeviceId,
            "Manual",
            NormalizeJson(
                request.TriggerPayloadJson),
            userId,
            cancellationToken);
    }

    public async Task ProcessEventAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<
                AutomationTriggerType>(
                automationEvent.TriggerType,
                true,
                out var trigger))
        {
            return;
        }

        var rules =
            await _db.Set<AutomationRule>()
                .Where(x =>
                    x.OrganizationId ==
                        automationEvent.OrganizationId &&
                    x.IsEnabled &&
                    x.TriggerType == trigger)
                .ToListAsync(
                    cancellationToken);

        /*
         * Reglas empresariales (con definición): pasan por la
         * cola persistente, con reintentos, idempotencia y
         * anti-ciclos. Si el motor no está registrado, no se
         * procesan (integración pendiente documentada).
         */
        if (_ingest is not null)
        {
            var enterpriseRules = rules
                .Where(x => x.HasDefinition)
                .ToArray();

            if (enterpriseRules.Length > 0)
            {
                await _ingest.IngestForRulesAsync(
                    automationEvent,
                    enterpriseRules,
                    cancellationToken);
            }
        }

        /*
         * Reglas legacy: se mantiene la ejecución en línea.
         */
        foreach (var rule in rules.Where(
            x => !x.HasDefinition))
        {
            if (!ConditionsMatch(
                    rule.ConditionsJson,
                    automationEvent
                        .TriggerPayloadJson))
            {
                continue;
            }

            await ExecuteInternalAsync(
                rule,
                automationEvent.DeviceId,
                automationEvent.TriggerType,
                automationEvent.TriggerPayloadJson,
                automationEvent.ActorUserId,
                cancellationToken);
        }
    }

    private async Task ExecuteInternalAsync(
        AutomationRule rule,
        Guid? deviceId,
        string triggerType,
        string triggerPayloadJson,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var execution =
            new AutomationExecution(
                rule.OrganizationId,
                rule.Id,
                deviceId,
                triggerType,
                triggerPayloadJson);

        _db.Set<AutomationExecution>()
            .Add(execution);

        try
        {
            if (!rule.IsEnabled)
            {
                execution.Skip(
                    "Automation rule is disabled.");

                await _db.SaveChangesAsync(
                    cancellationToken);

                return;
            }

            var result =
                await ExecuteActionAsync(
                    rule,
                    deviceId,
                    userId,
                    cancellationToken);

            execution.Complete(result);
            rule.RegisterExecution();
        }
        catch (Exception ex)
        {
            execution.Fail(ex.Message);
        }

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    private async Task<string>
        ExecuteActionAsync(
            AutomationRule rule,
            Guid? deviceId,
            Guid userId,
            CancellationToken cancellationToken)
    {
        switch (rule.ActionType)
        {
            case AutomationActionType
                .RequestLocation:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "LOCATION_REQUEST",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .SecurityScan:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "SECURITY_STATUS",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .ComplianceScan:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "COMPLIANCE_CHECK",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .AppInventory:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "APP_INVENTORY",
                    "{}",
                    cancellationToken);

            case AutomationActionType
    .WindowsInventory:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "DEVICE_INVENTORY",
                    "{}",
                    cancellationToken);

            case AutomationActionType
    .WindowsUpdateScan:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "WINDOWS_UPDATE_SCAN",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .WindowsUpdateInstall:
                {
                    var payload =
                        DeserializeActionPayload(
                            rule.ActionPayloadJson);

                    return await QueueCommandAsync(
                        rule,
                        deviceId,
                        userId,
                        "WINDOWS_UPDATE_INSTALL",
                        payload.PayloadJson
                        ??
                        """
        {
          "kbArticleIds": [],
          "acceptEula": true,
          "downloadOnly": false
        }
        """,
                        cancellationToken);
                }

            case AutomationActionType
    .RestartDevice:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "RESTART_DEVICE",
                    "{}",
                    cancellationToken);

            case AutomationActionType
    .LockDevice:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "LOCK_DEVICE",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .SendCommand:
                {
                    var payload =
                        DeserializeActionPayload(
                            rule.ActionPayloadJson);

                    if (string.IsNullOrWhiteSpace(
                            payload.CommandType))
                    {
                        throw new InvalidOperationException(
                            "SEND_COMMAND requires commandType.");
                    }

                    return await QueueCommandAsync(
                        rule,
                        deviceId,
                        userId,
                        payload.CommandType,
                        payload.PayloadJson ??
                            "{}",
                        cancellationToken);
                }

            case AutomationActionType
                .EnableLostMode:
                {
                    var payload =
                        DeserializeActionPayload(
                            rule.ActionPayloadJson);

                    return await QueueCommandAsync(
                        rule,
                        deviceId,
                        userId,
                        "LOST_MODE_ENABLE",
                        JsonSerializer.Serialize(
                            new
                            {
                                message =
                                    payload.Message ??
                                    "Dispositivo administrado por TitanMDM.",

                                phoneNumber =
                                    payload.PhoneNumber
                            }),
                        cancellationToken);
                }

            case AutomationActionType
                .DisableLostMode:

                return await QueueCommandAsync(
                    rule,
                    deviceId,
                    userId,
                    "LOST_MODE_DISABLE",
                    "{}",
                    cancellationToken);

            case AutomationActionType
                .Notification:

                return JsonSerializer.Serialize(
                    new
                    {
                        accepted = true,
                        type = "NOTIFICATION",
                        payload =
                            rule.ActionPayloadJson
                    });

            default:
                throw new InvalidOperationException(
                    $"Unsupported automation action: {rule.ActionType}.");
        }
    }

    private async Task<string>
        QueueCommandAsync(
            AutomationRule rule,
            Guid? deviceId,
            Guid userId,
            string commandType,
            string payloadJson,
            CancellationToken cancellationToken)
    {
        if (!deviceId.HasValue ||
            deviceId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{rule.ActionType} requires a device.");
        }

        var command =
            await _commands.CreateAsync(
                rule.OrganizationId,
                userId,
                new CreateDeviceCommandRequest(
                    deviceId.Value,
                    commandType,
                    payloadJson,
                    60),
                cancellationToken);

        return JsonSerializer.Serialize(
            new
            {
                commandId = command.Id,
                commandType =
                    command.CommandType,
                status = command.Status
            });
    }

    private static bool ConditionsMatch(
        string conditionsJson,
        string triggerPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(
                conditionsJson) ||
            conditionsJson.Trim() == "{}")
        {
            return true;
        }

        try
        {
            using var conditions =
                JsonDocument.Parse(
                    conditionsJson);

            using var payload =
                JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(
                        triggerPayloadJson)
                        ? "{}"
                        : triggerPayloadJson);

            foreach (
                var condition in
                conditions.RootElement
                    .EnumerateObject())
            {
                if (!payload.RootElement
                        .TryGetProperty(
                            condition.Name,
                            out var actual))
                {
                    return false;
                }

                if (
                    actual.ToString()
                        .Equals(
                            condition.Value
                                .ToString(),
                            StringComparison
                                .OrdinalIgnoreCase)
                    == false)
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<AutomationRule>
        FindRuleAsync(
            Guid organizationId,
            Guid ruleId,
            CancellationToken cancellationToken)
    {
        return await _db
            .Set<AutomationRule>()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == ruleId &&
                    x.OrganizationId ==
                    organizationId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Automation rule not found.");
    }

    private async Task<AutomationRule>
        FindAccessibleRuleAsync(
            Guid organizationId,
            Guid userId,
            Guid ruleId,
            CancellationToken cancellationToken)
    {
        var rule = await FindRuleAsync(
            organizationId,
            ruleId,
            cancellationToken);

        var scope = await _scopeAccess
            .GetScopeSnapshotAsync(
                organizationId,
                userId,
                cancellationToken);

        if (!IsRuleWithinScope(rule, scope))
        {
            throw new AutomationAuthorizationException(
                "El actor no tiene alcance para operar sobre " +
                "esta regla de automatización.");
        }

        return rule;
    }

    private async Task EnsureOrganizationScopeAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var allowed = await _scopeAccess
            .HasOrganizationScopeAsync(
                organizationId,
                userId,
                cancellationToken);

        if (!allowed)
        {
            throw new AutomationAuthorizationException(
                "El actor requiere alcance de organización " +
                "para crear una regla legacy.");
        }
    }

    private static bool IsRuleWithinScope(
        AutomationRule rule,
        AuthorizationScopeSnapshot scope)
    {
        if (scope.OrganizationWide)
            return true;

        var definition =
            AutomationRuleEngine.DeserializeDefinition(rule);

        if (definition is null ||
            definition.ScopeSiteIds.Count == 0)
        {
            return false;
        }

        var accessible = scope.SiteIds.ToHashSet();

        return definition.ScopeSiteIds
            .All(accessible.Contains);
    }

    private static AutomationRuleDto MapRule(
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

    private static AutomationTriggerType
        ParseTrigger(string value)
    {
        if (!Enum.TryParse<
                AutomationTriggerType>(
                value,
                true,
                out var result))
        {
            throw new ArgumentException(
                $"Invalid automation trigger '{value}'.");
        }

        return result;
    }

    private static AutomationActionType
        ParseAction(string value)
    {
        if (!Enum.TryParse<
                AutomationActionType>(
                value,
                true,
                out var result))
        {
            throw new ArgumentException(
                $"Invalid automation action '{value}'.");
        }

        return result;
    }

    private static string NormalizeJson(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "{}";

        using var document =
            JsonDocument.Parse(value);

        return document.RootElement
            .GetRawText();
    }

    private static ActionPayload
        DeserializeActionPayload(
            string json)
    {
        return JsonSerializer.Deserialize<
                   ActionPayload>(
                   json,
                   JsonOptions)
               ?? new ActionPayload();
    }

    private static readonly
        JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };

    private sealed class ActionPayload
    {
        public string? CommandType { get; set; }
        public string? PayloadJson { get; set; }
        public string? Message { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
