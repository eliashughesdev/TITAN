using TitanMDM.Api.Services;

namespace TitanMDM.IntegrationTests;

public sealed class HelpdeskRoutingWorkerStateTests
{
    [Fact]
    public void ReportsLifecycleCyclesErrorsAndAiAvailability()
    {
        var state = new HelpdeskRoutingWorkerState();

        state.Started();
        state.CycleStarted();
        state.RecordError(new InvalidOperationException("temporary"));
        state.CycleCompleted(7);

        var running = state.Snapshot(true);
        Assert.True(running.Running);
        Assert.True(running.AiAvailable);
        Assert.Equal(1, running.CompletedCycles);
        Assert.Equal(1, running.Errors);
        Assert.Equal(7, running.LastSelectedTickets);
        Assert.Equal("temporary", running.LastError);
        Assert.NotNull(running.LastCycleCompletedAtUtc);

        state.Stopped();
        Assert.False(state.Snapshot(true).Running);
    }
}
