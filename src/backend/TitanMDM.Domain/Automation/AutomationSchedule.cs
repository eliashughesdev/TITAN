namespace TitanMDM.Domain.Automation;

public sealed class AutomationSchedule
{
    private AutomationSchedule()
    {
    }

    public AutomationSchedule(
        Guid organizationId,
        Guid automationRuleId,
        AutomationScheduleRecurrence recurrence,
        string timeZoneId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (automationRuleId == Guid.Empty)
            throw new ArgumentException(
                "AutomationRuleId is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AutomationRuleId = automationRuleId;
        Recurrence = recurrence;
        TimeZoneId = string.IsNullOrWhiteSpace(
                timeZoneId)
            ? "UTC"
            : timeZoneId.Trim();
        IsEnabled = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid AutomationRuleId { get; private set; }

    public AutomationScheduleRecurrence Recurrence
    {
        get;
        private set;
    }

    public int? IntervalMinutes { get; private set; }

    public string? TimeOfDay { get; private set; }

    public int? DayOfWeek { get; private set; }

    public int? DayOfMonth { get; private set; }

    public string TimeZoneId { get; private set; } =
        "UTC";

    public DateTime? StartAtUtc { get; private set; }

    public DateTime? NextRunAtUtc { get; private set; }

    public DateTime? LastRunAtUtc { get; private set; }

    public bool CatchUp { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        AutomationScheduleRecurrence recurrence,
        int? intervalMinutes,
        string? timeOfDay,
        int? dayOfWeek,
        int? dayOfMonth,
        string timeZoneId,
        DateTime? startAtUtc,
        bool catchUp,
        bool isEnabled)
    {
        Recurrence = recurrence;
        IntervalMinutes = intervalMinutes;
        TimeOfDay = timeOfDay;
        DayOfWeek = dayOfWeek;
        DayOfMonth = dayOfMonth;
        TimeZoneId = string.IsNullOrWhiteSpace(
                timeZoneId)
            ? "UTC"
            : timeZoneId.Trim();
        StartAtUtc = startAtUtc;
        CatchUp = catchUp;
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ScheduleNext(DateTime? nextRunAtUtc)
    {
        NextRunAtUtc = nextRunAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RegisterRun(DateTime runAtUtc)
    {
        LastRunAtUtc = runAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
