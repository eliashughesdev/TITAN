namespace TitanMDM.Domain.Entities;

public sealed class SoftwareDeployment
{
    private SoftwareDeployment()
    {
    }

    public SoftwareDeployment(
        Guid organizationId,
        Guid packageId,
        string targetType,
        Guid targetId,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (packageId == Guid.Empty)
            throw new ArgumentException(
                "PackageId is required.");

        if (targetId == Guid.Empty)
            throw new ArgumentException(
                "TargetId is required.");

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.");

        if (string.IsNullOrWhiteSpace(targetType))
            throw new ArgumentException(
                "TargetType is required.");

        Id = Guid.NewGuid();

        OrganizationId =
            organizationId;

        PackageId =
            packageId;

        TargetType =
            targetType.Trim();

        TargetId =
            targetId;

        CreatedByUserId =
            createdByUserId;

        Status =
            "Queued";

        CreatedAtUtc =
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

    public Guid PackageId
    {
        get;
        private set;
    }

    public string TargetType
    {
        get;
        private set;
    } = string.Empty;

    public Guid TargetId
    {
        get;
        private set;
    }

    public Guid CreatedByUserId
    {
        get;
        private set;
    }

    public string Status
    {
        get;
        private set;
    } = "Queued";

    public int QueuedDevices
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public void MarkQueued(
        int queuedDevices)
    {
        QueuedDevices =
            Math.Max(
                queuedDevices,
                0);

        Status =
            queuedDevices > 0
                ? "Queued"
                : "Empty";

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}