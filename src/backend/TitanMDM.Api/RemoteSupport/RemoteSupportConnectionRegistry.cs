using System.Collections.Concurrent;

namespace TitanMDM.Api.RemoteSupport;

public sealed class RemoteSupportConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionState>
        _connections = new();

    private readonly ConcurrentDictionary<
        (Guid OrganizationId, Guid SessionId),
        RemoteControlLeaseState> _controlLeases = new();

    private readonly ConcurrentDictionary<
        (Guid OrganizationId, Guid SessionId),
        SessionGate> _sessionGates = new();

    public async ValueTask<IAsyncDisposable> LockSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var key = (organizationId, sessionId);

        while (true)
        {
            var gate = _sessionGates.GetOrAdd(
                key,
                static _ => new SessionGate());

            lock (gate)
            {
                if (gate.Retired)
                {
                    continue;
                }

                gate.Users++;
            }

            try
            {
                await gate.Semaphore.WaitAsync(cancellationToken);
                return new SessionGateLease(this, key, gate);
            }
            catch
            {
                RetireSessionGateUser(key, gate, releaseSemaphore: false);
                throw;
            }
        }
    }

    public void RegisterHuman(
        string connectionId,
        Guid organizationId,
        Guid userId,
        string displayName)
    {
        _connections[connectionId] =
            new ConnectionState(
                connectionId,
                RemoteConnectionKind.Human,
                organizationId,
                userId,
                displayName,
                null,
                null,
                DateTime.UtcNow);
    }

    public void RegisterRemoteHost(
        string connectionId,
        Guid organizationId,
        Guid deviceId,
        Guid sessionId)
    {
        _connections[connectionId] =
            new ConnectionState(
                connectionId,
                RemoteConnectionKind.RemoteHost,
                organizationId,
                null,
                null,
                deviceId,
                sessionId,
                DateTime.UtcNow);
    }

    public bool TryGet(
        string connectionId,
        out ConnectionState state)
    {
        return _connections.TryGetValue(
            connectionId,
            out state!);
    }

    public void JoinSession(
        string connectionId,
        RemoteSessionAccessState session)
    {
        if (!_connections.TryGetValue(
                connectionId,
                out var state))
        {
            return;
        }

        state.JoinedSessions[session.SessionId] = session;
    }

    public void LeaveSession(
        string connectionId,
        Guid sessionId)
    {
        if (!_connections.TryGetValue(
                connectionId,
                out var state))
        {
            return;
        }

        state.JoinedSessions.TryRemove(
            sessionId,
            out _);
    }

    public IReadOnlyCollection<Guid> GetJoinedSessions(
        string connectionId)
    {
        if (!_connections.TryGetValue(
                connectionId,
                out var state))
        {
            return Array.Empty<Guid>();
        }

        return state.JoinedSessions.Keys.ToArray();
    }

    public bool TryGetJoinedSession(
        string connectionId,
        Guid sessionId,
        out RemoteSessionAccessState session)
    {
        session = default!;

        if (_connections.TryGetValue(connectionId, out var state)
            && state.JoinedSessions.TryGetValue(
                sessionId,
                out var joinedSession))
        {
            session = joinedSession;
            return true;
        }

        return false;
    }

    public void SetControlLease(
        Guid organizationId,
        Guid sessionId,
        RemoteControlLeaseState lease)
    {
        var key = (organizationId, sessionId);

        if (!lease.HasController)
        {
            _controlLeases.TryRemove(key, out _);
            return;
        }

        _controlLeases[key] = lease;
    }

    public bool TryGetControlLease(
        Guid organizationId,
        Guid sessionId,
        out RemoteControlLeaseState lease)
    {
        var key = (organizationId, sessionId);

        if (!_controlLeases.TryGetValue(key, out lease!))
        {
            return false;
        }

        if (lease.ExpiresAtUtc > DateTime.UtcNow)
        {
            return true;
        }

        _controlLeases.TryRemove(key, out _);
        lease = default!;
        return false;
    }

    public void RemoveControlLease(
        Guid organizationId,
        Guid sessionId)
    {
        _controlLeases.TryRemove(
            (organizationId, sessionId),
            out _);
    }

    public ConnectionState? Remove(
        string connectionId)
    {
        _connections.TryRemove(
            connectionId,
            out var state);

        return state;
    }

    public bool HasRemoteHost(
        Guid sessionId)
    {
        return _connections.Values.Any(
            x =>
                x.Kind == RemoteConnectionKind.RemoteHost
                &&
                x.RemoteSessionId == sessionId);
    }

    public int GetTechnicianCount(
        Guid sessionId)
    {
        return _connections.Values.Count(
            x =>
                x.Kind == RemoteConnectionKind.Human
                &&
                x.JoinedSessions.ContainsKey(sessionId));
    }

    private void ReleaseSessionGate(
        (Guid OrganizationId, Guid SessionId) key,
        SessionGate gate)
    {
        RetireSessionGateUser(key, gate, releaseSemaphore: true);
    }

    private void RetireSessionGateUser(
        (Guid OrganizationId, Guid SessionId) key,
        SessionGate gate,
        bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            gate.Semaphore.Release();
        }

        lock (gate)
        {
            gate.Users--;

            if (gate.Users != 0)
            {
                return;
            }

            gate.Retired = true;
            _sessionGates.TryRemove(
                new KeyValuePair<
                    (Guid OrganizationId, Guid SessionId),
                    SessionGate>(key, gate));
        }
    }

    private sealed class SessionGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }

        public bool Retired { get; set; }
    }

    private sealed class SessionGateLease : IAsyncDisposable
    {
        private readonly RemoteSupportConnectionRegistry _registry;
        private readonly (
            Guid OrganizationId,
            Guid SessionId) _key;
        private SessionGate? _gate;

        public SessionGateLease(
            RemoteSupportConnectionRegistry registry,
            (Guid OrganizationId, Guid SessionId) key,
            SessionGate gate)
        {
            _registry = registry;
            _key = key;
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            var gate = Interlocked.Exchange(ref _gate, null);

            if (gate is not null)
            {
                _registry.ReleaseSessionGate(_key, gate);
            }

            return ValueTask.CompletedTask;
        }
    }
}

public enum RemoteConnectionKind
{
    Human = 1,
    RemoteHost = 2
}

public sealed class ConnectionState
{
    public ConnectionState(
        string connectionId,
        RemoteConnectionKind kind,
        Guid organizationId,
        Guid? userId,
        string? displayName,
        Guid? deviceId,
        Guid? remoteSessionId,
        DateTime connectedAtUtc)
    {
        ConnectionId = connectionId;
        Kind = kind;
        OrganizationId = organizationId;
        UserId = userId;
        DisplayName = displayName;
        DeviceId = deviceId;
        RemoteSessionId = remoteSessionId;
        ConnectedAtUtc = connectedAtUtc;
    }

    public string ConnectionId { get; }

    public RemoteConnectionKind Kind { get; }

    public Guid OrganizationId { get; }

    public Guid? UserId { get; }

    public string? DisplayName { get; }

    public Guid? DeviceId { get; }

    public Guid? RemoteSessionId { get; }

    public DateTime ConnectedAtUtc { get; }

    public ConcurrentDictionary<Guid, RemoteSessionAccessState>
        JoinedSessions
    { get; } = new();
}

public sealed record RemoteSessionAccessState(
    Guid OrganizationId,
    Guid SessionId,
    DateTime ExpiresAtUtc,
    bool AllowMouse,
    bool AllowKeyboard);
