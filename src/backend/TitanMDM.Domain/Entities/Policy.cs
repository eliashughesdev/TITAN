using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class Policy
{
    private Policy()
    {
    }

    public Policy(
        Guid organizationId,
        string name,
        string? description,
        PolicyPlatform platform,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Policy name is required.");

        Id = Guid.NewGuid();

        OrganizationId = organizationId;

        Name = name.Trim();

        Description =
            Normalize(description);

        Platform = platform;

        Status = PolicyStatus.Draft;

        CurrentVersion = 1;

        CreatedByUserId =
            createdByUserId;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public string Name
    {
        get;
        private set;
    } = string.Empty;

    public string? Description
    {
        get;
        private set;
    }

    public PolicyPlatform Platform
    {
        get;
        private set;
    }

    public PolicyStatus Status
    {
        get;
        private set;
    }

    public int CurrentVersion
    {
        get;
        private set;
    }

    public Guid CreatedByUserId
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

    public DateTime? ActivatedAtUtc
    {
        get;
        private set;
    }

    public DateTime? ArchivedAtUtc
    {
        get;
        private set;
    }

    public void Update(
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Policy name is required.");
        }

        Name = name.Trim();

        Description =
            Normalize(description);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void SetCurrentVersion(
        int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version));
        }

        CurrentVersion = version;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = PolicyStatus.Active;

        ActivatedAtUtc =
            DateTime.UtcNow;

        ArchivedAtUtc = null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Disable()
    {
        Status = PolicyStatus.Disabled;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = PolicyStatus.Archived;

        ArchivedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}