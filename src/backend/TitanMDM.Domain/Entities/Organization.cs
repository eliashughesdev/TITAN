namespace TitanMDM.Domain.Entities;

public sealed class Organization
{
    private Organization()
    {
    }

    public Organization(
        string name,
        string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Organization name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Organization code is required.",
                nameof(code));

        Id = Guid.NewGuid();

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();

        IsActive = true;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? PrimaryDomain { get; private set; }

    public string? Country { get; private set; }

    public string? TimeZone { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateInformation(
        string name,
        string? description,
        string? primaryDomain,
        string? country,
        string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Organization name is required.",
                nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        PrimaryDomain = primaryDomain?.Trim();
        Country = country?.Trim();
        TimeZone = timeZone?.Trim();

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLogo(string? logoUrl)
    {
        LogoUrl = logoUrl?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}