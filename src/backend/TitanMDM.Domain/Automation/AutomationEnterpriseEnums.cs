namespace TitanMDM.Domain.Automation;

public enum AutomationQueueItemState
{
    Pending = 0,

    Running = 1,

    Retried = 2,

    Completed = 3,

    Failed = 4,

    DeadLetter = 5,

    Skipped = 6
}

public enum AutomationScheduleRecurrence
{
    Once = 0,

    Hourly = 1,

    Daily = 2,

    Weekly = 3,

    Monthly = 4,

    Interval = 5
}

public enum AutomationConditionOperator
{
    Eq = 0,

    NotEq = 1,

    GreaterThan = 2,

    GreaterThanOrEqual = 3,

    LessThan = 4,

    LessThanOrEqual = 5,

    Contains = 6,

    NotContains = 7,

    StartsWith = 8,

    EndsWith = 9,

    In = 10,

    NotIn = 11,

    Exists = 12,

    NotExists = 13,

    IsEmpty = 14,

    IsNotEmpty = 15
}

public enum AutomationLogicalOperator
{
    All = 0,

    Any = 1
}

public enum AutomationErrorPolicy
{
    StopRule = 0,

    ContinueActions = 1,

    DisableRuleAfterFailures = 2
}

public enum AutomationConcurrencyPolicy
{
    Parallel = 0,

    SkipIfRunning = 1,

    Queue = 2
}
