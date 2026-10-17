using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Actions;

internal static class ActionParams
{
    public static string? Get(
        AutomationActionDto action,
        string name)
    {
        return action.Parameters.TryGetValue(
            name,
            out var value)
            ? value
            : null;
    }

    public static int GetInt(
        AutomationActionDto action,
        string name,
        int fallback)
    {
        return int.TryParse(
            Get(action, name),
            out var value)
            ? value
            : fallback;
    }

    public static bool GetBool(
        AutomationActionDto action,
        string name,
        bool fallback)
    {
        return bool.TryParse(
            Get(action, name),
            out var value)
            ? value
            : fallback;
    }
}

internal sealed class NotificationActionHandler
    : IAutomationActionHandler
{
    private readonly TitanMdmDbContext _db;

    public NotificationActionHandler(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    public string Code => "CreateNotification";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var title =
            ActionParams.Get(
                context.Action,
                "title");

        var message =
            ActionParams.Get(
                context.Action,
                "message");

        if (string.IsNullOrWhiteSpace(title))
        {
            return new AutomationActionResult(
                false,
                "CreateNotification requires 'title'.");
        }

        var severity =
            NormalizeSeverity(
                ActionParams.Get(
                    context.Action,
                    "severity"));

        Guid? targetUserId = null;
        var targetUserIdRaw =
            ActionParams.Get(
                context.Action,
                "targetUserId");

        if (Guid.TryParse(
                targetUserIdRaw,
                out var parsedUser))
        {
            targetUserId = parsedUser;
        }

        var notification =
            new AutomationNotification(
                context.OrganizationId,
                title,
                message ?? string.Empty,
                severity,
                "in_app",
                context.AutomationRuleId,
                context.AutomationExecutionId,
                context.DeviceId,
                targetUserId,
                ActionParams.Get(
                    context.Action,
                    "targetRole"));

        _db.Set<AutomationNotification>()
            .Add(notification);

        await _db.SaveChangesAsync(
            cancellationToken);

        return new AutomationActionResult(
            true,
            "Notification created.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    notificationId = notification.Id,
                    channel = notification.Channel,
                    severity
                }));
    }

    private static string NormalizeSeverity(
        string? value)
    {
        var normalized =
            (value ?? "info")
                .Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "warning" => "warning",
            "critical" => "critical",
            _ => "info"
        };
    }
}

internal sealed class SendAvisoActionHandler
    : IAutomationActionHandler
{
    private readonly TitanMdmDbContext _db;

    public SendAvisoActionHandler(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    public string Code => "SendAviso";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var message =
            ActionParams.Get(
                context.Action,
                "message");

        if (string.IsNullOrWhiteSpace(message))
        {
            return new AutomationActionResult(
                false,
                "SendAviso requires 'message'.");
        }

        var channel =
            (ActionParams.Get(
                 context.Action,
                 "channel") ??
             "in_app")
            .Trim()
            .ToLowerInvariant();

        if (channel != "email")
            channel = "in_app";

        var notification =
            new AutomationNotification(
                context.OrganizationId,
                "Aviso de automatización",
                message!,
                ActionParams.Get(
                    context.Action,
                    "severity") ??
                "info",
                channel,
                context.AutomationRuleId,
                context.AutomationExecutionId,
                context.DeviceId);

        _db.Set<AutomationNotification>()
            .Add(notification);

        await _db.SaveChangesAsync(
            cancellationToken);

        return new AutomationActionResult(
            true,
            $"Aviso registrado en canal '{channel}'.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    notificationId = notification.Id,
                    channel
                }));
    }
}

