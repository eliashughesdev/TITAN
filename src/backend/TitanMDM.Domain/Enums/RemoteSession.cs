using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class RemoteSession
{
    private RemoteSession()
    {
    }

    public RemoteSession(
        Guid organizationId,
        Guid deviceId,
        Guid requestedByUserId,
        string technicianName,
        string reason,
        bool allowKeyboard,
        bool allowMouse,
        bool allowClipboard,
        bool allowFileTransfer,
        DateTime expiresAtUtc)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));

        if (requestedByUserId == Guid.Empty)
            throw new ArgumentException(
                "RequestedByUserId is required.",
                nameof(requestedByUserId));

        if (string.IsNullOrWhiteSpace(
                technicianName))
            throw new ArgumentException(
                "Technician name is required.",
                nameof(technicianName));

        if (string.IsNullOrWhiteSpace(
                reason))
            throw new ArgumentException(
                "Remote support reason is required.",
                nameof(reason));

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Expiration must be in the future.",
                nameof(expiresAtUtc));

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        DeviceId =
            deviceId;

        RequestedByUserId =
            requestedByUserId;

        TechnicianName =
            technicianName.Trim();

        Reason =
            reason.Trim();

        AllowKeyboard =
            allowKeyboard;

        AllowMouse =
            allowMouse;

        AllowClipboard =
            allowClipboard;

        AllowFileTransfer =
            allowFileTransfer;

        Status =
            RemoteSessionStatus.Requested;

        RequestedAtUtc =
            DateTime.UtcNow;

        ExpiresAtUtc =
            NormalizeUtc(
                expiresAtUtc);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid DeviceId
    {
        get;
        private set;
    }

    public Guid RequestedByUserId
    {
        get;
        private set;
    }

    public string TechnicianName
    {
        get;
        private set;
    } = string.Empty;

    public string Reason
    {
        get;
        private set;
    } = string.Empty;

    public RemoteSessionStatus Status
    {
        get;
        private set;
    }

    public bool AllowKeyboard
    {
        get;
        private set;
    }

    public bool AllowMouse
    {
        get;
        private set;
    }

    public bool AllowClipboard
    {
        get;
        private set;
    }

    public bool AllowFileTransfer
    {
        get;
        private set;
    }

    public DateTime RequestedAtUtc
    {
        get;
        private set;
    }

    public DateTime ExpiresAtUtc
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

    public string? FailureReason
    {
        get;
        private set;
    }

    public string? TerminationReason
    {
        get;
        private set;
    }

    public string? TerminatedBy
    {
        get;
        private set;
    }

    public void MarkConnecting()
    {
        EnsureNotTerminal();

        Status =
            RemoteSessionStatus.Connecting;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkConnected()
    {
        EnsureNotTerminal();

        Status =
            RemoteSessionStatus.Connected;

        ConnectedAtUtc ??=
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkDisconnecting()
    {
        EnsureNotTerminal();

        Status =
            RemoteSessionStatus.Disconnecting;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Complete(
        string terminatedBy,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(
                terminatedBy))
        {
            throw new ArgumentException(
                "TerminatedBy is required.",
                nameof(terminatedBy));
        }

        if (IsTerminal)
            return;

        Status =
            RemoteSessionStatus.Completed;

        DisconnectedAtUtc =
            DateTime.UtcNow;

        TerminatedBy =
            terminatedBy.Trim();

        TerminationReason =
            Normalize(reason);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Fail(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(
                reason))
        {
            throw new ArgumentException(
                "Failure reason is required.",
                nameof(reason));
        }

        if (IsTerminal)
            return;

        Status =
            RemoteSessionStatus.Failed;

        FailureReason =
            reason.Trim();

        DisconnectedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Cancel(
        string terminatedBy,
        string? reason = null)
    {
        if (IsTerminal)
            return;

        Status =
            RemoteSessionStatus.Cancelled;

        TerminatedBy =
            Normalize(
                terminatedBy);

        TerminationReason =
            Normalize(
                reason);

        DisconnectedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Expire()
    {
        if (IsTerminal)
            return;

        Status =
            RemoteSessionStatus.Expired;

        DisconnectedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public bool HasExpired =>
        DateTime.UtcNow >=
        ExpiresAtUtc;

    public bool IsTerminal =>
        Status is
            RemoteSessionStatus.Completed
            or RemoteSessionStatus.Failed
            or RemoteSessionStatus.Expired
            or RemoteSessionStatus.Cancelled;

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException(
                "The remote session is already terminal.");
        }

        if (HasExpired)
        {
            Expire();

            throw new InvalidOperationException(
                "The remote session has expired.");
        }
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }
}