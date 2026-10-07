using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class HelpdeskOutboundEmailTests
{
    [Fact]
    public void Constructor_ShouldCreatePendingMessage()
    {
        var message =
            CreateMessage();

        Assert.Equal(
            HelpdeskOutboundEmail.PendingStatus,
            message.Status);

        Assert.Equal(
            0,
            message.AttemptCount);

        Assert.Null(
            message.SentAtUtc);
    }

    [Fact]
    public void Constructor_ShouldNormalizeEmail()
    {
        var message =
            new HelpdeskOutboundEmail(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                " USER@EXAMPLE.COM ",
                "Subject",
                "Body");

        Assert.Equal(
            "user@example.com",
            message.ToEmail);
    }

    [Fact]
    public void MarkSending_ShouldIncrementAttempt()
    {
        var message =
            CreateMessage();

        message.MarkSending();

        Assert.Equal(
            HelpdeskOutboundEmail.SendingStatus,
            message.Status);

        Assert.Equal(
            1,
            message.AttemptCount);

        Assert.NotNull(
            message.LastAttemptAtUtc);
    }

    [Fact]
    public void MarkSent_ShouldCompleteDelivery()
    {
        var message =
            CreateMessage();

        message.MarkSending();

        message.MarkSent();

        Assert.Equal(
            HelpdeskOutboundEmail.SentStatus,
            message.Status);

        Assert.NotNull(
            message.SentAtUtc);

        Assert.Null(
            message.NextAttemptAtUtc);

        Assert.Null(
            message.LastError);
    }

    [Fact]
    public void Failure_ShouldScheduleRetry()
    {
        var message =
            CreateMessage();

        message.MarkSending();

        message.MarkFailure(
            "Graph unavailable.",
            8);

        Assert.Equal(
            HelpdeskOutboundEmail.RetryStatus,
            message.Status);

        Assert.NotNull(
            message.NextAttemptAtUtc);

        Assert.Equal(
            "Graph unavailable.",
            message.LastError);
    }

    [Fact]
    public void MaxFailures_ShouldMoveToDeadLetter()
    {
        var message =
            CreateMessage();

        for (var attempt = 0;
             attempt < 3;
             attempt++)
        {
            message.MarkSending();

            message.MarkFailure(
                "Failure.",
                3);
        }

        Assert.Equal(
            HelpdeskOutboundEmail.DeadLetterStatus,
            message.Status);

        Assert.Equal(
            3,
            message.AttemptCount);

        Assert.Null(
            message.NextAttemptAtUtc);
    }

    [Fact]
    public void RecoverAbandonedSend_ShouldScheduleRetry()
    {
        var message =
            CreateMessage();

        message.MarkSending();

        message.RecoverAbandonedSend();

        Assert.Equal(
            HelpdeskOutboundEmail.RetryStatus,
            message.Status);

        Assert.NotNull(
            message.NextAttemptAtUtc);
    }

    [Fact]
    public void EmptyDestination_ShouldFail()
    {
        Assert.Throws<
            ArgumentException>(
            () =>
                new HelpdeskOutboundEmail(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "",
                    "Subject",
                    "Body"));
    }

    [Fact]
    public void EmptyCommentId_ShouldFail()
    {
        Assert.Throws<
            ArgumentException>(
            () =>
                new HelpdeskOutboundEmail(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.Empty,
                    "user@example.com",
                    "Subject",
                    "Body"));
    }

    private static HelpdeskOutboundEmail
        CreateMessage()
    {
        return new HelpdeskOutboundEmail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user@example.com",
            "TitanMDM Helpdesk",
            "Respuesta de prueba.");
    }
}