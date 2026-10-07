namespace TitanMDM.Domain.Entities;

public sealed class RemoteSessionParticipant
{
    private RemoteSessionParticipant()
    {
    }

    public RemoteSessionParticipant(
        Guid organizationId,
        Guid remoteSessionId,
        Guid userId,
        string displayName,
        bool canControl)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.");
        }

        if (
            remoteSessionId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "RemoteSessionId is required.");
        }

        if (
            userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "UserId is required.");
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

        CanControl =
            canControl;

        IsConnected =
            false;

        JoinedAtUtc =
            DateTime.UtcNow;

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

    public bool CanControl
    {
        get;
        private set;
    }

    public bool IsConnected
    {
        get;
        private set;
    }

    public DateTime JoinedAtUtc
    {
        get;
        private set;
    }

    public DateTime? ConnectedAtUtc
    {
        get;
        private set;
    }

    public DateTime? DisconnectedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public void MarkConnected()
    {
        IsConnected =
            true;

        ConnectedAtUtc =
            DateTime.UtcNow;

        DisconnectedAtUtc =
            null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkDisconnected()
    {
        IsConnected =
            false;

        DisconnectedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void GrantControl()
    {
        CanControl =
            true;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void RevokeControl()
    {
        CanControl =
            false;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}