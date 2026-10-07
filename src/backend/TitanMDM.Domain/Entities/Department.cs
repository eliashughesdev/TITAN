namespace TitanMDM.Domain.Entities;

public sealed class Department
{
    private Department()
    {
    }

    public Department(
        Guid organizationId,
        string name)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Department name is required.",
                nameof(name));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();

        IsActive = true;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Department name is required.",
                nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}