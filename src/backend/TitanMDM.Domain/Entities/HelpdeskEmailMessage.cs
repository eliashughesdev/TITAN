using System.Security.Cryptography;
using System.Text;

namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskEmailMessage
{
    private HelpdeskEmailMessage()
    {
    }

    public HelpdeskEmailMessage(
        Guid organizationId,
        Guid ticketId,
        string mailbox,
        string internetMessageId,
        string? conversationId)
    {
        if (organizationId == Guid.Empty ||
            ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization and ticket are required.");
        }

        if (string.IsNullOrWhiteSpace(mailbox) ||
            mailbox.Trim().Length > 320 ||
            string.IsNullOrWhiteSpace(internetMessageId) ||
            internetMessageId.Trim().Length > 998 ||
            conversationId?.Trim().Length > 512)
        {
            throw new ArgumentException(
                "Mail identity is invalid.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TicketId = ticketId;
        Mailbox = mailbox.Trim().ToLowerInvariant();
        InternetMessageId = internetMessageId.Trim();

        MessageKey = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(InternetMessageId)));

        ConversationId =
            string.IsNullOrWhiteSpace(conversationId)
                ? null
                : conversationId.Trim();

        ImportedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TicketId { get; private set; }
    public string Mailbox { get; private set; } = string.Empty;
    public string InternetMessageId { get; private set; } = string.Empty;
    public string MessageKey { get; private set; } = string.Empty;
    public string? ConversationId { get; private set; }
    public DateTime ImportedAtUtc { get; private set; }
}