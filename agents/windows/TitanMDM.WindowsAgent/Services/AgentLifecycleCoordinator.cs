namespace TitanMDM.WindowsAgent.Services;

public sealed class AgentLifecycleCoordinator
{
    private readonly object
        _syncRoot = new();

    private AgentLifecycleState
        _state =
            AgentLifecycleState.Starting;

    private string?
        _lastErrorCode;

    private string?
        _lastErrorMessage;

    private DateTime
        _updatedAtUtc =
            DateTime.UtcNow;

    public AgentLifecycleState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public bool HasIdentity
    {
        get
        {
            var state =
                State;

            return
                state ==
                    AgentLifecycleState.Enrolled
                ||
                state ==
                    AgentLifecycleState.Healthy;
        }
    }

    public void SetState(
        AgentLifecycleState state,
        string? errorCode = null,
        string? errorMessage = null)
    {
        lock (_syncRoot)
        {
            _state =
                state;

            _lastErrorCode =
                Normalize(
                    errorCode);

            _lastErrorMessage =
                Normalize(
                    errorMessage);

            _updatedAtUtc =
                DateTime.UtcNow;
        }
    }

    public AgentLifecycleSnapshot
        GetSnapshot()
    {
        lock (_syncRoot)
        {
            return new AgentLifecycleSnapshot(
                _state,
                _lastErrorCode,
                _lastErrorMessage,
                _updatedAtUtc);
        }
    }

    public void MarkStarting()
    {
        SetState(
            AgentLifecycleState.Starting);
    }

    public void MarkEnrollmentRequired(
        string? message = null)
    {
        SetState(
            AgentLifecycleState.EnrollmentRequired,
            null,
            message);
    }

    public void MarkEnrolling()
    {
        SetState(
            AgentLifecycleState.Enrolling);
    }

    public void MarkEnrolled()
    {
        SetState(
            AgentLifecycleState.Enrolled);
    }

    public void MarkHealthy()
    {
        SetState(
            AgentLifecycleState.Healthy);
    }

    public void MarkTokenExpired(
        string? message = null)
    {
        SetState(
            AgentLifecycleState.TokenExpired,
            "TOKEN_EXPIRED",
            message);
    }

    public void MarkTokenInvalid(
        string? code,
        string? message)
    {
        SetState(
            AgentLifecycleState.TokenInvalid,
            code,
            message);
    }

    public void MarkRecoveryRequired(
        string? code,
        string? message)
    {
        SetState(
            AgentLifecycleState.RecoveryRequired,
            code,
            message);
    }

    public void MarkServerUnavailable(
        string? message = null)
    {
        SetState(
            AgentLifecycleState.ServerUnavailable,
            "SERVER_UNAVAILABLE",
            message);
    }

    public void MarkStopping()
    {
        SetState(
            AgentLifecycleState.Stopping);
    }

    private static string?
        Normalize(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();
    }
}

public sealed record AgentLifecycleSnapshot(
    AgentLifecycleState State,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime UpdatedAtUtc);