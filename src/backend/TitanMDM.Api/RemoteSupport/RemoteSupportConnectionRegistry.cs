using System.Collections.Concurrent;

namespace TitanMDM.Api.RemoteSupport;

public sealed class RemoteSupportConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionState>
        _connections = new();

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
        Guid sessionId)
    {
        if (!_connections.TryGetValue(
                connectionId,
                out var state))
        {
            return;
        }

        state.JoinedSessions.TryAdd(
            sessionId,
            0);
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

    public ConcurrentDictionary<Guid, byte>
        JoinedSessions { get; } = new();
}