namespace TitanMDM.Application.Automation;

public sealed record AutomationRuleDto(
    Guid Id,
    string Name,
    string? Description,
    string TriggerType,
    string ConditionsJson,
    string ActionType,
    string ActionPayloadJson,
    bool IsEnabled,
    int ExecutionCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastExecutedAtUtc);

public sealed record AutomationExecutionDto(
    Guid Id,
    Guid AutomationRuleId,
    string AutomationName,
    Guid? DeviceId,
    string? DeviceName,
    string TriggerType,
    string Status,
    string? ResultJson,
    string? ErrorMessage,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record CreateAutomationRuleRequest(
    string Name,
    string? Description,
    string TriggerType,
    string? ConditionsJson,
    string ActionType,
    string? ActionPayloadJson);

public sealed record UpdateAutomationRuleRequest(
    string Name,
    string? Description,
    string TriggerType,
    string? ConditionsJson,
    string ActionType,
    string? ActionPayloadJson,
    bool IsEnabled);

public sealed record ExecuteAutomationRequest(
    Guid? DeviceId,
    string? TriggerPayloadJson);

public sealed record AutomationEvent(
    Guid OrganizationId,
    Guid? DeviceId,
    string TriggerType,
    string TriggerPayloadJson,
    Guid ActorUserId);