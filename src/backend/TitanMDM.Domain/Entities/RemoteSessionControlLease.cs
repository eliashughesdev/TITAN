namespace TitanMDM.Domain.Entities;

public sealed class RemoteSessionControlLease
{
    private RemoteSessionControlLease()
    {
    }

    public RemoteSessionControlLease(
        Guid organizationId,
        Guid remoteSessionId,
        Guid userId,
        string displayName,
        TimeSpan duration)
    {
        if (
            organizationId ==
            Guid.Empty
            ||
            remoteSessionId ==
            Guid.Empty
            ||
            userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Remote control lease contains an invalid identifier.");
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        RemoteSessionId =
            remoteSessionId;

        UserId =
            userId;

        DisplayName =
            string.IsNullOrWhiteSpace(
                displayName)
                ? userId.ToString()
                : displayName.Trim();

        AcquiredAtUtc =
            DateTime.UtcNow;

        ExpiresAtUtc =
            DateTime.UtcNow.Add(
                duration);

        UpdatedAtUtc =
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

    public Guid RemoteSessionId
    {
        get;
        private set;
    }

    public Guid UserId
    {
        get;
        private set;
    }

    public string DisplayName
    {
        get;
        private set;
    } = string.Empty;

    public DateTime AcquiredAtUtc
    {
        get;
        private set;
    }

    public DateTime ExpiresAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public bool IsExpired()
    {
        return
            ExpiresAtUtc <=
            DateTime.UtcNow;
    }

    public void Renew(
        TimeSpan duration)
    {
        ExpiresAtUtc =
            DateTime.UtcNow.Add(
                duration);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}