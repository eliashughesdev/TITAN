namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskSiteCoverage
{
    private HelpdeskSiteCoverage()
    {
    }

    public HelpdeskSiteCoverage(
        Guid organizationId,
        Guid teamId,
        Guid siteId,
        Guid? siteLocationId,
        string? category,
        int priority = 100)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "TeamId is required.",
                nameof(teamId));
        }

        if (siteId == Guid.Empty)
        {
            throw new ArgumentException(
                "SiteId is required.",
                nameof(siteId));
        }

        if (priority is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                "Priority must be between 1 and 1000.");
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        TeamId =
            teamId;

        SiteId =
            siteId;

        SiteLocationId =
            siteLocationId;

        Category =
            NormalizeCategory(
                category);

        Priority =
            priority;

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

    public Guid TeamId
    {
        get;
        private set;
    }

    public Guid SiteId
    {
        get;
        private set;
    }

    public Guid? SiteLocationId
    {
        get;
        private set;
    }

    /// <summary>
    /// Null significa cobertura para cualquier categoría.
    /// </summary>
    public string? Category
    {
        get;
        private set;
    }

    /// <summary>
    /// Menor número = mayor prioridad.
    /// </summary>
    public int Priority
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
        Guid? siteLocationId,
        string? category,
        int priority)
    {
        if (priority is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                "Priority must be between 1 and 1000.");
        }

        SiteLocationId =
            siteLocationId;

        Category =
            NormalizeCategory(
                category);

        Priority =
            priority;

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

    private static string?
        NormalizeCategory(
            string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length > 80)
        {
            throw new ArgumentException(
                "Category cannot exceed 80 characters.",
                nameof(value));
        }

        return normalized;
    }
}