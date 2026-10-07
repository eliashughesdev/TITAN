namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskMailProcessingLog
{
    private HelpdeskMailProcessingLog()
    {
    }

    public HelpdeskMailProcessingLog(
        Guid organizationId,
        string mailbox,
        string internetMessageId,
        string? conversationId,
        string fromEmail,
        string? subject,
        string decision,
        string reasonCode,
        string reason,
        DateTime? receivedAtUtc,
        Guid? ticketId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        Mailbox =
            NormalizeRequired(
                mailbox,
                320,
                nameof(mailbox));

        InternetMessageId =
            NormalizeRequired(
                internetMessageId,
                500,
                nameof(internetMessageId));

        ConversationId =
            NormalizeOptional(
                conversationId,
                500);

        FromEmail =
            NormalizeRequired(
                fromEmail,
                320,
                nameof(fromEmail));

        Subject =
            NormalizeOptional(
                subject,
                250);

        Decision =
            NormalizeRequired(
                decision,
                30,
                nameof(decision))
                .ToLowerInvariant();

        ReasonCode =
            NormalizeRequired(
                reasonCode,
                80,
                nameof(reasonCode))
                .ToLowerInvariant();

        Reason =
            NormalizeRequired(
                reason,
                1000,
                nameof(reason));

        ReceivedAtUtc =
            receivedAtUtc;

        TicketId =
            ticketId;

        ProcessedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id
    {
        get;
        private set;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid? TicketId
    {
        get;
        private set;
    }

    public string Mailbox
    {
        get;
        private set;
    } = string.Empty;

    public string InternetMessageId
    {
        get;
        private set;
    } = string.Empty;

    public string? ConversationId
    {
        get;
        private set;
    }

    public string FromEmail
    {
        get;
        private set;
    } = string.Empty;

    public string? Subject
    {
        get;
        private set;
    }

    /*
     * accepted
     * ignored
     * failed
     */
    public string Decision
    {
        get;
        private set;
    } = string.Empty;

    public string ReasonCode
    {
        get;
        private set;
    } = string.Empty;

    public string Reason
    {
        get;
        private set;
    } = string.Empty;

    public DateTime? ReceivedAtUtc
    {
        get;
        private set;
    }

    public DateTime ProcessedAtUtc
    {
        get;
        private set;
    }

    public void LinkTicket(
        Guid ticketId)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "TicketId is required.",
                nameof(ticketId));
        }

        TicketId =
            ticketId;
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string parameter)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{parameter} is required.",
                parameter);
        }

        var normalized =
            value.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                maxLength)];
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                maxLength)];
    }
}