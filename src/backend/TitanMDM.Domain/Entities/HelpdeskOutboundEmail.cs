namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskOutboundEmail
{
    private HelpdeskOutboundEmail()
    {
    }

    public HelpdeskOutboundEmail(
        Guid organizationId,
        Guid ticketId,
        Guid commentId,
        string toEmail,
        string subject,
        string body)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "TicketId is required.",
                nameof(ticketId));
        }

        if (commentId == Guid.Empty)
        {
            throw new ArgumentException(
                "CommentId is required.",
                nameof(commentId));
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException(
                "Destination email is required.",
                nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException(
                "Subject is required.",
                nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException(
                "Body is required.",
                nameof(body));
        }

        var normalizedEmail =
            toEmail.Trim().ToLowerInvariant();

        if (normalizedEmail.Length > 320)
        {
            throw new ArgumentException(
                "Destination email is too long.",
                nameof(toEmail));
        }

        Id = Guid.NewGuid();

        OrganizationId =
            organizationId;

        TicketId =
            ticketId;

        CommentId =
            commentId;

        ToEmail =
            normalizedEmail;

        Subject =
            subject.Trim()[
                ..Math.Min(
                    subject.Trim().Length,
                    250)];

        Body =
            body.Trim()[
                ..Math.Min(
                    body.Trim().Length,
                    10000)];

        Status =
            PendingStatus;

        AttemptCount =
            0;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            CreatedAtUtc;
    }

    public const string PendingStatus =
        "pending";

    public const string SendingStatus =
        "sending";

    public const string SentStatus =
        "sent";

    public const string RetryStatus =
        "retry";

    public const string DeadLetterStatus =
        "deadletter";

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid CommentId { get; private set; }

    public string ToEmail { get; private set; } =
        string.Empty;

    public string Subject { get; private set; } =
        string.Empty;

    public string Body { get; private set; } =
        string.Empty;

    public string Status { get; private set; } =
        PendingStatus;

    public int AttemptCount { get; private set; }

    public DateTime? NextAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? LastAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? SentAtUtc
    {
        get;
        private set;
    }

    public string? LastError { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkSending()
    {
        if (Status == SentStatus)
        {
            return;
        }

        Status =
            SendingStatus;

        AttemptCount++;

        LastAttemptAtUtc =
            DateTime.UtcNow;

        NextAttemptAtUtc =
            null;

        LastError =
            null;

        UpdatedAtUtc =
            LastAttemptAtUtc.Value;
    }

    public void MarkSent()
    {
        var now =
            DateTime.UtcNow;

        Status =
            SentStatus;

        SentAtUtc =
            now;

        NextAttemptAtUtc =
            null;

        LastError =
            null;

        UpdatedAtUtc =
            now;
    }

    public void MarkFailure(
        string? error,
        int maxAttempts)
    {
        var now =
            DateTime.UtcNow;

        LastAttemptAtUtc =
            now;

        LastError =
            NormalizeError(
                error);

        if (AttemptCount >=
            Math.Max(1, maxAttempts))
        {
            Status =
                DeadLetterStatus;

            NextAttemptAtUtc =
                null;

            UpdatedAtUtc =
                now;

            return;
        }

        Status =
            RetryStatus;

        var exponent =
            Math.Clamp(
                AttemptCount - 1,
                0,
                6);

        var delayMinutes =
            Math.Min(
                60,
                Math.Pow(
                    2,
                    exponent));

        NextAttemptAtUtc =
            now.AddMinutes(
                delayMinutes);

        UpdatedAtUtc =
            now;
    }

    public void RecoverAbandonedSend()
    {
        if (Status !=
            SendingStatus)
        {
            return;
        }

        Status =
            RetryStatus;

        NextAttemptAtUtc =
            DateTime.UtcNow;

        LastError =
            "Envío anterior interrumpido antes de confirmarse.";

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    private static string NormalizeError(
        string? error)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                error)
                ? "Error desconocido."
                : error.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                2000)];
    }
}