namespace TitanMDM.Application.Automation;

/*
 * AUT-1.2 — Modelo de definición empresarial de una regla.
 *
 * Es un documento versionado que se serializa en
 * AutomationRule.DefinitionJson. Sustituye el par legacy
 * (ConditionsJson + ActionType/ActionPayloadJson) para las
 * reglas nuevas, manteniendo compatibilidad total con las
 * reglas antiguas.
 */
public sealed class AutomationRuleDefinition
{
    public int Version { get; set; } = 1;

    public string Module { get; set; } =
        AutomationModules.General;

    public string Trigger { get; set; } =
        "Manual";

    public int Priority { get; set; } = 100;

    public AutomationConditionGroupDto Conditions
    {
        get;
        set;
    } = new();

    public List<AutomationActionDto> Actions
    {
        get;
        set;
    } = new();

    public AutomationPoliciesDto Policies
    {
        get;
        set;
    } = new();

    public AutomationScheduleDefinitionDto? Schedule
    {
        get;
        set;
    }

    public List<Guid> ScopeSiteIds { get; set; } =
        new();
}

public sealed class AutomationConditionGroupDto
{
    public string Operator { get; set; } = "All";

    public List<AutomationConditionDto> Conditions
    {
        get;
        set;
    } = new();

    public List<AutomationConditionGroupDto> Groups
    {
        get;
        set;
    } = new();
}

public sealed class AutomationConditionDto
{
    public string Field { get; set; } = string.Empty;

    public string Operator { get; set; } = "Eq";

    public string? Value { get; set; }

    public List<string>? Values { get; set; }
}

public sealed class AutomationActionDto
{
    public string Code { get; set; } = string.Empty;

    public int Order { get; set; }

    public Dictionary<string, string?>
        Parameters { get; set; } = new();

    public bool ContinueOnError { get; set; }
}

public sealed class AutomationPoliciesDto
{
    public string ErrorPolicy { get; set; } = "StopRule";

    public int DisableAfterFailures { get; set; } = 5;

    public int MaxRetries { get; set; } = 2;

    public int RetryBackoffSeconds { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 60;

    public string ConcurrencyPolicy { get; set; } =
        "SkipIfRunning";

    public int MinSecondsBetweenExecutions { get; set; }
}

public sealed class AutomationScheduleDefinitionDto
{
    public string Recurrence { get; set; } = "Once";

    public int? IntervalMinutes { get; set; }

    public string? TimeOfDay { get; set; }

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public DateTime? StartAtUtc { get; set; }

    public bool CatchUp { get; set; }
}

public static class AutomationModules
{
    public const string General = "general";

    public const string Helpdesk = "helpdesk";

    public const string Windows = "windows";

    public const string Android = "android";

    public const string Ponches = "ponches";

    public const string Inventory = "inventory";

    public const string Compliance = "compliance";

    public const string Notifications = "notifications";

    public static readonly IReadOnlyList<string>
        All = new[]
        {
            General,
            Helpdesk,
            Windows,
            Android,
            Ponches,
            Inventory,
            Compliance,
            Notifications
        };
}

public static class AutomationPolicyDefaults
{
    public const int MaxCycleDepth = 3;

    public const int MaxActionsPerRule = 25;

    public const int MaxConditionsPerGroup = 50;

    public const int MaxConditionDepth = 5;

    public const int MaxConcurrentPerOrganization = 4;

    public const int MaxExecutionsPerMinutePerRule = 60;
}
