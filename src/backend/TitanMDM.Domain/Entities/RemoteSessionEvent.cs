namespace TitanMDM.Domain.Entities;

public sealed class RemoteSessionEvent
{
    private RemoteSessionEvent()
    {
    }

    public RemoteSessionEvent(
        Guid organizationId,
        Guid remoteSessionId,
        string eventType,
        string description,
        Guid? userId = null,
        string? metadataJson = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (remoteSessionId == Guid.Empty)
            throw new ArgumentException(
                "RemoteSessionId is required.",
                nameof(remoteSessionId));

        if (string.IsNullOrWhiteSpace(
                eventType))
            throw new ArgumentException(
                "EventType is required.",
                nameof(eventType));

        if (string.IsNullOrWhiteSpace(
                description))
            throw new ArgumentException(
                "Description is required.",
                nameof(description));

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        RemoteSessionId =
            remoteSessionId;

        UserId =
            userId;

        EventType =
            eventType.Trim();

        Description =
            description.Trim();

        MetadataJson =
            string.IsNullOrWhiteSpace(
                metadataJson)
                ? null
                : metadataJson.Trim();

        OccurredAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid RemoteSessionId
    {
        get;
        private set;
    }

    public Guid? UserId
    {
        get;
        private set;
    }

    public string EventType
    {
        get;
        private set;
    } = string.Empty;

    public string Description
    {
        get;
        private set;
    } = string.Empty;

    public string? MetadataJson
    {
        get;
        private set;
    }

    public DateTime OccurredAtUtc
    {
        get;
        private set;
    }
}