internal sealed class RegisterAlertActionHandler
    : IAutomationActionHandler
{
    private readonly TitanMdmDbContext _db;
    private readonly IAdministrativeAuditWriter _audit;

    public RegisterAlertActionHandler(
        TitanMdmDbContext db,
        IAdministrativeAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    public string Code => "RegisterAlert";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var message =
            ActionParams.Get(
                context.Action,
                "message");

        if (string.IsNullOrWhiteSpace(message))
        {
            return new AutomationActionResult(
                false,
                "RegisterAlert requires 'message'.");
        }

        var severity =
            ActionParams.Get(
                context.Action,
                "severity") ??
            "warning";

        var notification =
            new AutomationNotification(
                context.OrganizationId,
                "Alerta de automatización",
                message!,
                severity,
                "in_app",
                context.AutomationRuleId,
                context.AutomationExecutionId,
                context.DeviceId);

        _db.Set<AutomationNotification>()
            .Add(notification);

        await _audit.WriteAsync(
            new AdministrativeAuditEntry(
                context.OrganizationId,
                context.ActorUserId == Guid.Empty
                    ? null
                    : context.ActorUserId,
                "Automation.Alert",
                context.DeviceId.HasValue
                    ? "Device"
                    : "AutomationRule",
                context.DeviceId?.ToString() ??
                    context.AutomationRuleId?.ToString(),
                "Registered",
                context.AutomationExecutionId
                    ?.ToString() ??
                Guid.NewGuid().ToString("N"),
                null),
            cancellationToken);

        await _db.SaveChangesAsync(
            cancellationToken);

        return new AutomationActionResult(
            true,
            "Alert registered.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    notificationId = notification.Id,
                    severity
                }));
    }
}

internal sealed class EscalateTicketActionHandler
    : IAutomationActionHandler
{
    private readonly IHelpdeskService _helpdesk;
    private readonly IAdministrativeAuditWriter _audit;

    public EscalateTicketActionHandler(
        IHelpdeskService helpdesk,
        IAdministrativeAuditWriter audit)
    {
        _helpdesk = helpdesk;
        _audit = audit;
    }

    public string Code => "EscalateTicket";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var ticketIdRaw =
            ActionParams.Get(
                context.Action,
                "ticketId");

        if (!Guid.TryParse(
                ticketIdRaw,
                out var ticketId))
        {
            return new AutomationActionResult(
                false,
                "EscalateTicket requires a valid 'ticketId'.");
        }

        var useFallback =
            ActionParams.GetBool(
                context.Action,
                "useFallback",
                true);

        var escalated = useFallback
            ? await _helpdesk
                .RetryAutomaticAssignmentWithFallbackAsync(
                    context.OrganizationId,
                    ticketId,
                    cancellationToken)
            : await _helpdesk
                .RetryAutomaticAssignmentEnterpriseAsync(
                    context.OrganizationId,
                    ticketId,
                    cancellationToken);

        await _audit.WriteAsync(
            new AdministrativeAuditEntry(
                context.OrganizationId,
                context.ActorUserId == Guid.Empty
                    ? null
                    : context.ActorUserId,
                "Automation.EscalateTicket",
                "HelpdeskTicket",
                ticketId.ToString(),
                escalated
                    ? "Escalated"
                    : "NoAssignment",
                context.AutomationExecutionId
                    ?.ToString() ??
                Guid.NewGuid().ToString("N"),
                null),
            cancellationToken);

        return new AutomationActionResult(
            escalated,
            escalated
                ? "Ticket escalated through routing."
                : "No assignment was possible for the ticket.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    ticketId,
                    escalated
                }));
    }
}

