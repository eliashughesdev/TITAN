using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class HelpdeskEmailMessageDomainTests
{
    [Fact]
    public void Constructor_ShouldNormalizeMailbox()
    {
        var entity =
            new HelpdeskEmailMessage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                " HELP@EXAMPLE.COM ",
                "<message-1@example.com>",
                "conversation-1");

        Assert.Equal(
            "help@example.com",
            entity.Mailbox);
    }

    [Fact]
    public void Constructor_ShouldGenerateStableMessageKey()
    {
        var organizationId =
            Guid.NewGuid();

        var ticketId =
            Guid.NewGuid();

        var first =
            new HelpdeskEmailMessage(
                organizationId,
                ticketId,
                "help@example.com",
                "<same-message@example.com>",
                "conversation-1");

        var second =
            new HelpdeskEmailMessage(
                organizationId,
                ticketId,
                "help@example.com",
                "<same-message@example.com>",
                "conversation-1");

        Assert.Equal(
            first.MessageKey,
            second.MessageKey);

        Assert.Equal(
            64,
            first.MessageKey.Length);
    }

    [Fact]
    public void DifferentMessageIds_ShouldGenerateDifferentKeys()
    {
        var organizationId =
            Guid.NewGuid();

        var ticketId =
            Guid.NewGuid();

        var first =
            new HelpdeskEmailMessage(
                organizationId,
                ticketId,
                "help@example.com",
                "<message-a@example.com>",
                "conversation-1");

        var second =
            new HelpdeskEmailMessage(
                organizationId,
                ticketId,
                "help@example.com",
                "<message-b@example.com>",
                "conversation-1");

        Assert.NotEqual(
            first.MessageKey,
            second.MessageKey);
    }

    [Fact]
    public void ConversationId_ShouldBeTrimmed()
    {
        var entity =
            new HelpdeskEmailMessage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "help@example.com",
                "<message@example.com>",
                "  conversation-123  ");

        Assert.Equal(
            "conversation-123",
            entity.ConversationId);
    }

    [Fact]
    public void EmptyConversationId_ShouldBecomeNull()
    {
        var entity =
            new HelpdeskEmailMessage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "help@example.com",
                "<message@example.com>",
                "   ");

        Assert.Null(
            entity.ConversationId);
    }

    [Fact]
    public void MissingMailbox_ShouldFail()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new HelpdeskEmailMessage(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "",
                    "<message@example.com>",
                    null));
    }

    [Fact]
    public void MissingInternetMessageId_ShouldFail()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new HelpdeskEmailMessage(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "help@example.com",
                    "",
                    null));
    }

    [Fact]
    public void EmptyOrganization_ShouldFail()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new HelpdeskEmailMessage(
                    Guid.Empty,
                    Guid.NewGuid(),
                    "help@example.com",
                    "<message@example.com>",
                    null));
    }

    [Fact]
    public void EmptyTicket_ShouldFail()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new HelpdeskEmailMessage(
                    Guid.NewGuid(),
                    Guid.Empty,
                    "help@example.com",
                    "<message@example.com>",
                    null));
    }
}