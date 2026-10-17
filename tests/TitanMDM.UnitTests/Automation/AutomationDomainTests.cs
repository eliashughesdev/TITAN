using TitanMDM.Domain.Automation;

namespace TitanMDM.UnitTests.Automation;

public sealed class AutomationDomainTests
{
    private static readonly Guid Org =
        Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

    private static readonly Guid User =
        Guid.Parse(
            "22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ApplyDefinitionMarksRuleAsEnterprise()
    {
        var rule = NewRule();

        Assert.False(rule.HasDefinition);

        rule.ApplyDefinition(
            "{\"version\":1}",
            AutomationTriggerType.Manual,
            "general",
            250,
            "[\"33333333-3333-3333-3333-333333333333\"]",
            User);

        Assert.True(rule.HasDefinition);
        Assert.Equal(250, rule.Priority);
        Assert.Equal("general", rule.Module);
        Assert.NotNull(rule.ScopeSiteIdsJson);
        Assert.Equal(User, rule.ModifiedByUserId);
    }

    [Fact]
    public void ConsecutiveFailuresResetOnce()
    {
        var rule = NewRule();

        rule.RegisterFailure();
        rule.RegisterFailure();

        Assert.Equal(2, rule.ConsecutiveFailures);

        rule.ResetFailures();

        Assert.Equal(0, rule.ConsecutiveFailures);
    }

    [Fact]
    public void QueueItemIdempotentTryAcquire()
    {
        var item = NewQueueItem();

        var now = DateTime.UtcNow;
        var lease = TimeSpan.FromMinutes(2);

        Assert.True(item.TryAcquire(now, lease));
        Assert.Equal(
            AutomationQueueItemState.Running,
            item.State);
        Assert.Equal(1, item.Attempts);

        Assert.False(item.TryAcquire(now, lease));

        item.Complete();

        Assert.Equal(
            AutomationQueueItemState.Completed,
            item.State);
        Assert.False(item.TryAcquire(now, lease));
    }

    [Fact]
    public void QueueItemDeadLettersAfterMaxAttempts()
    {
        var item = NewQueueItem(maxAttempts: 2);

        var now = DateTime.UtcNow;

        Assert.True(item.TryAcquire(now, TimeSpan.FromMinutes(1)));

        var scheduled = item.ScheduleRetry(
            "boom",
            now.AddSeconds(-1));

        Assert.True(scheduled);
        Assert.Equal(
            AutomationQueueItemState.Retried,
            item.State);

        Assert.True(item.TryAcquire(now, TimeSpan.FromMinutes(1)));

        var deadLettered = item.ScheduleRetry(
            "boom",
            now.AddSeconds(-1));

        Assert.False(deadLettered);
        Assert.Equal(
            AutomationQueueItemState.DeadLetter,
            item.State);
    }

    [Fact]
    public void QueueItemRecoversExpiredLease()
    {
        var item = NewQueueItem();

        item.TryAcquire(
            DateTime.UtcNow,
            TimeSpan.FromMinutes(-1));

        item.RecoverExpiredLease();

        Assert.Equal(
            AutomationQueueItemState.Retried,
            item.State);
    }

    [Fact]
    public void ExecutionLifecycleComputesDuration()
    {
        var execution = new AutomationExecution(
            Org,
            Guid.NewGuid(),
            null,
            "Manual",
            "{}");

        execution.Configure(
            -5,
            Guid.Empty,
            simulated: true);
        execution.SetRetryCount(3);

        Assert.Equal(0, execution.CycleDepth);
        Assert.Null(execution.ActorUserId);
        Assert.True(execution.Simulated);
        Assert.Equal(3, execution.RetryCount);

        execution.Complete("{\"ok\":true}");

        Assert.Equal(
            AutomationExecutionStatus.Success,
            execution.Status);
        Assert.NotNull(execution.DurationMs);
        Assert.NotNull(execution.CompletedAtUtc);
    }

    [Fact]
    public void ExecutionFailAndSkipAreTerminal()
    {
        var failed = new AutomationExecution(
            Org,
            Guid.NewGuid(),
            null,
            "Manual",
            "{}");

        failed.Fail("nope");

        Assert.Equal(
            AutomationExecutionStatus.Failed,
            failed.Status);
        Assert.Equal("nope", failed.ErrorMessage);

        var skipped = new AutomationExecution(
            Org,
            Guid.NewGuid(),
            null,
            "Manual",
            "{}");

        skipped.Skip("not now");

        Assert.Equal(
            AutomationExecutionStatus.Skipped,
            skipped.Status);
    }

    [Fact]
    public void ScheduleTracksNextAndLastRun()
    {
        var schedule = new AutomationSchedule(
            Org,
            Guid.NewGuid(),
            AutomationScheduleRecurrence.Daily,
            "UTC");

        schedule.Update(
            AutomationScheduleRecurrence.Interval,
            30,
            null,
            null,
            null,
            "UTC",
            null,
            false,
            true);

        Assert.Equal(
            AutomationScheduleRecurrence.Interval,
            schedule.Recurrence);
        Assert.Equal(30, schedule.IntervalMinutes);
        Assert.True(schedule.IsEnabled);

        var next = DateTime.UtcNow.AddMinutes(30);
        schedule.ScheduleNext(next);
        Assert.Equal(next, schedule.NextRunAtUtc);

        var runAt = DateTime.UtcNow;
        schedule.RegisterRun(runAt);
        Assert.Equal(runAt, schedule.LastRunAtUtc);
    }

    private static AutomationRule NewRule()
    {
        return new AutomationRule(
            Org,
            "Rule",
            "description",
            AutomationTriggerType.Manual,
            "{}",
            AutomationActionType.Notification,
            "{}",
            User);
    }

    private static AutomationQueueItem NewQueueItem(
        int maxAttempts = 3)
    {
        return new AutomationQueueItem(
            Org,
            Guid.NewGuid(),
            "Manual",
            "{}",
            null,
            "key-" + Guid.NewGuid().ToString("N"),
            maxAttempts: maxAttempts);
    }
}
