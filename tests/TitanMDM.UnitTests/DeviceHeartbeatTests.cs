using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

namespace TitanMDM.UnitTests;

public sealed class DeviceHeartbeatTests
{
    [Fact]
    public void NullIpPreservesLastValidAddress()
    {
        var device = CreateDevice();

        device.RegisterHeartbeat(
            "10.20.30.40",
            null);
        device.RegisterHeartbeat(
            null,
            null);

        Assert.Equal(
            "10.20.30.40",
            device.IpAddress);
    }

    [Fact]
    public void GenericHeartbeatAcceptsIpv6()
    {
        var device = CreateDevice();

        device.RegisterHeartbeat(
            "2001:db8::15",
            null);

        Assert.Equal(
            "2001:db8::15",
            device.IpAddress);
    }

    [Fact]
    public void InvalidIpDoesNotEraseLastValidAddress()
    {
        var device = CreateDevice();

        device.RegisterHeartbeat(
            "192.168.50.10",
            null);
        device.RegisterHeartbeat(
            "0.0.0.0",
            null);

        Assert.Equal(
            "192.168.50.10",
            device.IpAddress);
    }

    [Fact]
    public void ObservedLogoffClearsInteractiveSession()
    {
        var device = CreateDevice();

        device.UpdateInteractiveUser(
            "CONTOSO\\alice",
            4);
        device.UpdateInteractiveUser(
            null,
            null);

        Assert.Null(device.InteractiveUser);
        Assert.Null(device.InteractiveSessionId);
        Assert.NotNull(
            device.InteractiveUserUpdatedAtUtc);
    }

    [Fact]
    public void NegativeInteractiveSessionIsNotPersisted()
    {
        var device = CreateDevice();

        device.UpdateInteractiveUser(
            "CONTOSO\\alice",
            -1);

        Assert.Equal(
            "CONTOSO\\alice",
            device.InteractiveUser);
        Assert.Null(device.InteractiveSessionId);
    }

    private static Device CreateDevice() =>
        new(
            Guid.NewGuid(),
            "PC-001",
            DevicePlatform.Windows,
            "SERIAL-001");
}
