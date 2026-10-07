namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskTicketEvent
{
    private HelpdeskTicketEvent()
    {
    }

    public HelpdeskTicketEvent(
        Guid organizationId,
        Guid ticketId,
        Guid? actorUserId,
        string eventType,
        string summary)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TicketId = ticketId;
        ActorUserId = actorUserId;
        EventType = eventType.Trim().ToLowerInvariant();
        Summary = summary.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}
