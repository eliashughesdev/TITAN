using System.Text.RegularExpressions;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
using TitanMDM.Infrastructure.Automation.Enterprise.Scheduling;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Engine;

/*
 * AUT-1.2 / AUT-1.8 — Validación estricta de definiciones.
 *
 * Sin eval, sin SQL de usuario: sólo se aceptan eventos del
 * catálogo, campos con sintaxis segura y acciones registradas
 * y habilitadas.
 */
internal static class AutomationDefinitionValidator
{
    private static readonly Regex FieldPattern =
        new(
            "^[A-Za-z0-9_-]+(\\.[A-Za-z0-9_-]+)*$",
            RegexOptions.Compiled);

    public static AutomationDefinitionValidationDto
        Validate(AutomationRuleDefinition? definition)
    {
        var issues =
            new List<AutomationValidationIssueDto>();

        if (definition is null)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "definition",
                    "Definition is required."));

            return new AutomationDefinitionValidationDto(
                false,
                issues);
        }

        ValidateModule(definition, issues);
        ValidateTrigger(definition, issues);
        ValidatePriority(definition, issues);
        ValidateConditions(
            definition.Trigger,
            definition.Conditions,
            0,
            "conditions",
            issues);
        ValidateActions(definition.Actions, issues);
        ValidatePolicies(definition.Policies, issues);
        ValidateSchedule(definition, issues);

        var isValid = !issues.Any(
            x => x.Severity == "error");

        return new AutomationDefinitionValidationDto(
            isValid,
            issues);
    }

    private static void ValidateModule(
        AutomationRuleDefinition definition,
        List<AutomationValidationIssueDto> issues)
    {
        if (string.IsNullOrWhiteSpace(
                definition.Module) ||
            !AutomationModules.All.Contains(
                definition.Module,
                StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "module",
                    $"Unknown module '{definition.Module}'."));
        }
    }

    private static void ValidateTrigger(
        AutomationRuleDefinition definition,
        List<AutomationValidationIssueDto> issues)
    {
        var descriptor =
            AutomationEventCatalog.Find(
                definition.Trigger);

        if (descriptor is null)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "trigger",
                    $"Unknown event '{definition.Trigger}'."));

            return;
        }

        if (!descriptor.Connected)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "trigger",
                    $"Event '{descriptor.Code}' is not connected yet."));
        }
    }

    private static void ValidatePriority(
        AutomationRuleDefinition definition,
        List<AutomationValidationIssueDto> issues)
    {
        if (definition.Priority is < 0 or > 1000)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "priority",
                    "Priority must be between 0 and 1000."));
        }
    }

    private static void ValidateConditions(
        string? trigger,
        AutomationConditionGroupDto? group,
        int depth,
        string path,
        List<AutomationValidationIssueDto> issues)
    {
        if (group is null)
            return;

        if (depth >
            AutomationPolicyDefaults.MaxConditionDepth)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    path,
                    "Condition nesting is too deep."));

            return;
        }

        if (!string.Equals(
                group.Operator,
                "All",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                group.Operator,
                "Any",
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    $"{path}.operator",
                    "Group operator must be 'All' or 'Any'."));
        }

        if (group.Conditions.Count >
            AutomationPolicyDefaults
                .MaxConditionsPerGroup)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    path,
                    "Too many conditions in group."));
        }

        for (var i = 0;
            i < group.Conditions.Count;
            i++)
        {
            ValidateCondition(
                trigger,
                group.Conditions[i],
                $"{path}.conditions[{i}]",
                issues);
        }

        for (var i = 0;
            i < group.Groups.Count;
            i++)
        {
            ValidateConditions(
                trigger,
                group.Groups[i],
                depth + 1,
                $"{path}.groups[{i}]",
                issues);
        }
    }

    private static void ValidateCondition(
        string? trigger,
        AutomationConditionDto condition,
        string path,
        List<AutomationValidationIssueDto> issues)
    {
        if (string.IsNullOrWhiteSpace(
                condition.Field) ||
            !FieldPattern.IsMatch(
                condition.Field))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    $"{path}.field",
                    $"Invalid field '{condition.Field}'."));

            return;
        }

        if (!AutomationFieldRegistry.IsValidSyntax(
                condition.Field))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    $"{path}.field",
                    "Field syntax is not allowed."));
        }

        if (!AutomationFieldRegistry.IsKnownField(
                trigger,
                condition.Field))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "warning",
                    $"{path}.field",
                    $"Field '{condition.Field}' is not declared in the event catalog."));
        }

        if (!Enum.TryParse<
                AutomationConditionOperator>(
                condition.Operator,
                true,
                out var op))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    $"{path}.operator",
                    $"Unknown operator '{condition.Operator}'."));

            return;
        }

        var needsValue = op switch
        {
            AutomationConditionOperator.Exists =>
                false,
            AutomationConditionOperator.NotExists =>
                false,
            AutomationConditionOperator.IsEmpty =>
                false,
            AutomationConditionOperator.IsNotEmpty =>
                false,
            _ => true
        };

        if (op is AutomationConditionOperator.In or
            AutomationConditionOperator.NotIn)
        {
            if (condition.Values is null ||
                condition.Values.Count == 0)
            {
                issues.Add(
                    new AutomationValidationIssueDto(
                        "error",
                        $"{path}.values",
                        "Operator In/NotIn requires values."));
            }

            return;
        }

        if (needsValue &&
            string.IsNullOrEmpty(
                condition.Value))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    $"{path}.value",
                    $"Operator {op} requires a value."));
        }
    }

    private static void ValidateActions(
        List<AutomationActionDto> actions,
        List<AutomationValidationIssueDto> issues)
    {
        if (actions is null || actions.Count == 0)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "actions",
                    "At least one action is required."));

            return;
        }

        if (actions.Count >
            AutomationPolicyDefaults.MaxActionsPerRule)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "actions",
                    "Too many actions."));
        }

        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var path = $"actions[{i}]";

            var descriptor =
                AutomationActionCatalog.Find(
                    action.Code);

            if (descriptor is null)
            {
                issues.Add(
                    new AutomationValidationIssueDto(
                        "error",
                        $"{path}.code",
                        $"Unknown action '{action.Code}'."));

                continue;
            }

            if (!descriptor.Enabled)
            {
                issues.Add(
                    new AutomationValidationIssueDto(
                        "error",
                        $"{path}.code",
                        $"Action '{descriptor.Code}' is not enabled in AUT-1."));
            }

            foreach (
                var parameter in
                descriptor.Parameters)
            {
                if (parameter.Required &&
                    (action.Parameters is null ||
                     !action.Parameters.TryGetValue(
                         parameter.Name,
                         out var value) ||
                     string.IsNullOrWhiteSpace(
                         value)))
                {
                    issues.Add(
                        new AutomationValidationIssueDto(
                            "error",
                            $"{path}.parameters.{parameter.Name}",
                            $"Action '{descriptor.Code}' requires '{parameter.Name}'."));
                }
            }
        }
    }

    private static void ValidatePolicies(
        AutomationPoliciesDto policies,
        List<AutomationValidationIssueDto> issues)
    {
        if (policies is null)
            return;

        if (policies.TimeoutSeconds is < 1 or > 600)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.timeoutSeconds",
                    "Timeout must be between 1 and 600 seconds."));
        }

        if (policies.MaxRetries is < 0 or > 5)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.maxRetries",
                    "MaxRetries must be between 0 and 5."));
        }

        if (policies.RetryBackoffSeconds is < 0 or > 3600)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.retryBackoffSeconds",
                    "RetryBackoffSeconds must be between 0 and 3600."));
        }

        if (policies.DisableAfterFailures is < 1 or > 50)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.disableAfterFailures",
                    "DisableAfterFailures must be between 1 and 50."));
        }

        if (!Enum.TryParse<
                AutomationErrorPolicy>(
                policies.ErrorPolicy,
                true,
                out _))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.errorPolicy",
                    $"Unknown error policy '{policies.ErrorPolicy}'."));
        }

        if (!Enum.TryParse<
                AutomationConcurrencyPolicy>(
                policies.ConcurrencyPolicy,
                true,
                out _))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.concurrencyPolicy",
                    $"Unknown concurrency policy '{policies.ConcurrencyPolicy}'."));
        }

        if (policies.MinSecondsBetweenExecutions < 0)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "policies.minSecondsBetweenExecutions",
                    "MinSecondsBetweenExecutions must be >= 0."));
        }
    }

    private static void ValidateSchedule(
        AutomationRuleDefinition definition,
        List<AutomationValidationIssueDto> issues)
    {
        var isScheduleTrigger =
            definition.Trigger.Equals(
                "Schedule",
                StringComparison.OrdinalIgnoreCase);

        var schedule = definition.Schedule;

        if (schedule is null)
        {
            if (isScheduleTrigger)
            {
                issues.Add(
                    new AutomationValidationIssueDto(
                        "error",
                        "schedule",
                        "Schedule trigger requires a schedule."));
            }

            return;
        }

        if (!Enum.TryParse<
                AutomationScheduleRecurrence>(
                schedule.Recurrence,
                true,
                out var recurrence))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "schedule.recurrence",
                    $"Unknown recurrence '{schedule.Recurrence}'."));

            return;
        }

        if (recurrence ==
                AutomationScheduleRecurrence.Interval &&
            (schedule.IntervalMinutes ?? 0) < 5)
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "schedule.intervalMinutes",
                    "Interval must be at least 5 minutes."));
        }

        if (recurrence is
                AutomationScheduleRecurrence.Daily or
                AutomationScheduleRecurrence.Weekly or
                AutomationScheduleRecurrence.Monthly &&
            !AutomationScheduleCalculator
                .IsValidTimeOfDay(schedule.TimeOfDay))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "schedule.timeOfDay",
                    "TimeOfDay must be in HH:mm format."));
        }

        if (recurrence ==
                AutomationScheduleRecurrence.Weekly &&
            (schedule.DayOfWeek is null or < 0 or > 6))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "schedule.dayOfWeek",
                    "DayOfWeek must be between 0 and 6."));
        }

        if (recurrence ==
                AutomationScheduleRecurrence.Monthly &&
            (schedule.DayOfMonth is null or < 1 or > 28))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "error",
                    "schedule.dayOfMonth",
                    "DayOfMonth must be between 1 and 28."));
        }

        if (string.IsNullOrWhiteSpace(
                schedule.TimeZoneId))
        {
            issues.Add(
                new AutomationValidationIssueDto(
                    "warning",
                    "schedule.timeZoneId",
                    "Time zone is empty; UTC will be used."));
        }
    }
}
