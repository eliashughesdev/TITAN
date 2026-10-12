using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class HelpdeskMailProcessingRecoveryTests
{
    private static HelpdeskMailProcessingLog CreateFailedLog()
    {
        return new HelpdeskMailProcessingLog(
            organizationId: Guid.NewGuid(),
            mailbox: "helpdesk@example.com",
            internetMessageId: "<message-101@example.com>",
            conversationId: "conversation-101",
            fromEmail: "usuario@example.com",
            subject: "Incidencia de impresora",
            decision: "failed",
            reasonCode: "processing_error",
            reason: "Error temporal importando ticket.",
            receivedAtUtc: DateTime.UtcNow);
    }

    [Fact]
    public void FailedMessage_CanBecomeAccepted()
    {
        var log = CreateFailedLog();
        var ticketId = Guid.NewGuid();

        log.UpdateOutcome(
            "accepted",
            "accepted",
            "Ticket importado correctamente.",
            ticketId);

        Assert.Equal("accepted", log.Decision);
        Assert.Equal(ticketId, log.TicketId);
        Assert.Equal("accepted", log.ReasonCode);
    }

    [Fact]
    public void FailedMessage_CanBecomeIgnored()
    {
        var log = CreateFailedLog();

        log.UpdateOutcome(
            "ignored",
            "blocked_sender",
            "Remitente bloqueado.");

        Assert.Equal("ignored", log.Decision);
        Assert.Null(log.TicketId);
    }

    [Fact]
    public void AcceptedMessage_CannotReturnToFailed()
    {
        var log = CreateFailedLog();

        log.UpdateOutcome(
            "accepted",
            "accepted",
            "Importado.",
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => log.UpdateOutcome(
                "failed",
                "processing_error",
                "Fallo posterior."));
    }

    [Fact]
    public void AcceptedMessage_RequiresTicket()
    {
        var log = CreateFailedLog();

        Assert.Throws<ArgumentException>(
            () => log.UpdateOutcome(
                "accepted",
                "accepted",
                "Importado."));
    }

    [Fact]
    public void FailedMessage_CanRecordAnotherFailure()
    {
        var log = CreateFailedLog();

        log.UpdateOutcome(
            "failed",
            "graph_timeout",
            "Microsoft Graph agotó el tiempo de espera.");

        Assert.Equal("failed", log.Decision);
        Assert.Equal("graph_timeout", log.ReasonCode);
    }

    [Fact]
    public void UnknownDecision_IsRejected()
    {
        var log = CreateFailedLog();

        Assert.Throws<ArgumentException>(
            () => log.UpdateOutcome(
                "unknown",
                "unknown",
                "Resultado inválido."));
    }
}