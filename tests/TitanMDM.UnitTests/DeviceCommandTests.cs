using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

namespace TitanMDM.UnitTests;

public sealed class DeviceCommandTests
{
    [Fact]
    public void DeliveryAndExecutionAreMonotonicAndIdempotent()
    {
        var command = CreateCommand();
        command.Queue();
        command.MarkDispatching();
        command.MarkSent();

        command.MarkDelivered();
        var deliveredAt = command.DeliveredAtUtc;
        command.MarkDelivered();

        Assert.Equal(DeviceCommandStatus.Delivered, command.Status);
        Assert.Equal(deliveredAt, command.DeliveredAtUtc);

        command.MarkExecuting();
        var startedAt = command.StartedAtUtc;
        Assert.Throws<InvalidOperationException>(command.MarkDelivered);
        command.MarkExecuting();

        Assert.Equal(DeviceCommandStatus.Executing, command.Status);
        Assert.Equal(startedAt, command.StartedAtUtc);
    }

    [Fact]
    public void TerminalCommandRejectsLateDeliveryAcknowledgement()
    {
        var command = CreateExecutingCommand();
        command.CompleteSuccess("{}");

        Assert.Throws<InvalidOperationException>(command.MarkDelivered);
        Assert.Throws<InvalidOperationException>(command.MarkExecuting);
        Assert.Equal(DeviceCommandStatus.Success, command.Status);
    }

    [Fact]
    public void SuccessAcknowledgementIsIdempotent()
    {
        var command = CreateExecutingCommand();

        command.CompleteSuccess("{\"accepted\":true}");
        var completedAt = command.CompletedAtUtc;
        command.CompleteSuccess("{\"accepted\":true}");

        Assert.Equal(DeviceCommandStatus.Success, command.Status);
        Assert.Equal(completedAt, command.CompletedAtUtc);
    }

    [Fact]
    public void DeliveredCommandCannotBeCancelled()
    {
        var command = CreateCommand();
        command.Queue();
        command.MarkDispatching();
        command.MarkSent();
        command.MarkDelivered();

        Assert.Throws<InvalidOperationException>(command.Cancel);
        Assert.Equal(DeviceCommandStatus.Delivered, command.Status);
    }

    [Fact]
    public void CommandCannotExecuteBeforeDelivery()
    {
        var command = CreateCommand();
        command.Queue();

        Assert.Throws<InvalidOperationException>(command.MarkExecuting);
        Assert.Equal(DeviceCommandStatus.Queued, command.Status);
    }

    private static DeviceCommand CreateExecutingCommand()
    {
        var command = CreateCommand();
        command.Queue();
        command.MarkDispatching();
        command.MarkSent();
        command.MarkDelivered();
        command.MarkExecuting();
        return command;
    }

    private static DeviceCommand CreateCommand()
    {
        return new DeviceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LOCK_DEVICE",
            "{}",
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(2));
    }
}
