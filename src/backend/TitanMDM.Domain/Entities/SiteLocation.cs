namespace TitanMDM.Domain.Entities;

public sealed class SiteLocation
{
    private SiteLocation()
    {
    }

    public SiteLocation(
        Guid organizationId,
        Guid siteId,
        string name)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (siteId == Guid.Empty)
        {
            throw new ArgumentException(
                "SiteId is required.",
                nameof(siteId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Location name is required.",
                nameof(name));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        SiteId =
            siteId;

        Name =
            name.Trim();

        IsActive =
            true;

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

    public Guid SiteId
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

    public bool IsActive
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

    public void Update(
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Location name is required.",
                nameof(name));
        }

        Name =
            name.Trim();

        Description =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive =
            true;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive =
            false;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}