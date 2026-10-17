namespace TitanMDM.Application.Automation;

public sealed record AutomationEventDescriptorDto(
    string Code,
    string Module,
    string Label,
    string Description,
    bool Connected,
    IReadOnlyCollection<string> PayloadFields);

public sealed record AutomationActionParameterDescriptorDto(
    string Name,
    string Label,
    bool Required,
    string? DefaultValue);

public sealed record AutomationActionDescriptorDto(
    string Code,
    string Module,
    string Label,
    string Description,
    string Impact,
    bool RequiresDevice,
    bool Enabled,
    IReadOnlyCollection<
        AutomationActionParameterDescriptorDto>
        Parameters);

public sealed record AutomationValidationIssueDto(
    string Severity,
    string Path,
    string Message);

public sealed record AutomationDefinitionValidationDto(
    bool IsValid,
    IReadOnlyCollection<AutomationValidationIssueDto>
        Issues);

public sealed record AutomationConditionTraceDto(
    string Path,
    string Field,
    string Operator,
    string? Expected,
    string? Actual,
    bool Passed);

public sealed record AutomationPlannedActionDto(
    string Code,
    int Order,
    bool Valid,
    string? Message);

public sealed record AutomationSimulationResultDto(
    Guid RuleId,
    bool Matched,
    bool Simulated,
    IReadOnlyCollection<
        AutomationConditionTraceDto>
        ConditionTraces,
    IReadOnlyCollection<
        AutomationPlannedActionDto>
        PlannedActions,
    IReadOnlyCollection<string> Warnings);

public sealed record AutomationDashboardDto(
    int TotalRules,
    int ActiveRules,
    int ExecutionsLast24Hours,
    int FailuresLast24Hours,
    int QueuePending,
    int QueueDeadLetter,
    double SuccessRate,
    double AverageDurationMs,
    IReadOnlyCollection<AutomationExecutionDto>
        RecentExecutions,
    IReadOnlyCollection<AutomationAlertDto> Alerts);

public sealed record AutomationAlertDto(
    string Severity,
    string Message,
    Guid? RuleId,
    DateTime CreatedAtUtc);

public sealed record AutomationActionRunDto(
    string Code,
    int Order,
    string Status,
    string? Message,
    string? ResultJson);

public sealed record AutomationExecutionDetailDto(
    Guid Id,
    Guid AutomationRuleId,
    string AutomationName,
    Guid? DeviceId,
    string? DeviceName,
    string TriggerType,
    string Status,
    bool Simulated,
    int RetryCount,
    int CycleDepth,
    long? DurationMs,
    string? ResultJson,
    string? ErrorMessage,
    IReadOnlyCollection<AutomationActionRunDto>
        Actions,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record AutomationNotificationDto(
    Guid Id,
    Guid? AutomationRuleId,
    string? AutomationName,
    Guid? DeviceId,
    string Title,
    string Message,
    string Severity,
    string Channel,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public sealed record AutomationScheduleDto(
    Guid Id,
    Guid AutomationRuleId,
    string Recurrence,
    int? IntervalMinutes,
    string? TimeOfDay,
    int? DayOfWeek,
    int? DayOfMonth,
    string TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? NextRunAtUtc,
    DateTime? LastRunAtUtc,
    bool CatchUp,
    bool IsEnabled);

public sealed record AutomationScheduleRequest(
    string Recurrence,
    int? IntervalMinutes,
    string? TimeOfDay,
    int? DayOfWeek,
    int? DayOfMonth,
    string? TimeZoneId,
    DateTime? StartAtUtc,
    bool CatchUp,
    bool IsEnabled);

public sealed record AutomationQueueStatsDto(
    int Pending,
    int Retried,
    int Running,
    int DeadLetter);

public sealed record AutomationExecutionQuery(
    Guid? RuleId = null,
    string? Status = null,
    bool? Simulated = null,
    int Limit = 100);

public sealed record EnterpriseCreateRuleRequest(
    string Name,
    string? Description,
    bool IsEnabled,
    AutomationRuleDefinition Definition);

public sealed record EnterpriseUpdateRuleRequest(
    string Name,
    string? Description,
    bool IsEnabled,
    AutomationRuleDefinition Definition);

public sealed record SimulateRuleRequest(
    Guid? DeviceId,
    string? TriggerPayloadJson);

public sealed record AutomationEnterpriseRuleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    string Module,
    int Priority,
    int ConsecutiveFailures,
    int ExecutionCount,
    DateTime UpdatedAtUtc,
    DateTime? LastExecutedAtUtc,
    AutomationRuleDefinition Definition);
