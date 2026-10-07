using TitanMDM.WindowsAgent.Contracts;

namespace TitanMDM.WindowsAgent.Services;

public sealed class RemoteSupportSessionManager
{
    private readonly object
        _syncRoot =
            new();

    private readonly ILogger<
        RemoteSupportSessionManager> _logger;

    private RemoteSupportSessionState?
        _currentSession;

    public RemoteSupportSessionManager(
        ILogger<RemoteSupportSessionManager> logger)
    {
        _logger =
            logger;
    }

    public RemoteSupportSessionState?
        CurrentSession
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentSession;
            }
        }
    }

    public bool TryBegin(
        RemoteSupportRequest request,
        out RemoteSupportSessionState state)
    {
        lock (_syncRoot)
        {
            if (_currentSession is not null)
            {
                state =
                    _currentSession;

                return false;
            }

            state =
                new RemoteSupportSessionState(
                    request.SessionId,
                    "Connecting",
                    request.TechnicianDisplayName,
                    request.Reason,
                    DateTime.UtcNow,
                    null);

            _currentSession =
                state;

            _logger.LogInformation(
                "Remote support session {SessionId} accepted by agent for technician {Technician}.",
                request.SessionId,
                request.TechnicianDisplayName);

            return true;
        }
    }

    public void MarkConnected(
        Guid sessionId)
    {
        lock (_syncRoot)
        {
            EnsureCurrentSession(
                sessionId);

            _currentSession =
                _currentSession! with
                {
                    Status =
                        "Connected"
                };
        }
    }

    public RemoteSupportSessionState?
        End(
            Guid sessionId)
    {
        lock (_syncRoot)
        {
            if (_currentSession is null)
            {
                return null;
            }

            if (_currentSession.SessionId !=
                sessionId)
            {
                return null;
            }

            var completed =
                _currentSession with
                {
                    Status =
                        "Completed",

                    EndedAtUtc =
                        DateTime.UtcNow
                };

            _currentSession =
                null;

            _logger.LogInformation(
                "Remote support session {SessionId} released by agent.",
                sessionId);

            return completed;
        }
    }

    private void EnsureCurrentSession(
        Guid sessionId)
    {
        if (_currentSession is null)
        {
            throw new InvalidOperationException(
                "No existe una sesión remota activa.");
        }

        if (_currentSession.SessionId !=
            sessionId)
        {
            throw new InvalidOperationException(
                "SessionId no corresponde con la sesión remota actual.");
        }
    }
}