using TitanMDM.Api.RemoteSupport;

namespace TitanMDM.IntegrationTests;

public sealed class RemoteSupportConnectionRegistryTests
{
    [Fact]
    public void JoinedSessionIsScopedToRegisteredConnection()
    {
        var registry = new RemoteSupportConnectionRegistry();
        var organizationId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";

        registry.RegisterHuman(
            connectionId,
            organizationId,
            Guid.NewGuid(),
            "Technician");
        registry.JoinSession(
            connectionId,
            new RemoteSessionAccessState(
                organizationId,
                sessionId,
                DateTime.UtcNow.AddMinutes(5),
                AllowMouse: true,
                AllowKeyboard: true));

        Assert.True(registry.TryGetJoinedSession(
            connectionId,
            sessionId,
            out var session));
        Assert.Equal(organizationId, session.OrganizationId);
        Assert.False(registry.TryGetJoinedSession(
            "another-connection",
            sessionId,
            out _));
    }

    [Fact]
    public void ExpiredLeaseIsRejectedAndEvicted()
    {
        var registry = new RemoteSupportConnectionRegistry();
        var organizationId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        registry.SetControlLease(
            organizationId,
            sessionId,
            new RemoteControlLeaseState(
                true,
                Guid.NewGuid(),
                "Technician",
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddSeconds(-1)));

        Assert.False(registry.TryGetControlLease(
            organizationId,
            sessionId,
            out _));
        Assert.False(registry.TryGetControlLease(
            organizationId,
            sessionId,
            out _));
    }

    [Fact]
    public void ActiveLeaseCanBeRemoved()
    {
        var registry = new RemoteSupportConnectionRegistry();
        var organizationId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        registry.SetControlLease(
            organizationId,
            sessionId,
            new RemoteControlLeaseState(
                true,
                Guid.NewGuid(),
                "Technician",
                DateTime.UtcNow,
                DateTime.UtcNow.AddSeconds(45)));

        Assert.True(registry.TryGetControlLease(
            organizationId,
            sessionId,
            out _));

        registry.RemoveControlLease(organizationId, sessionId);

        Assert.False(registry.TryGetControlLease(
            organizationId,
            sessionId,
            out _));
    }
}
