namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskTicketComment
{
    private HelpdeskTicketComment()
    {
    }

    public HelpdeskTicketComment(
        Guid organizationId,
        Guid ticketId,
        Guid authorUserId,
        string body,
        bool isInternal)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.", nameof(ticketId));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        Body = body.Trim();
        IsInternal = isInternal;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public string? ExternalAuthorName { get; private set; }
    public string? ExternalAuthorEmail { get; private set; }

    public void SetEmailAuthor(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
            throw new ArgumentException(
                "Valid author email is required.",
                nameof(email));

        ExternalAuthorName = string.IsNullOrWhiteSpace(name)
    ? email.Trim()[..Math.Min(email.Trim().Length, 200)]
    : name.Trim()[..Math.Min(name.Trim().Length, 200)];
        ExternalAuthorEmail = email.Trim().ToLowerInvariant();
    }
}

