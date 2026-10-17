namespace TitanMDM.Domain.Automation;

public sealed class AutomationQueueItem
{
    private AutomationQueueItem()
    {
    }

    public AutomationQueueItem(
        Guid organizationId,
        Guid? automationRuleId,
        string triggerType,
        string triggerPayloadJson,
        Guid? deviceId,
        string idempotencyKey,
        int cycleDepth = 0,
        bool simulated = false,
        Guid? actorUserId = null,
        int priority = 100,
        DateTime? notBeforeUtc = null,
        int maxAttempts = 3)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (string.IsNullOrWhiteSpace(
                triggerType))
            throw new ArgumentException(
                "TriggerType is required.");

        if (string.IsNullOrWhiteSpace(
                idempotencyKey))
            throw new ArgumentException(
                "IdempotencyKey is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AutomationRuleId = automationRuleId;
        TriggerType = triggerType.Trim();

        TriggerPayloadJson =
            string.IsNullOrWhiteSpace(
                triggerPayloadJson)
                ? "{}"
                : triggerPayloadJson.Trim();

        DeviceId = deviceId;
        IdempotencyKey =
            idempotencyKey.Trim();
        CycleDepth = Math.Max(0, cycleDepth);
        Simulated = simulated;
        ActorUserId =
            actorUserId == Guid.Empty
                ? null
                : actorUserId;
        Priority = Math.Clamp(priority, 0, 1000);
        MaxAttempts = Math.Clamp(maxAttempts, 1, 10);
        State =
            AutomationQueueItemState.Pending;
        NextAttemptAtUtc =
            notBeforeUtc ??
            DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? AutomationRuleId { get; private set; }

    public Guid? AutomationExecutionId { get; private set; }

    public Guid? DeviceId { get; private set; }

    public string TriggerType { get; private set; } =
        string.Empty;

    public string TriggerPayloadJson
    {
        get;
        private set;
    } = "{}";

    public AutomationQueueItemState State
    {
        get;
        private set;
    }

    public int Attempts { get; private set; }

    public int MaxAttempts { get; private set; } = 3;

    public int CycleDepth { get; private set; }

    public bool Simulated { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public int Priority { get; private set; } = 100;

    public string IdempotencyKey { get; private set; } =
        string.Empty;

    public DateTime NextAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? LeaseUntilUtc
    {
        get;
        private set;
    }

    public DateTime? CompletedAtUtc
    {
        get;
        private set;
    }

    public string? LastError { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void AttachExecution(
        Guid automationExecutionId)
    {
        AutomationExecutionId =
            automationExecutionId;

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool TryAcquire(
        DateTime nowUtc,
        TimeSpan leaseDuration)
    {
        if (State ==
                AutomationQueueItemState.Completed ||
            State ==
                AutomationQueueItemState.DeadLetter ||
            State ==
                AutomationQueueItemState.Skipped)
        {
            return false;
        }

        var isLeaseExpired =
            !LeaseUntilUtc.HasValue ||
            LeaseUntilUtc.Value <= nowUtc;

        var isRunningButStale =
            State ==
                AutomationQueueItemState.Running &&
            isLeaseExpired;

        if (State ==
                AutomationQueueItemState.Running &&
            !isRunningButStale)
        {
            return false;
        }

        if (NextAttemptAtUtc > nowUtc)
            return false;

        State = AutomationQueueItemState.Running;
        Attempts++;
        LeaseUntilUtc =
            nowUtc + leaseDuration;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void Complete()
    {
        State =
            AutomationQueueItemState.Completed;

        LastError = null;
        LeaseUntilUtc = null;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Skip(string reason)
    {
        State =
            AutomationQueueItemState.Skipped;

        LastError = reason;
        LeaseUntilUtc = null;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool ScheduleRetry(
        string error,
        DateTime nextAttemptAtUtc)
    {
        LastError = error;

        if (Attempts >= MaxAttempts)
        {
            State =
                AutomationQueueItemState
                    .DeadLetter;

            LeaseUntilUtc = null;
            CompletedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
            return false;
        }

        State =
            AutomationQueueItemState.Retried;

        NextAttemptAtUtc = nextAttemptAtUtc;
        LeaseUntilUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public void Fail(string error)
    {
        State =
            AutomationQueueItemState.Failed;

        LastError = error;
        LeaseUntilUtc = null;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecoverExpiredLease()
    {
        if (State !=
            AutomationQueueItemState.Running)
            return;

        State =
            AutomationQueueItemState.Retried;

        LeaseUntilUtc = null;
        NextAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
