namespace TitanMDM.Domain.Entities;

public sealed class Site
{
    private Site()
    {
    }

    public Site(
        Guid organizationId,
        string code,
        string name)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Site code is required.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Site name is required.",
                nameof(name));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        Code =
            NormalizeCode(code);

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

    public string Code
    {
        get;
        private set;
    } = string.Empty;

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

    public string? Address
    {
        get;
        private set;
    }

    public string? City
    {
        get;
        private set;
    }

    public string? Province
    {
        get;
        private set;
    }

    public string? Country
    {
        get;
        private set;
    }

    public string? TimeZoneId
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
        string? description,
        string? address,
        string? city,
        string? province,
        string? country,
        string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Site name is required.",
                nameof(name));
        }

        Name =
            name.Trim();

        Description =
            Normalize(description);

        Address =
            Normalize(address);

        City =
            Normalize(city);

        Province =
            Normalize(province);

        Country =
            Normalize(country);

        TimeZoneId =
            Normalize(timeZoneId);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void RenameCode(
        string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Site code is required.",
                nameof(code));
        }

        Code =
            NormalizeCode(code);

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

    private static string NormalizeCode(
        string value)
    {
        return value
            .Trim()
            .ToUpperInvariant();
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}