internal sealed class CreateFollowUpRecordActionHandler
    : IAutomationActionHandler
{
    private readonly IAdministrativeAuditWriter _audit;

    public CreateFollowUpRecordActionHandler(
        IAdministrativeAuditWriter audit)
    {
        _audit = audit;
    }

    public string Code => "CreateFollowUpRecord";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var summary =
            ActionParams.Get(
                context.Action,
                "summary");

        if (string.IsNullOrWhiteSpace(summary))
        {
            return new AutomationActionResult(
                false,
                "CreateFollowUpRecord requires 'summary'.");
        }

        var targetType =
            ActionParams.Get(
                context.Action,
                "targetType") ??
            "Device";

        await _audit.WriteAsync(
            new AdministrativeAuditEntry(
                context.OrganizationId,
                context.ActorUserId == Guid.Empty
                    ? null
                    : context.ActorUserId,
                "Automation.FollowUp",
                targetType,
                context.DeviceId?.ToString() ??
                    context.AutomationRuleId?.ToString(),
                "Recorded",
                context.AutomationExecutionId
                    ?.ToString() ??
                Guid.NewGuid().ToString("N"),
                null),
            cancellationToken);

        return new AutomationActionResult(
            true,
            "Follow-up record created.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    summary,
                    targetType
                }));
    }
}

internal sealed class RequestRuleReevaluationActionHandler
    : IAutomationActionHandler
{
    private readonly TitanMdmDbContext _db;
    private readonly IAutomationQueueService _queue;

    public RequestRuleReevaluationActionHandler(
        TitanMdmDbContext db,
        IAutomationQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    public string Code => "RequestRuleReevaluation";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var trigger =
            ActionParams.Get(
                context.Action,
                "trigger");

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return new AutomationActionResult(
                false,
                "RequestRuleReevaluation requires 'trigger'.");
        }

        var nextDepth = context.CycleDepth + 1;

        if (nextDepth >
            AutomationPolicyDefaults.MaxCycleDepth)
        {
            return new AutomationActionResult(
                false,
                "Reevaluation cycle limit reached; blocked to prevent loops.");
        }

        var items = 0;

        var rules =
            await _db.Set<AutomationRule>()
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        context.OrganizationId &&
                    x.IsEnabled &&
                    x.HasDefinition)
                .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            var definition =
                AutomationDefinitionSerializer
                    .Deserialize<
                        AutomationRuleDefinition>(
                        rule.DefinitionJson);

            if (definition is null ||
                !definition.Trigger.Equals(
                    trigger,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = new AutomationQueueItem(
                context.OrganizationId,
                rule.Id,
                trigger!,
                context.TriggerPayloadJson,
                context.DeviceId,
                $"reeval-{Guid.NewGuid():N}",
                cycleDepth: nextDepth,
                simulated: context.Simulated,
                actorUserId: context.ActorUserId,
                priority: rule.Priority);

            await _queue.EnqueueAsync(
                item,
                cancellationToken);

            items++;
        }

        return new AutomationActionResult(
            true,
            $"Reevaluation requested for {items} rule(s).",
            AutomationDefinitionSerializer.Serialize(
                new { trigger, items }));
    }
}

internal sealed class ScheduleTaskActionHandler
    : IAutomationActionHandler
{
    private readonly IAutomationQueueService _queue;

    public ScheduleTaskActionHandler(
        IAutomationQueueService queue)
    {
        _queue = queue;
    }

    public string Code => "ScheduleTask";

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        var delaySeconds =
            ActionParams.GetInt(
                context.Action,
                "delaySeconds",
                60);

        if (delaySeconds < 5 || delaySeconds > 86400)
        {
            return new AutomationActionResult(
                false,
                "ScheduleTask delay must be between 5 and 86400 seconds.");
        }

        var actionCode =
            ActionParams.Get(
                context.Action,
                "actionCode");

        if (string.IsNullOrWhiteSpace(actionCode) ||
            !AutomationActionCatalog.IsEnabled(actionCode))
        {
            return new AutomationActionResult(
                false,
                $"ScheduleTask requires an enabled 'actionCode' (received '{actionCode}').");
        }

        var runAt = DateTime.UtcNow.AddSeconds(
            delaySeconds);

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    actionCode,
                    actionParamsJson =
                        ActionParams.Get(
                            context.Action,
                            "actionParamsJson") ??
                        "{}"
                },
                AutomationDefinitionSerializer.Options);

        var item = new AutomationQueueItem(
            context.OrganizationId,
            context.AutomationRuleId,
            "ScheduledTask",
            payload,
            context.DeviceId,
            $"task-{Guid.NewGuid():N}",
            simulated: context.Simulated,
            actorUserId: context.ActorUserId,
            priority: 500,
            notBeforeUtc: runAt,
            maxAttempts: 3);

        await _queue.EnqueueAsync(
            item,
            cancellationToken);

        return new AutomationActionResult(
            true,
            "Task scheduled.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    queueItemId = item.Id,
                    runAtUtc = runAt,
                    actionCode
                }));
    }
}

