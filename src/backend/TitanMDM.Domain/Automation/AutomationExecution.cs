namespace TitanMDM.Domain.Automation;

public sealed class AutomationExecution
{
    private AutomationExecution()
    {
    }

    public AutomationExecution(
        Guid organizationId,
        Guid automationRuleId,
        Guid? deviceId,
        string triggerType,
        string triggerPayloadJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AutomationRuleId = automationRuleId;
        DeviceId = deviceId;

        TriggerType =
            string.IsNullOrWhiteSpace(triggerType)
                ? "Manual"
                : triggerType.Trim();

        TriggerPayloadJson =
            string.IsNullOrWhiteSpace(
                triggerPayloadJson)
                ? "{}"
                : triggerPayloadJson.Trim();

        Status =
            AutomationExecutionStatus.Running;

        StartedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid AutomationRuleId { get; private set; }

    public Guid? DeviceId { get; private set; }

    public string TriggerType { get; private set; } =
        string.Empty;

    public string TriggerPayloadJson
    {
        get;
        private set;
    } = "{}";

    public AutomationExecutionStatus Status
    {
        get;
        private set;
    }

    public string? ResultJson { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public int RetryCount { get; private set; }

    public long? DurationMs { get; private set; }

    public bool Simulated { get; private set; }

    public int CycleDepth { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public void Configure(
        int cycleDepth,
        Guid? actorUserId,
        bool simulated)
    {
        CycleDepth = Math.Max(0, cycleDepth);
        ActorUserId =
            actorUserId == Guid.Empty
                ? null
                : actorUserId;
        Simulated = simulated;
    }

    public void SetRetryCount(int retryCount)
    {
        RetryCount = Math.Max(0, retryCount);
    }

    public void Complete(
        string? resultJson = null)
    {
        Status =
            AutomationExecutionStatus.Success;

        ResultJson =
            string.IsNullOrWhiteSpace(resultJson)
                ? "{}"
                : resultJson;

        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = ComputeDuration();
    }

    public void Fail(string error)
    {
        Status =
            AutomationExecutionStatus.Failed;

        ErrorMessage =
            string.IsNullOrWhiteSpace(error)
                ? "Automation execution failed."
                : error.Trim();

        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = ComputeDuration();
    }

    public void Skip(string reason)
    {
        Status =
            AutomationExecutionStatus.Skipped;

        ResultJson = reason;
        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = ComputeDuration();
    }

    private long ComputeDuration()
    {
        var completedAt =
            CompletedAtUtc ??
            DateTime.UtcNow;

        var duration =
            (completedAt -
                StartedAtUtc).TotalMilliseconds;

        return duration < 0
            ? 0
            : (long)duration;
    }
}