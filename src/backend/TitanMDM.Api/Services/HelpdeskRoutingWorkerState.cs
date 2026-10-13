namespace TitanMDM.Api.Services;

public sealed class HelpdeskRoutingWorkerState
{
    private readonly object _sync = new();
    private DateTime? _startedAtUtc;
    private DateTime? _lastCycleStartedAtUtc;
    private DateTime? _lastCycleCompletedAtUtc;
    private string? _lastError;
    private long _completedCycles;
    private long _errors;
    private int _lastSelectedTickets;
    private bool _running;

    public void Started()
    {
        lock (_sync)
        {
            _running = true;
            _startedAtUtc = DateTime.UtcNow;
        }
    }

    public void Stopped()
    {
        lock (_sync)
        {
            _running = false;
        }
    }

    public void CycleStarted()
    {
        lock (_sync)
        {
            _lastCycleStartedAtUtc = DateTime.UtcNow;
        }
    }

    public void CycleCompleted(int selectedTickets)
    {
        lock (_sync)
        {
            _lastCycleCompletedAtUtc = DateTime.UtcNow;
            _lastSelectedTickets = selectedTickets;
            _completedCycles++;
        }
    }

    public void RecordError(Exception exception)
    {
        lock (_sync)
        {
            _errors++;
            _lastError = exception.Message.Length <= 500
                ? exception.Message
                : exception.Message[..500];
        }
    }

    public HelpdeskRoutingWorkerSnapshot Snapshot(bool aiAvailable)
    {
        lock (_sync)
        {
            return new HelpdeskRoutingWorkerSnapshot(
                _running,
                _startedAtUtc,
                _lastCycleStartedAtUtc,
                _lastCycleCompletedAtUtc,
                _completedCycles,
                _errors,
                _lastSelectedTickets,
                _lastError,
                aiAvailable);
        }
    }
}

public sealed record HelpdeskRoutingWorkerSnapshot(
    bool Running,
    DateTime? StartedAtUtc,
    DateTime? LastCycleStartedAtUtc,
    DateTime? LastCycleCompletedAtUtc,
    long CompletedCycles,
    long Errors,
    int LastSelectedTickets,
    string? LastError,
    bool AiAvailable);