internal sealed class DeviceCommandActionHandler
    : IAutomationActionHandler
{
    private readonly IDeviceCommandService _commands;
    private readonly string _code;
    private readonly string _commandType;

    public DeviceCommandActionHandler(
        IDeviceCommandService commands,
        string code,
        string commandType)
    {
        _commands = commands;
        _code = code;
        _commandType = commandType;
    }

    public string Code => _code;

    public async Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        if (!context.DeviceId.HasValue ||
            context.DeviceId.Value == Guid.Empty)
        {
            return new AutomationActionResult(
                false,
                $"{_code} requires a device.");
        }

        var commandType = _commandType;
        var payloadJson = "{}";

        if (_code.Equals(
                "SendCommand",
                StringComparison.OrdinalIgnoreCase))
        {
            commandType =
                ActionParams.Get(
                    context.Action,
                    "commandType") ??
                string.Empty;

            payloadJson =
                ActionParams.Get(
                    context.Action,
                    "payloadJson") ??
                "{}";

            if (string.IsNullOrWhiteSpace(
                    commandType))
            {
                return new AutomationActionResult(
                    false,
                    "SendCommand requires 'commandType'.");
            }
        }
        else if (_code.Equals(
                     "WindowsUpdateInstall",
                     StringComparison.OrdinalIgnoreCase))
        {
            payloadJson =
                BuildWindowsUpdatePayload(
                    context.Action);
        }
        else if (_code.Equals(
                     "EnableLostMode",
                     StringComparison.OrdinalIgnoreCase))
        {
            payloadJson =
                JsonSerializer.Serialize(
                    new
                    {
                        message =
                            ActionParams.Get(
                                context.Action,
                                "message") ??
                            "Dispositivo administrado por TitanMDM.",
                        phoneNumber =
                            ActionParams.Get(
                                context.Action,
                                "phoneNumber")
                    });
        }

        var command =
            await _commands.CreateAsync(
                context.OrganizationId,
                context.ActorUserId,
                new CreateDeviceCommandRequest(
                    context.DeviceId.Value,
                    commandType,
                    payloadJson,
                    60),
                cancellationToken);

        return new AutomationActionResult(
            true,
            $"Command '{commandType}' queued.",
            AutomationDefinitionSerializer.Serialize(
                new
                {
                    commandId = command.Id,
                    commandType =
                        command.CommandType,
                    status = command.Status
                }));
    }

    private static string BuildWindowsUpdatePayload(
        AutomationActionDto action)
    {
        var kb =
            ActionParams.Get(
                action,
                "kbArticleIds");

        var kbIds = string.IsNullOrWhiteSpace(kb)
            ? Array.Empty<string>()
            : kb!
                .Split(
                    ',',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                kbArticleIds = kbIds,
                acceptEula =
                    ActionParams.GetBool(
                        action,
                        "acceptEula",
                        true),
                downloadOnly =
                    ActionParams.GetBool(
                        action,
                        "downloadOnly",
                        false)
            });
    }
}

/*
 * Stub para acciones de alto impacto no habilitadas.
 */
internal sealed class DisabledActionHandler
    : IAutomationActionHandler
{
    public DisabledActionHandler(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public Task<AutomationActionResult>
        ExecuteAsync(
            AutomationActionContext context,
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AutomationActionResult(
                false,
                $"Action '{Code}' is registered but not enabled in AUT-1. Pending AUT-2."));
    }
}
